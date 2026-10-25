using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;

namespace OrderInventory.Application.Common;

/// <summary>Sequential document numbers such as SO-00005 and WO-00012.</summary>
internal static class DocumentNumbers
{
    public const string SalesOrderPrefix = "SO-";
    public const string WorkOrderPrefix = "WO-";

    /// <summary>
    /// Returns the next number after the highest existing one. Must run inside a transaction: it takes a
    /// transaction-scoped advisory lock per prefix, so two requests cannot read the same maximum and
    /// produce the same number. The lock is held until the caller commits the new document.
    /// (The unique index on Number would also catch a duplicate, but as an error instead of a number.)
    /// </summary>
    public static async Task<string> NextAsync(
        IAppDbContext db, string prefix, IQueryable<string> existingNumbers, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Document numbers must be taken inside a transaction.");
        }

        // One lock key per prefix, derived from its characters so it is stable and readable ("SO-" → 0x534F2D).
        var lockKey = prefix.Aggregate(0L, (key, c) => (key << 8) | c);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);

        // Zero-padded numbers sort as text, so the highest string is the highest number.
        var last = await existingNumbers.Where(n => n.StartsWith(prefix)).MaxAsync(n => (string?)n, cancellationToken);
        var next = last is null ? 1 : int.Parse(last[prefix.Length..], CultureInfo.InvariantCulture) + 1;
        return prefix + next.ToString("00000", CultureInfo.InvariantCulture);
    }
}
