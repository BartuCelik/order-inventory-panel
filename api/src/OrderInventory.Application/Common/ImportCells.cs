using System.Globalization;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Common;

/// <summary>
/// Reads imported cell text. Accepts the labels the Excel export writes ("Raw material", "m²", "Yes") as
/// well as the enum names, so an exported file imports back. Null means the text is not a valid value.
/// </summary>
public static class ImportCells
{
    private static readonly Dictionary<string, Domain.Products.ProductType> Types = new(StringComparer.Ordinal)
    {
        ["rawmaterial"] = Domain.Products.ProductType.RawMaterial,
        ["semifinished"] = Domain.Products.ProductType.SemiFinished,
        ["finished"] = Domain.Products.ProductType.Finished,
    };

    private static readonly Dictionary<string, UnitOfMeasure> Units = new(StringComparer.Ordinal)
    {
        ["pcs"] = UnitOfMeasure.Piece,
        ["piece"] = UnitOfMeasure.Piece,
        ["pieces"] = UnitOfMeasure.Piece,
        ["m"] = UnitOfMeasure.Meter,
        ["meter"] = UnitOfMeasure.Meter,
        ["metre"] = UnitOfMeasure.Meter,
        ["m²"] = UnitOfMeasure.SquareMeter,
        ["m2"] = UnitOfMeasure.SquareMeter,
        ["squaremeter"] = UnitOfMeasure.SquareMeter,
        ["squaremetre"] = UnitOfMeasure.SquareMeter,
        ["kg"] = UnitOfMeasure.Kilogram,
        ["kilogram"] = UnitOfMeasure.Kilogram,
        ["l"] = UnitOfMeasure.Liter,
        ["liter"] = UnitOfMeasure.Liter,
        ["litre"] = UnitOfMeasure.Liter,
    };

    public static Domain.Products.ProductType? ProductType(string text) => Types.TryGetValue(Key(text), out var type) ? type : null;

    public static UnitOfMeasure? Unit(string text) => Units.TryGetValue(Key(text), out var unit) ? unit : null;

    /// <summary>
    /// A plain invariant number ("12.5"). The reader writes numeric cells this way; a comma is rejected
    /// rather than guessed, because "1,500" means 1.5 in some languages and 1500 in others.
    /// </summary>
    public static decimal? Number(string text) =>
        decimal.TryParse(text.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    public static bool? YesNo(string text) => Key(text) switch
    {
        "yes" or "true" or "1" => true,
        "no" or "false" or "0" => false,
        _ => null,
    };

    /// <summary>Lower case without spaces or hyphens: "Semi-finished" and "SemiFinished" become "semifinished".</summary>
    private static string Key(string text) =>
        string.Concat(text.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToLowerInvariant();
}
