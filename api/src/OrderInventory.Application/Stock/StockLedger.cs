using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;

namespace OrderInventory.Application.Stock;

/// <summary>A movement to write; the ledger adds who and when.</summary>
public sealed record NewStockMovement(
    int ProductId,
    decimal Quantity,
    StockMovementType Type,
    StockMovementRefType? RefType = null,
    int? RefId = null,
    string? Note = null);

/// <summary>
/// The only way stock changes (rules 2–4). Every caller — manual movements now, work order completion and
/// shipping later — writes through <see cref="PostAsync"/>.
/// </summary>
public sealed class StockLedger(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <summary>
    /// Writes <paramref name="movements"/> as one all-or-nothing operation, or throws
    /// <see cref="BusinessRuleException"/> without writing anything if any product would go below zero.
    /// </summary>
    /// <remarks>
    /// Joins the caller's transaction when there is one (e.g. completing a work order also updates the
    /// work order), so the caller's other changes are saved and committed together with the movements.
    /// </remarks>
    public async Task<IReadOnlyList<StockMovement>> PostAsync(IReadOnlyCollection<NewStockMovement> movements, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movements);
        if (movements.Count == 0)
        {
            throw new ArgumentException("At least one movement is required.", nameof(movements));
        }

        // A wrong sign is a programming error in the caller, not something the user can fix.
        var wrongSign = movements.FirstOrDefault(m => !StockRules.HasValidSign(m.Type, m.Quantity));
        if (wrongSign is not null)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "A {0} movement cannot have quantity {1}.", wrongSign.Type, wrongSign.Quantity),
                nameof(movements));
        }

        // Rule 4, step 1: one transaction for the check and the write.
        await using var ownTransaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        // Rule 4, step 2: lock the affected product rows before reading any stock. A second operation on
        // the same product blocks here until this one commits or rolls back, so "is there enough?" is
        // always answered from the ledger as it is after the previous writer finished. Reading on-hand
        // first and locking afterwards would let two operations both see the same stock.
        //
        // The rows are locked in product id order. Two operations needing {A, B} then always lock A
        // before B, so neither can hold B while waiting for A — the classic deadlock.
        //
        // Every stock-changing operation must go through this method; a writer that skipped the lock
        // would not be serialised against the others.
        var productIds = movements.Select(m => m.ProductId).Distinct().Order().ToArray();
        var products = await db.Products
            .FromSql($"SELECT * FROM products WHERE id = ANY({productIds}) ORDER BY id FOR UPDATE")
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException($"Product {string.Join(", ", missing)} does not exist.");
        }

        // Rule 4, step 3: the check, inside the lock. Read Committed takes a fresh snapshot for this
        // statement, so it includes everything committed by whoever held the lock before us.
        var onHand = await db.StockMovements
            .Where(m => productIds.Contains(m.ProductId))
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, OnHand = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.OnHand, cancellationToken);

        var createdBy = currentUser.Name;
        var createdAt = timeProvider.GetUtcNow().UtcDateTime;
        var entities = movements.Select(m => new StockMovement
        {
            ProductId = m.ProductId,
            Quantity = m.Quantity,
            Type = m.Type,
            RefType = m.RefType,
            RefId = m.RefId,
            Note = m.Note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
        }).ToList();

        // Rule 3: rejected as a whole. Nothing has been written yet; disposing the transaction rolls back
        // and releases the locks.
        var shortages = StockRules.FindShortages(onHand, entities);
        if (shortages.Count > 0)
        {
            throw new BusinessRuleException("Not enough stock. " + string.Join(" ", shortages.Select(s => Describe(s, products[s.ProductId]))));
        }

        // Rule 4, step 4: the write, still inside the lock.
        db.StockMovements.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);

        if (ownTransaction is not null)
        {
            await ownTransaction.CommitAsync(cancellationToken);
        }

        return entities;
    }

    private static string Describe(StockShortage shortage, Product product) => string.Format(
        CultureInfo.InvariantCulture,
        "{0}: {1:0.####} on hand, {2:0.####} needed, {3:0.####} short.",
        product.Code,
        shortage.OnHand,
        shortage.Requested,
        shortage.Missing);
}
