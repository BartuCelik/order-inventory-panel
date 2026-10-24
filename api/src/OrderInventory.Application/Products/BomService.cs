using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

public sealed class BomService(IAppDbContext db)
{
    // Key of the PostgreSQL advisory lock that serialises BOM writes. Any constant works as long as
    // nothing else uses it; these are the ASCII bytes of "BOM".
    private const long BomWriteLockKey = 0x424F4D;

    private static readonly Expression<Func<BomLine, BomLineDto>> ToDto = l => new BomLineDto(
        l.Id, l.ComponentProductId, l.ComponentProduct!.Code, l.ComponentProduct.Name,
        l.ComponentProduct.Type, l.ComponentProduct.Unit, l.QtyPerUnit);

    public async Task<IReadOnlyList<BomLineDto>> GetLinesAsync(int productId, CancellationToken cancellationToken)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);
        return await db.BomLines
            .Where(l => l.ParentProductId == productId)
            .OrderBy(l => l.ComponentProduct!.Code)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<BomLineDto> AddLineAsync(int productId, AddBomLineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Rule 6 under concurrency. The cycle check reads the whole BOM, then the insert writes one line.
        // Two requests checked side by side (A→B and B→A, or longer loops through products neither
        // request touches) would each see no cycle and together create one. Locking product rows cannot
        // prevent that, because the loop may run through rows neither request locks. So every BOM write
        // takes one transaction-scoped advisory lock first: check and insert run one request at a time,
        // and each check sees every line committed before it. The lock is released on commit or rollback.
        // BOM edits are rare planner actions, so serialising them costs nothing noticeable.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({BomWriteLockKey})", cancellationToken);

        var parent = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw ProductNotFound(productId);
        var component = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ComponentProductId, cancellationToken)
            ?? throw new BusinessRuleException($"Component product {request.ComponentProductId} does not exist.");

        // The cycle check needs every line, not only the parent's, to see indirect cycles.
        var allLines = await db.BomLines.AsNoTracking().ToListAsync(cancellationToken);
        var violation = BomLineRules.CheckNewLine(parent, component.Id, request.QtyPerUnit, allLines);
        if (violation is { } v)
        {
            throw ToException(v, parent, component);
        }

        var line = new BomLine { ParentProductId = parent.Id, ComponentProductId = component.Id, QtyPerUnit = request.QtyPerUnit };
        db.BomLines.Add(line);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BomLineDto(line.Id, component.Id, component.Code, component.Name, component.Type, component.Unit, line.QtyPerUnit);
    }

    /// <remarks>Removing a line cannot create a cycle, so no lock is needed. Existing work orders keep their snapshot (rule 8).</remarks>
    public async Task DeleteLineAsync(int productId, int lineId, CancellationToken cancellationToken)
    {
        var line = await db.BomLines.SingleOrDefaultAsync(l => l.Id == lineId && l.ParentProductId == productId, cancellationToken)
            ?? throw new NotFoundException($"BOM line {lineId} was not found on product {productId}.");

        db.BomLines.Remove(line);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BomExplosionDto> ExplodeAsync(int productId, decimal quantity, CancellationToken cancellationToken)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);

        // A small workshop's whole BOM is a few hundred lines; loading it once is simpler than a recursive SQL query.
        var allLines = await db.BomLines.AsNoTracking().ToListAsync(cancellationToken);
        var need = BomExplosion.Explode(productId, quantity, allLines);

        var componentIds = need.Keys.ToList();
        var items = await db.Products
            .Where(p => componentIds.Contains(p.Id))
            .OrderBy(p => p.Code)
            .Select(p => new BomExplosionItemDto(p.Id, p.Code, p.Name, p.Type, p.Unit, 0m))
            .ToListAsync(cancellationToken);

        return new BomExplosionDto(productId, quantity, items.Select(i => i with { Quantity = need[i.ProductId] }).ToList());
    }

    private async Task EnsureProductExistsAsync(int productId, CancellationToken cancellationToken)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            throw ProductNotFound(productId);
        }
    }

    private static Exception ToException(BomLineViolation violation, Product parent, Product component) => violation switch
    {
        BomLineViolation.ParentIsRawMaterial => new BusinessRuleException(
            $"Product '{parent.Code}' is a raw material; raw materials cannot have bill of materials lines."),
        BomLineViolation.QtyPerUnitNotPositive => new BusinessRuleException(
            "Quantity per unit must be greater than zero."),
        BomLineViolation.DuplicateComponent => new ConflictException(
            $"'{component.Code}' is already a component of '{parent.Code}'."),
        BomLineViolation.WouldCreateCycle => new BusinessRuleException(
            $"Adding '{component.Code}' to '{parent.Code}' would create a cycle in the bill of materials."),
        _ => throw new ArgumentOutOfRangeException(nameof(violation), violation, null),
    };

    private static NotFoundException ProductNotFound(int id) => new($"Product {id} was not found.");
}
