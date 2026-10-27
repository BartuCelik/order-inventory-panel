using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Stock;

namespace OrderInventory.Application.Stock;

/// <summary>
/// Opening stock from a spreadsheet (Code, Quantity; other columns are ignored): one "Opening stock"
/// Adjustment per product, only for products whose ledger is still empty. That makes the import safe to
/// repeat — a second run is rejected instead of doubling the stock — and keeps it from overwriting history;
/// stock that has moved is corrected with an Adjustment instead. A quantity of 0 is skipped.
/// </summary>
public sealed class OpeningStockImportService(IAppDbContext db, StockLedger ledger)
{
    public const string Note = "Opening stock";

    public async Task<ImportResult> ImportAsync(ImportSheet sheet, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.Require("Code", "Quantity");

        var errors = new List<ImportError>();
        var rows = new List<(int Number, string Code, decimal Quantity)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in sheet.Rows)
        {
            var before = errors.Count;
            var code = row["Code"].ToUpperInvariant();
            if (code.Length == 0)
            {
                errors.Add(new ImportError(row.Number, "Code is required."));
            }
            else if (!seen.Add(code))
            {
                errors.Add(new ImportError(row.Number, $"Code {code} appears more than once in the file."));
            }

            var quantity = ImportCells.Number(row["Quantity"]);
            if (quantity is null)
            {
                errors.Add(new ImportError(row.Number, $"Quantity '{row["Quantity"]}' is not a number."));
            }
            else if (quantity < 0)
            {
                errors.Add(new ImportError(row.Number, "Quantity cannot be negative."));
            }

            if (errors.Count == before)
            {
                rows.Add((row.Number, code, quantity!.Value));
            }
        }

        var codes = rows.Select(r => r.Code).ToList();
        var productIds = await db.Products
            .Where(p => codes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Id, cancellationToken);
        errors.AddRange(rows
            .Where(r => !productIds.ContainsKey(r.Code))
            .Select(r => new ImportError(r.Number, $"No product has code {r.Code}.")));

        var lines = rows.Where(r => r.Quantity > 0 && productIds.ContainsKey(r.Code)).ToList();
        var ids = lines.Select(l => productIds[l.Code]).ToList();

        // Rule 4: the write locks the product rows, and the "ledger is empty" check runs inside that lock.
        // Two imports of the same file at once then queue on the lock; the second sees the first one's
        // movements and is rejected. A dry run, or a file that already failed, only reads.
        var write = !dryRun && errors.Count == 0 && lines.Count > 0;
        await using var transaction = write ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (write)
        {
            await ledger.LockAsync(ids, cancellationToken);
        }

        var withMovements = (await db.StockMovements
            .Where(m => ids.Contains(m.ProductId))
            .Select(m => m.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();
        errors.AddRange(lines
            .Where(l => withMovements.Contains(productIds[l.Code]))
            .Select(l => new ImportError(l.Number,
                $"{l.Code} already has stock movements. Opening stock is only for products with an empty ledger; record an adjustment instead.")));

        if (errors.Count > 0)
        {
            // Disposing the transaction rolls back and releases the locks.
            throw new ImportException(errors);
        }

        if (write)
        {
            // Joins this transaction, so the movements are written under the locks taken above.
            await ledger.PostAsync(
                lines.Select(l => new NewStockMovement(productIds[l.Code], l.Quantity, StockMovementType.Adjustment, Note: Note)).ToList(),
                cancellationToken);
            await transaction!.CommitAsync(cancellationToken);
        }

        // Every code exists by now, so the rest are the skipped zeros.
        return new ImportResult(lines.Count, 0, rows.Count - lines.Count, dryRun);
    }
}
