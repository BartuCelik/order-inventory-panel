using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

/// <summary>Product use cases: list, read, create, update and BOM explosion.</summary>
public sealed class ProductService(IAppDbContext db)
{
    // Passed explicitly: without an escape argument Npgsql emits ESCAPE '', which turns escaping off.
    private const string LikeEscape = @"\";

    /// <summary>Every row the list's filters match, for Excel export (see <see cref="Export"/>).</summary>
    public async Task<IReadOnlyList<ProductListItem>> ExportAsync(ProductListQuery query, CancellationToken cancellationToken) =>
        Export.Rows(await ListAsync(query with { Page = 1, PageSize = Export.MaxRows }, cancellationToken));

    public async Task<PagedResult<ProductListItem>> ListAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var products = db.Products.AsNoTracking();
        if (!query.IncludeInactive)
        {
            products = products.Where(p => p.IsActive);
        }

        if (query.Type is { } type)
        {
            products = products.Where(p => p.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLikePattern(query.Search.Trim())}%";
            products = products.Where(p =>
                EF.Functions.ILike(p.Code, pattern, LikeEscape) || EF.Functions.ILike(p.Name, pattern, LikeEscape));
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var items = await products
            .OrderBy(p => p.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Type,
                p.Unit,
                p.MinStock,
                p.IsActive,
                // Rule 1: on-hand is never stored, always the sum of the ledger.
                OnHand = db.StockMovements.Where(m => m.ProductId == p.Id).Sum(m => m.Quantity),
                Reserved = db.WorkOrderComponents
                    .Where(c => c.ComponentProductId == p.Id
                        && (c.WorkOrder!.Status == WorkOrderStatus.Planned || c.WorkOrder.Status == WorkOrderStatus.InProgress))
                    .Sum(c => c.RequiredQty - c.ConsumedQty),
            })
            .Select(p => new ProductListItem(
                p.Id, p.Code, p.Name, p.Type, p.Unit, p.MinStock, p.IsActive, p.OnHand, p.OnHand - p.Reserved))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListItem>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<ProductDetails> GetAsync(int id, CancellationToken cancellationToken = default) =>
        LoadDetailsAsync(id, cancellationToken);

    public async Task<ProductDetails> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Codes are compared and stored upper-case so "rm-oak" and "RM-OAK" cannot both exist.
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            throw new ValidationException("Code and name are required.");
        }

        if (!Enum.IsDefined(request.Type) || !Enum.IsDefined(request.Unit))
        {
            throw new ValidationException("Unknown product type or unit.");
        }

        ValidateMinStock(request.MinStock);

        // A friendly 409 for the common case; the unique index still catches a concurrent insert.
        if (await db.Products.AnyAsync(p => p.Code == code, cancellationToken))
        {
            throw new ConflictException($"A product with code '{code}' already exists.");
        }

        var product = new Product
        {
            Code = code,
            Name = name,
            Type = request.Type,
            Unit = request.Unit,
            MinStock = request.MinStock,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDetailsAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDetails> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new ValidationException("Name is required.");
        }

        ValidateMinStock(request.MinStock);

        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw ProductNotFound(id);

        product.Name = name;
        product.MinStock = request.MinStock;
        product.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDetailsAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<MaterialNeed>> ExplodeAsync(int id, decimal quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ValidationException("Quantity must be greater than zero.");
        }

        if (!await db.Products.AnyAsync(p => p.Id == id, cancellationToken))
        {
            throw ProductNotFound(id);
        }

        // The whole BOM table is small for a 10–50 person workshop, and one query beats one per level.
        var bomLines = await db.BomLines.AsNoTracking().ToListAsync(cancellationToken);
        var need = BomExplosion.Explode(id, quantity, bomLines);

        var leafIds = need.Keys.ToList();
        var leaves = await db.Products.AsNoTracking()
            .Where(p => leafIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Name, p.Unit })
            .ToListAsync(cancellationToken);

        return leaves
            .OrderBy(p => p.Code, StringComparer.Ordinal)
            .Select(p => new MaterialNeed(p.Id, p.Code, p.Name, p.Unit, need[p.Id]))
            .ToList();
    }

    internal static NotFoundException ProductNotFound(int id) => new($"Product {id} was not found.");

    private async Task<ProductDetails> LoadDetailsAsync(int id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Type,
                p.Unit,
                p.MinStock,
                p.IsActive,
                OnHand = db.StockMovements.Where(m => m.ProductId == p.Id).Sum(m => m.Quantity),
                Reserved = db.WorkOrderComponents
                    .Where(c => c.ComponentProductId == p.Id
                        && (c.WorkOrder!.Status == WorkOrderStatus.Planned || c.WorkOrder.Status == WorkOrderStatus.InProgress))
                    .Sum(c => c.RequiredQty - c.ConsumedQty),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw ProductNotFound(id);

        var bomLines = await db.BomLines.AsNoTracking()
            .Where(l => l.ParentProductId == id)
            .OrderBy(l => l.ComponentProduct!.Code)
            .Select(BomLineProjection.ToDto)
            .ToListAsync(cancellationToken);

        return new ProductDetails(
            product.Id,
            product.Code,
            product.Name,
            product.Type,
            product.Unit,
            product.MinStock,
            product.IsActive,
            product.OnHand,
            product.OnHand - product.Reserved,
            bomLines);
    }

    private static void ValidateMinStock(decimal minStock)
    {
        if (minStock < 0)
        {
            throw new ValidationException("Minimum stock cannot be negative.");
        }
    }

    /// <summary>Makes <c>%</c>, <c>_</c> and <c>\</c> in user input match literally, using <see cref="LikeEscape"/>.</summary>
    private static string EscapeLikePattern(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
