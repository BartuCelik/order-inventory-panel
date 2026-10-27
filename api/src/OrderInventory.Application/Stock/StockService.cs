using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Stock;

namespace OrderInventory.Application.Stock;

/// <summary>Manual stock movements (receipt, issue, adjustment) and the ledger view.</summary>
public sealed class StockService(IAppDbContext db, StockLedger ledger)
{
    internal static readonly Expression<Func<StockMovement, StockMovementDto>> ToDto = m => new StockMovementDto(
        m.Id, m.ProductId, m.Product!.Code, m.Product.Name, m.Product.Unit, m.Type, m.Quantity,
        m.RefType, m.RefId, m.Note, m.CreatedBy, m.CreatedAt);

    public async Task<StockMovementDto> RecordAsync(RecordStockMovementRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var movement = (await ledger.PostAsync(
            [new NewStockMovement(request.ProductId, request.SignedQuantity, request.Type, Note: note)],
            cancellationToken)).Single();

        return await db.StockMovements
            .Where(m => m.Id == movement.Id)
            .Select(ToDto)
            .SingleAsync(cancellationToken);
    }

    /// <summary>Every row the list's filters match, for Excel export (see <see cref="Export"/>).</summary>
    public async Task<IReadOnlyList<StockMovementDto>> ExportAsync(StockMovementQuery query, CancellationToken cancellationToken) =>
        Export.Rows(await ListAsync(query with { Page = 1, PageSize = Export.MaxRows }, cancellationToken));

    public async Task<PagedResult<StockMovementDto>> ListAsync(StockMovementQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var movements = db.StockMovements.AsQueryable();
        if (query.ProductId is { } productId)
        {
            movements = movements.Where(m => m.ProductId == productId);
        }

        var totalCount = await movements.CountAsync(cancellationToken);
        var items = await movements
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>(items, query.Page, query.PageSize, totalCount);
    }
}
