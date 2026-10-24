using Microsoft.AspNetCore.Mvc;
using OrderInventory.Application.Common;
using OrderInventory.Application.Products;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> List([FromQuery] ProductListQuery query, CancellationToken cancellationToken) =>
        products.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ProductDto> Get(int id, CancellationToken cancellationToken) =>
        products.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public Task<ProductDto> Update(int id, ProductRequest request, CancellationToken cancellationToken) =>
        products.UpdateAsync(id, request, cancellationToken);
}
