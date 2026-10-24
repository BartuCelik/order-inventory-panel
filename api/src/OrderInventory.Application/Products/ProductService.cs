using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

public sealed class ProductService(IAppDbContext db)
{
    public async Task<PagedResult<ProductDto>> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var products = db.Products.AsQueryable();
        if (query.Type is { } type)
        {
            products = products.Where(p => p.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // This runs as SQL (upper(...) and strpos), not in .NET, so the culture/StringComparison
            // overloads the analyzers ask for would not translate.
            var term = query.Search.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            products = products.Where(p => p.Code.ToUpper().Contains(term) || p.Name.ToUpper().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var items = await ToDto(products.OrderBy(p => p.Code).Skip(query.Skip).Take(query.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ProductDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await ToDto(db.Products.Where(p => p.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound(id);

    public async Task<ProductDto> CreateAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = request.Code.Trim();
        await EnsureCodeIsFreeAsync(code, exceptId: null, cancellationToken);

        var product = new Product { Code = code, Name = request.Name.Trim() };
        Apply(request, product);
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        // A new product has no movements yet.
        return ToDto(product, onHand: 0m);
    }

    public async Task<ProductDto> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken) ?? throw NotFound(id);
        var code = request.Code.Trim();
        await EnsureCodeIsFreeAsync(code, exceptId: id, cancellationToken);

        // Rule 5 seen from the other side: a raw material cannot have BOM lines, so a product that has some cannot become one.
        if (request.Type == ProductType.RawMaterial && product.Type != ProductType.RawMaterial
            && await db.BomLines.AnyAsync(l => l.ParentProductId == id, cancellationToken))
        {
            throw new BusinessRuleException(
                $"Product '{product.Code}' has bill of materials lines. Remove them before changing its type to raw material.");
        }

        product.Code = code;
        product.Name = request.Name.Trim();
        Apply(request, product);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private async Task EnsureCodeIsFreeAsync(string code, int? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(p => p.Code == code && p.Id != exceptId, cancellationToken))
        {
            throw new ConflictException($"A product with code '{code}' already exists.");
        }
    }

    private static void Apply(ProductRequest request, Product product)
    {
        product.Type = request.Type;
        product.Unit = request.Unit;
        product.MinStock = request.MinStock;
        product.IsActive = request.IsActive;
    }

    // On-hand is a correlated SUM over the ledger (rule 1); EF Core wraps it in COALESCE, so no movements gives 0.
    private IQueryable<ProductDto> ToDto(IQueryable<Product> products) =>
        products.Select(p => new ProductDto(
            p.Id, p.Code, p.Name, p.Type, p.Unit, p.MinStock, p.IsActive,
            db.StockMovements.Where(m => m.ProductId == p.Id).Sum(m => m.Quantity)));

    private static ProductDto ToDto(Product p, decimal onHand) =>
        new(p.Id, p.Code, p.Name, p.Type, p.Unit, p.MinStock, p.IsActive, onHand);

    private static NotFoundException NotFound(int id) => new($"Product {id} was not found.");
}
