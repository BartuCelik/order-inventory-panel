using System.ComponentModel.DataAnnotations;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

/// <summary>A product with its on-hand stock, computed from the stock ledger (never stored).</summary>
public sealed record ProductDto(
    int Id, string Code, string Name, ProductType Type, UnitOfMeasure Unit, decimal MinStock, bool IsActive, decimal OnHand);

/// <summary>Body of POST and PUT. Lengths match the database columns.</summary>
public sealed record ProductRequest(
    [Required, StringLength(32)] string Code,
    [Required, StringLength(200)] string Name,
    ProductType Type,
    UnitOfMeasure Unit,
    [Range(0, double.MaxValue)] decimal MinStock,
    bool IsActive = true);

public sealed class ProductListQuery : PageQuery
{
    public ProductType? Type { get; init; }

    /// <summary>Case-insensitive match anywhere in the code or the name.</summary>
    [StringLength(100)]
    public string? Search { get; init; }
}
