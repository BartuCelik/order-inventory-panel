using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Api.Auth;
using OrderInventory.Application.Common;
using OrderInventory.Application.Sales;
using OrderInventory.Infrastructure.Documents;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("sales-orders")]
public sealed class SalesOrdersController(SalesOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SalesOrderSummaryDto>> List([FromQuery] SalesOrderListQuery query, CancellationToken cancellationToken) =>
        orders.ListAsync(query, cancellationToken);

    /// <summary>The list as an Excel file: the same filters, every matching row.</summary>
    [HttpGet("export")]
    public async Task<FileContentResult> Export(
        [FromQuery] SalesOrderListQuery query, [FromServices] ListWorkbooks workbooks, CancellationToken cancellationToken) =>
        File(ListWorkbooks.SalesOrders(await orders.ExportAsync(query, cancellationToken)),
            ListWorkbooks.ContentType, workbooks.FileName("sales-orders"));

    [HttpGet("{id:int}")]
    public Task<SalesOrderDto> Get(int id, CancellationToken cancellationToken) =>
        orders.GetAsync(id, cancellationToken);

    [Authorize(Roles = Roles.Planning)]
    [HttpPost]
    public async Task<ActionResult<SalesOrderDto>> Create(CreateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [Authorize(Roles = Roles.Warehouse)]
    [HttpPost("{id:int}/ship")]
    public Task<SalesOrderDto> Ship(int id, CancellationToken cancellationToken) =>
        orders.ShipAsync(id, cancellationToken);

    [Authorize(Roles = Roles.Planning)]
    [HttpPost("{id:int}/cancel")]
    public Task<SalesOrderDto> Cancel(int id, CancellationToken cancellationToken) =>
        orders.CancelAsync(id, cancellationToken);
}
