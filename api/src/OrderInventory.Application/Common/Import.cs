namespace OrderInventory.Application.Common;

/// <summary>
/// A spreadsheet as text: the header row and every non-empty row below it. Infrastructure reads the file;
/// all parsing and rules stay in the services, so they do not depend on the file format.
/// </summary>
public sealed record ImportSheet(IReadOnlyList<string> Headers, IReadOnlyList<SheetRow> Rows)
{
    /// <summary>The same limit as an export, so an exported file can always be imported back.</summary>
    public const int MaxRows = Export.MaxRows;

    /// <summary>Throws an <see cref="ImportException"/> when a required column is missing or the file is empty or too long.</summary>
    public void Require(params string[] columns)
    {
        var errors = columns
            .Where(c => !Headers.Contains(c, StringComparer.OrdinalIgnoreCase))
            .Select(c => new ImportError(null, $"Column '{c}' is missing. The first row must hold the column names."))
            .ToList();
        if (Rows.Count == 0)
        {
            errors.Add(new ImportError(null, "The file has no rows below the column names."));
        }
        else if (Rows.Count > MaxRows)
        {
            errors.Add(new ImportError(null, $"The file has {Rows.Count:N0} rows; an import takes at most {MaxRows:N0}. Split it into smaller files."));
        }

        if (errors.Count > 0)
        {
            throw new ImportException(errors);
        }
    }
}

/// <summary>One row by its spreadsheet row number, cells keyed by column name (case-insensitive).</summary>
public sealed record SheetRow(int Number, IReadOnlyDictionary<string, string> Cells)
{
    /// <summary>The trimmed cell text, or "" when the cell or the column is empty.</summary>
    public string this[string column] => Cells.TryGetValue(column, out var text) ? text.Trim() : "";
}

/// <summary>What an import wrote, or with <see cref="DryRun"/> would write.</summary>
public sealed record ImportResult(int Created, int Updated, int Unchanged, bool DryRun);

/// <summary>A problem in the file: <see cref="Row"/> is the spreadsheet row number, null for the file as a whole.</summary>
public sealed record ImportError(int? Row, string Message);
