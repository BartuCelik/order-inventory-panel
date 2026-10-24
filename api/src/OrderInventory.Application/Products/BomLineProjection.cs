using System.Linq.Expressions;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

/// <summary>One translatable projection, shared by every query that returns BOM lines.</summary>
internal static class BomLineProjection
{
    public static readonly Expression<Func<BomLine, BomLineDto>> ToDto = l => new BomLineDto(
        l.Id,
        l.ComponentProductId,
        l.ComponentProduct!.Code,
        l.ComponentProduct.Name,
        l.ComponentProduct.Type,
        l.ComponentProduct.Unit,
        l.QtyPerUnit);
}
