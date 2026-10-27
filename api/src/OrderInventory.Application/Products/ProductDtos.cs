using System.ComponentModel.DataAnnotations;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

public sealed record ProductListQuery
{
    [Range(1, 100_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    /// <summary>Case-insensitive match on code or name.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    public ProductType? Type { get; init; }

    public bool IncludeInactive { get; init; }
}

/// <summary><see cref="Available"/> is on-hand minus what open work orders still reserve (rule 9).</summary>
public sealed record ProductListItem(
    int Id, string Code, string Name, ProductType Type, UnitOfMeasure Unit, decimal MinStock, bool IsActive, decimal OnHand,
    decimal Available);

public sealed record ProductDetails(
    int Id,
    string Code,
    string Name,
    ProductType Type,
    UnitOfMeasure Unit,
    decimal MinStock,
    bool IsActive,
    decimal OnHand,
    decimal Available,
    IReadOnlyList<BomLineDto> BomLines);

/// <summary>One direct BOM line of a product, with enough of the component to display it.</summary>
public sealed record BomLineDto(
    int Id,
    int ComponentProductId,
    string ComponentCode,
    string ComponentName,
    ProductType ComponentType,
    UnitOfMeasure ComponentUnit,
    decimal QtyPerUnit);

/// <summary>Total need of one leaf material for an exploded quantity (rule 7).</summary>
public sealed record MaterialNeed(int ProductId, string Code, string Name, UnitOfMeasure Unit, decimal Quantity);

/// <remarks>
/// Code, type and unit are fixed after creation: the stock ledger, BOM lines and orders are all
/// expressed in the product's unit and depend on its type, so changing them would silently corrupt history.
/// </remarks>
public sealed record CreateProductRequest
{
    [Required]
    [StringLength(32)]
    public required string Code { get; init; }

    [Required]
    [StringLength(200)]
    public required string Name { get; init; }

    public required ProductType Type { get; init; }

    public required UnitOfMeasure Unit { get; init; }

    [Range(typeof(decimal), "0", "99999999999999")]
    public decimal MinStock { get; init; }
}

public sealed record UpdateProductRequest
{
    [Required]
    [StringLength(200)]
    public required string Name { get; init; }

    [Range(typeof(decimal), "0", "99999999999999")]
    public decimal MinStock { get; init; }

    public required bool IsActive { get; init; }
}

public sealed record AddBomLineRequest
{
    public required int ComponentProductId { get; init; }

    /// <summary>Not range-validated here: <c>QtyPerUnit &gt; 0</c> is business rule 5 and answers with 422.</summary>
    public required decimal QtyPerUnit { get; init; }
}

public sealed record UpdateBomLineRequest
{
    public required decimal QtyPerUnit { get; init; }
}
