using System.Globalization;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Infrastructure.Documents;

/// <summary>English labels for PDFs and Excel files, worded as the screens word them in English.</summary>
internal static class DocumentLabels
{
    // The same words the import reads back, so an exported file imports unchanged.
    public static string Unit(UnitOfMeasure unit) => ImportCells.Label(unit);

    /// <summary>An enum value in sentence case: <c>InProgress</c> → "In progress", <c>RawMaterial</c> → "Raw material".</summary>
    public static string Of<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        if (value is ProductType type)
        {
            return ImportCells.Label(type);
        }

        var name = value.ToString();
        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + char.ToLower(c, CultureInfo.InvariantCulture) : c.ToString()));
    }

    public static string YesNo(bool value) => value ? "Yes" : "No";
}
