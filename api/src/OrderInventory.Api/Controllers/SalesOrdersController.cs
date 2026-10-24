using Microsoft.AspNetCore.Mvc;
using OrderInventory.Application.Common;
using OrderInventory.Application.Sales;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("sales-orders")]
public sealed class SalesOrdersController(SalesOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SalesOrderSummaryDto>> List([FromQuery] SalesOrderListQuery query, CancellationToken cancellationToken) =>
        orders.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<SalesOrderDto> Get(int id, CancellationToken cancellationToken) =>
        orders.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<SalesOrderDto>> Create(CreateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPost("{id:int}/ship")]
    public Task<SalesOrderDto> Ship(int id, CancellationToken cancellationToken) =>
        orders.ShipAsync(id, cancellationToken);

    [HttpPost("{id:int}/cancel")]
    public Task<SalesOrderDto> Cancel(int id, CancellationToken cancellationToken) =>
        orders.CancelAsync(id, cancellationToken);
}
