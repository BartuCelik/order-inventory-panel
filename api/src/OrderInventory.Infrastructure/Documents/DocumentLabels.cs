using System.Globalization;
using OrderInventory.Domain.Products;

namespace OrderInventory.Infrastructure.Documents;

/// <summary>English labels for PDFs and Excel files, worded as the screens word them in English.</summary>
internal static class DocumentLabels
{
    public static string Unit(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Piece => "pcs",
        UnitOfMeasure.Meter => "m",
        UnitOfMeasure.SquareMeter => "m²",
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Liter => "L",
        _ => unit.ToString(),
    };

    /// <summary>An enum value in sentence case: <c>InProgress</c> → "In progress", <c>RawMaterial</c> → "Raw material".</summary>
    public static string Of<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        if (value is ProductType.SemiFinished)
        {
            return "Semi-finished";
        }

        var name = value.ToString();
        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + char.ToLower(c, CultureInfo.InvariantCulture) : c.ToString()));
    }

    public static string YesNo(bool value) => value ? "Yes" : "No";
}
