using Microsoft.AspNetCore.Mvc;
using OrderInventory.Application.Common;
using OrderInventory.Application.Stock;

namespace OrderInventory.Api.Controllers;

/// <summary>
/// The stock ledger. Append-only (rule 2): there is no PUT, PATCH or DELETE; a mistake is fixed with an Adjustment.
/// </summary>
[ApiController]
[Route("stock/movements")]
public sealed class StockMovementsController(StockService stock) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<StockMovementDto>> List([FromQuery] StockMovementQuery query, CancellationToken cancellationToken) =>
        stock.ListAsync(query, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<StockMovementDto>> Record(RecordStockMovementRequest request, CancellationToken cancellationToken)
    {
        var movement = await stock.RecordAsync(request, cancellationToken);
        return Created((string?)null, movement);
    }
}
