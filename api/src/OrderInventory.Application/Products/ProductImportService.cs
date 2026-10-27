using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Products;

/// <summary>
/// Creates and updates products from a spreadsheet with the export's columns (Code, Name, Type, Unit,
/// Minimum, Active; others such as On hand are ignored). A new code creates a product; an existing code
/// updates its name, minimum and active flag. The whole file is checked first and saved in one go, or
/// nothing is saved.
/// </summary>
public sealed class ProductImportService(IAppDbContext db)
{
    // Same limits as CreateProductRequest/UpdateProductRequest.
    private const int MaxCodeLength = 32;
    private const int MaxNameLength = 200;
    private const decimal MaxMinStock = 99_999_999_999_999m;

    public async Task<ImportResult> ImportAsync(ImportSheet sheet, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.Require("Code", "Name", "Type", "Unit");

        var errors = new List<ImportError>();
        var rows = new List<ProductRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in sheet.Rows)
        {
            if (Parse(row, seen, errors) is { } parsed)
            {
                rows.Add(parsed);
            }
        }

        var codes = rows.Select(r => r.Code).ToList();
        var existing = await db.Products.Where(p => codes.Contains(p.Code)).ToDictionaryAsync(p => p.Code, cancellationToken);

        // Code, type and unit are fixed after creation (see CreateProductRequest): the ledger, BOM lines and
        // orders depend on them.
        foreach (var row in rows.Where(r => existing.ContainsKey(r.Code)))
        {
            var product = existing[row.Code];
            if (product.Type != row.Type)
            {
                errors.Add(new ImportError(row.Number, $"The type of {row.Code} cannot change; it is {ImportCells.Label(product.Type)}."));
            }

            if (product.Unit != row.Unit)
            {
                errors.Add(new ImportError(row.Number, $"The unit of {row.Code} cannot change; it is {ImportCells.Label(product.Unit)}."));
            }
        }

        if (errors.Count > 0)
        {
            throw new ImportException(errors);
        }

        int created = 0, updated = 0, unchanged = 0;
        foreach (var row in rows)
        {
            if (!existing.TryGetValue(row.Code, out var product))
            {
                db.Products.Add(new Product
                {
                    Code = row.Code,
                    Name = row.Name,
                    Type = row.Type,
                    Unit = row.Unit,
                    MinStock = row.MinStock ?? 0m,
                    IsActive = row.IsActive ?? true,
                });
                created++;
                continue;
            }

            // A blank Minimum or Active keeps the current value.
            var minStock = row.MinStock ?? product.MinStock;
            var isActive = row.IsActive ?? product.IsActive;
            if (product.Name == row.Name && product.MinStock == minStock && product.IsActive == isActive)
            {
                unchanged++;
                continue;
            }

            product.Name = row.Name;
            product.MinStock = minStock;
            product.IsActive = isActive;
            updated++;
        }

        // One SaveChanges is one transaction: every row or none. A code created concurrently by someone else
        // still hits the unique index and answers 409.
        if (!dryRun)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ImportResult(created, updated, unchanged, dryRun);
    }

    private static ProductRow? Parse(SheetRow row, HashSet<string> seen, List<ImportError> errors)
    {
        var before = errors.Count;
        void Error(string message) => errors.Add(new ImportError(row.Number, message));

        // Upper-case, as ProductService stores codes.
        var code = row["Code"].ToUpperInvariant();
        if (code.Length == 0)
        {
            Error("Code is required.");
        }
        else if (code.Length > MaxCodeLength)
        {
            Error($"Code {code} is longer than {MaxCodeLength} characters.");
        }
        else if (!seen.Add(code))
        {
            Error($"Code {code} appears more than once in the file.");
        }

        var name = row["Name"];
        if (name.Length == 0)
        {
            Error("Name is required.");
        }
        else if (name.Length > MaxNameLength)
        {
            Error($"Name is longer than {MaxNameLength} characters.");
        }

        var type = ImportCells.ProductType(row["Type"]);
        if (type is null)
        {
            Error($"Type '{row["Type"]}' is not one of Raw material, Semi-finished, Finished.");
        }

        var unit = ImportCells.Unit(row["Unit"]);
        if (unit is null)
        {
            Error($"Unit '{row["Unit"]}' is not one of pcs, m, m², kg, L.");
        }

        decimal? minStock = null;
        if (row["Minimum"].Length > 0)
        {
            minStock = ImportCells.Number(row["Minimum"]);
            if (minStock is null)
            {
                Error($"Minimum '{row["Minimum"]}' is not a number.");
            }
            else if (minStock < 0)
            {
                Error("Minimum cannot be negative.");
            }
            else if (minStock > MaxMinStock)
            {
                Error("Minimum is too large.");
            }
        }

        bool? isActive = null;
        if (row["Active"].Length > 0)
        {
            isActive = ImportCells.YesNo(row["Active"]);
            if (isActive is null)
            {
                Error($"Active '{row["Active"]}' must be Yes or No.");
            }
        }

        return errors.Count == before
            ? new ProductRow(row.Number, code, name, type!.Value, unit!.Value, minStock, isActive)
            : null;
    }

    private sealed record ProductRow(
        int Number, string Code, string Name, ProductType Type, UnitOfMeasure Unit, decimal? MinStock, bool? IsActive);
}
