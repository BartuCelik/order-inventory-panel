using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;

namespace OrderInventory.Application.Products;

// Signatures only, so the tests compile and fail; the suppressions go away with the implementation.
#pragma warning disable CS9113, CA1822

/// <summary>Product use cases: list, read, create, update and BOM explosion.</summary>
public sealed class ProductService(IAppDbContext db)
{
    public Task<PagedResult<ProductListItem>> ListAsync(ProductListQuery query, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<ProductDetails> GetAsync(int id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<ProductDetails> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<ProductDetails> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<MaterialNeed>> ExplodeAsync(int id, decimal quantity, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
