using System.ComponentModel.DataAnnotations;

namespace OrderInventory.Application.Common;

/// <summary>
/// Excel export of a list: the list's own query with paging replaced by one page of <see cref="MaxRows"/>,
/// so the file always holds exactly what the screen's filters match.
/// </summary>
public static class Export
{
    public const int MaxRows = 10_000;

    /// <summary>The rows, or a 400 when more match than fit: a cut-off file would look complete.</summary>
    public static IReadOnlyList<T> Rows<T>(PagedResult<T> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.TotalCount <= MaxRows
            ? page.Items
            : throw new ValidationException(
                $"{page.TotalCount:N0} rows match; an export holds at most {MaxRows:N0}. Narrow the filters and export again.");
    }
}
