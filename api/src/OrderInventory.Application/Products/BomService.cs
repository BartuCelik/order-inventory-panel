using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

/// <summary>Editing a product's bill of materials (rules 5 and 6).</summary>
public sealed class BomService(IAppDbContext db)
{
    /// <summary>
    /// Key for <c>pg_advisory_xact_lock</c> that serialises structural BOM changes across the database.
    /// The value is arbitrary; it only has to be used for nothing else.
    /// </summary>
    private const long BomStructureLockKey = 6_100_001;

    public async Task<BomLineDto> AddLineAsync(int productId, AddBomLineRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Why a lock: the cycle check reads the whole BOM, then inserts. Two requests adding A→B and
        // B→A at the same time would each read a BOM without the other's line, both pass, and leave a
        // cycle that no constraint can catch. A transaction-scoped advisory lock makes BOM additions run
        // one at a time; it is released automatically at commit or rollback. BOM edits are rare, so
        // serialising them costs nothing noticeable. Quantity updates and deletes cannot create a
        // cycle and do not take the lock.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({BomStructureLockKey})", cancellationToken);

        var parent = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw ProductService.ProductNotFound(productId);

        var component = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == request.ComponentProductId, cancellationToken)
            ?? throw new BusinessRuleException(
                $"Component product {request.ComponentProductId} does not exist.", "ComponentNotFound");

        if (!component.IsActive)
        {
            throw new BusinessRuleException(
                $"'{component.Code}' is inactive and cannot be added to a bill of materials.", "ComponentInactive");
        }

        // All lines, not only the parent's: an indirect cycle runs through other products' BOMs.
        var existingLines = await db.BomLines.AsNoTracking().ToListAsync(cancellationToken);
        if (BomLineRules.CheckNewLine(parent, component.Id, request.QtyPerUnit, existingLines) is { } violation)
        {
            throw new BusinessRuleException(Describe(violation, parent, component), violation.ToString());
        }

        var line = new BomLine
        {
            ParentProductId = parent.Id,
            ComponentProductId = component.Id,
            QtyPerUnit = request.QtyPerUnit,
        };
        db.BomLines.Add(line);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LoadLineAsync(line.Id, cancellationToken);
    }

    public async Task<BomLineDto> UpdateLineAsync(
        int productId, int lineId, UpdateBomLineRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var line = await FindLineAsync(productId, lineId, cancellationToken);
        if (request.QtyPerUnit <= 0)
        {
            throw new BusinessRuleException(
                Describe(BomLineViolation.QtyPerUnitNotPositive), nameof(BomLineViolation.QtyPerUnitNotPositive));
        }

        line.QtyPerUnit = request.QtyPerUnit;
        await db.SaveChangesAsync(cancellationToken);

        return await LoadLineAsync(line.Id, cancellationToken);
    }

    /// <remarks>Existing work orders keep their component snapshot (rule 8), so deleting a line is always safe.</remarks>
    public async Task DeleteLineAsync(int productId, int lineId, CancellationToken cancellationToken = default)
    {
        var line = await FindLineAsync(productId, lineId, cancellationToken);
        db.BomLines.Remove(line);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A line is addressed through its parent, so a line id under the wrong product is a 404.</summary>
    private async Task<BomLine> FindLineAsync(int productId, int lineId, CancellationToken cancellationToken) =>
        await db.BomLines.SingleOrDefaultAsync(l => l.Id == lineId && l.ParentProductId == productId, cancellationToken)
        ?? throw new NotFoundException($"BOM line {lineId} was not found on product {productId}.");

    private Task<BomLineDto> LoadLineAsync(int lineId, CancellationToken cancellationToken) =>
        db.BomLines.AsNoTracking()
            .Where(l => l.Id == lineId)
            .Select(BomLineProjection.ToDto)
            .SingleAsync(cancellationToken);

    private static string Describe(BomLineViolation violation, Product? parent = null, Product? component = null) =>
        violation switch
        {
            BomLineViolation.ParentIsRawMaterial =>
                $"'{parent?.Code}' is a raw material; raw materials cannot have a bill of materials.",
            BomLineViolation.QtyPerUnitNotPositive =>
                "Quantity per unit must be greater than zero.",
            BomLineViolation.DuplicateComponent =>
                $"'{component?.Code}' is already a component of '{parent?.Code}'. Change the quantity of the existing line instead.",
            BomLineViolation.WouldCreateCycle when parent?.Id == component?.Id =>
                $"'{parent?.Code}' cannot be a component of itself.",
            BomLineViolation.WouldCreateCycle =>
                $"Adding '{component?.Code}' to '{parent?.Code}' would create a cycle: '{component?.Code}' is already made, directly or indirectly, from '{parent?.Code}'.",
            _ => violation.ToString(),
        };
}
