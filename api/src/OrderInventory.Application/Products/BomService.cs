using OrderInventory.Application.Abstractions;

namespace OrderInventory.Application.Products;

// Signatures only, so the tests compile and fail; the suppressions go away with the implementation.
#pragma warning disable CS9113, CA1822

/// <summary>Editing a product's bill of materials (rules 5 and 6).</summary>
public sealed class BomService(IAppDbContext db)
{
    public Task<BomLineDto> AddLineAsync(int productId, AddBomLineRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<BomLineDto> UpdateLineAsync(
        int productId, int lineId, UpdateBomLineRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task DeleteLineAsync(int productId, int lineId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
