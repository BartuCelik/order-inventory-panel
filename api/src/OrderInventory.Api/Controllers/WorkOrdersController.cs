using Microsoft.AspNetCore.Mvc;
using OrderInventory.Application.Common;
using OrderInventory.Application.Production;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("work-orders")]
public sealed class WorkOrdersController(WorkOrderService workOrders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<WorkOrderSummaryDto>> List([FromQuery] WorkOrderListQuery query, CancellationToken cancellationToken) =>
        workOrders.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<WorkOrderDto> Get(int id, CancellationToken cancellationToken) =>
        workOrders.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> Create(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await workOrders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = workOrder.Id }, workOrder);
    }

    [HttpPost("{id:int}/start")]
    public Task<WorkOrderDto> Start(int id, CancellationToken cancellationToken) =>
        workOrders.StartAsync(id, cancellationToken);

    [HttpPost("{id:int}/complete")]
    public Task<WorkOrderDto> Complete(int id, CancellationToken cancellationToken) =>
        workOrders.CompleteAsync(id, cancellationToken);

    [HttpPost("{id:int}/cancel")]
    public Task<WorkOrderDto> Cancel(int id, CancellationToken cancellationToken) =>
        workOrders.CancelAsync(id, cancellationToken);
}
