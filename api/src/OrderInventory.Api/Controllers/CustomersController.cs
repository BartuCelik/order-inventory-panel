using Microsoft.AspNetCore.Mvc;
using OrderInventory.Application.Common;
using OrderInventory.Application.Sales;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("customers")]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerDto>> List([FromQuery] CustomerListQuery query, CancellationToken cancellationToken) =>
        customers.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<CustomerDto> Get(int id, CancellationToken cancellationToken) =>
        customers.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await customers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    public Task<CustomerDto> Update(int id, CustomerRequest request, CancellationToken cancellationToken) =>
        customers.UpdateAsync(id, request, cancellationToken);
}
