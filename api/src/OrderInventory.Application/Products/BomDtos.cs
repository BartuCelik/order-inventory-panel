using System.ComponentModel.DataAnnotations;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

/// <summary>One direct BOM line of a product, with the component's details for display.</summary>
public sealed record BomLineDto(
    int Id, int ComponentProductId, string ComponentCode, string ComponentName,
    ProductType ComponentType, UnitOfMeasure ComponentUnit, decimal QtyPerUnit);

public sealed record AddBomLineRequest(
    int ComponentProductId,
    [Range(QuantityLimits.MinPositive, QuantityLimits.Max)] decimal QtyPerUnit);

/// <summary>Total need of every leaf component for <see cref="Quantity"/> units of the product (rule 7).</summary>
public sealed record BomExplosionDto(int ProductId, decimal Quantity, IReadOnlyList<BomExplosionItemDto> Items);

public sealed record BomExplosionItemDto(
    int ProductId, string Code, string Name, ProductType Type, UnitOfMeasure Unit, decimal Quantity);
