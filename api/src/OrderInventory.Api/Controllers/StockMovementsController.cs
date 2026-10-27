using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Api.Auth;
using OrderInventory.Application.Common;
using OrderInventory.Application.Stock;
using OrderInventory.Infrastructure.Documents;

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

    /// <summary>The list as an Excel file: the same filters, every matching row.</summary>
    [HttpGet("export")]
    public async Task<FileContentResult> Export(
        [FromQuery] StockMovementQuery query, [FromServices] ListWorkbooks workbooks, CancellationToken cancellationToken) =>
        File(ListWorkbooks.StockMovements(await stock.ExportAsync(query, cancellationToken)),
            ListWorkbooks.ContentType, workbooks.FileName("stock-movements"));

    [Authorize(Roles = Roles.Warehouse)]
    [HttpPost]
    public async Task<ActionResult<StockMovementDto>> Record(RecordStockMovementRequest request, CancellationToken cancellationToken)
    {
        var movement = await stock.RecordAsync(request, cancellationToken);
        return Created((string?)null, movement);
    }
}
