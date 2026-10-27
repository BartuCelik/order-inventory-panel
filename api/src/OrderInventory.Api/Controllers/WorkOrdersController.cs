using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Api.Auth;
using OrderInventory.Application.Common;
using OrderInventory.Application.Production;
using OrderInventory.Infrastructure.Documents;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("work-orders")]
public sealed class WorkOrdersController(WorkOrderService workOrders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<WorkOrderSummaryDto>> List([FromQuery] WorkOrderListQuery query, CancellationToken cancellationToken) =>
        workOrders.ListAsync(query, cancellationToken);

    /// <summary>The list as an Excel file: the same filters, every matching row.</summary>
    [HttpGet("export")]
    public async Task<FileContentResult> Export(
        [FromQuery] WorkOrderListQuery query, [FromServices] ListWorkbooks workbooks, CancellationToken cancellationToken) =>
        File(ListWorkbooks.WorkOrders(await workOrders.ExportAsync(query, cancellationToken)),
            ListWorkbooks.ContentType, workbooks.FileName("work-orders"));

    [HttpGet("{id:int}")]
    public Task<WorkOrderDto> Get(int id, CancellationToken cancellationToken) =>
        workOrders.GetAsync(id, cancellationToken);

    [HttpGet("{id:int}/pdf")]
    public async Task<FileContentResult> Pdf(int id, [FromServices] WorkOrderPdf pdf, CancellationToken cancellationToken)
    {
        var workOrder = await workOrders.GetAsync(id, cancellationToken);
        return File(pdf.Render(workOrder), "application/pdf", $"{workOrder.Number}.pdf");
    }

    [Authorize(Roles = Roles.Planning)]
    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> Create(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await workOrders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = workOrder.Id }, workOrder);
    }

    [Authorize(Roles = Roles.Planning)]
    [HttpPost("{id:int}/start")]
    public Task<WorkOrderDto> Start(int id, CancellationToken cancellationToken) =>
        workOrders.StartAsync(id, cancellationToken);

    [Authorize(Roles = Roles.Warehouse)]
    [HttpPost("{id:int}/complete")]
    public Task<WorkOrderDto> Complete(int id, CancellationToken cancellationToken) =>
        workOrders.CompleteAsync(id, cancellationToken);

    [Authorize(Roles = Roles.Planning)]
    [HttpPost("{id:int}/cancel")]
    public Task<WorkOrderDto> Cancel(int id, CancellationToken cancellationToken) =>
        workOrders.CancelAsync(id, cancellationToken);
}
