using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Api.Auth;
using OrderInventory.Application.Common;
using OrderInventory.Application.Products;

namespace OrderInventory.Api.Controllers;

/// <summary>
/// Products and their bills of materials. Errors come back as ProblemDetails:
/// 400 invalid input, 404 unknown product or line, 409 duplicate code,
/// 422 business rule (with a <c>code</c> such as <c>WouldCreateCycle</c>).
/// </summary>
/// <remarks>
/// Not yet authorised: JWT and roles arrive in Phase 3, when product and BOM edits will be
/// limited to Admin and Planner, with Warehouse read-only.
/// </remarks>
[ApiController]
[Route("products")]
public sealed class ProductsController(ProductService products, BomService bom) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductListItem>> List([FromQuery] ProductListQuery query, CancellationToken cancellationToken) =>
        products.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ProductDetails> Get(int id, CancellationToken cancellationToken) =>
        products.GetAsync(id, cancellationToken);

    [Authorize(Roles = Roles.Planning)]
    [HttpPost]
    public async Task<ActionResult<ProductDetails>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var created = await products.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [Authorize(Roles = Roles.Planning)]
    [HttpPut("{id:int}")]
    public Task<ProductDetails> Update(int id, UpdateProductRequest request, CancellationToken cancellationToken) =>
        products.UpdateAsync(id, request, cancellationToken);

    /// <summary>Total leaf-material need for <paramref name="quantity"/> units, through every BOM level (rule 7).</summary>
    [HttpGet("{id:int}/explosion")]
    public Task<IReadOnlyList<MaterialNeed>> Explode(int id, [FromQuery] decimal quantity = 1, CancellationToken cancellationToken = default) =>
        products.ExplodeAsync(id, quantity, cancellationToken);

    [Authorize(Roles = Roles.Planning)]
    [HttpPost("{id:int}/bom-lines")]
    public async Task<ActionResult<BomLineDto>> AddBomLine(int id, AddBomLineRequest request, CancellationToken cancellationToken)
    {
        var line = await bom.AddLineAsync(id, request, cancellationToken);
        // Lines have no endpoint of their own; they are read as part of the product.
        return CreatedAtAction(nameof(Get), new { id }, line);
    }

    /// <summary>Changes only the quantity. To use a different component, delete the line and add a new one.</summary>
    [Authorize(Roles = Roles.Planning)]
    [HttpPut("{id:int}/bom-lines/{lineId:int}")]
    public Task<BomLineDto> UpdateBomLine(int id, int lineId, UpdateBomLineRequest request, CancellationToken cancellationToken) =>
        bom.UpdateLineAsync(id, lineId, request, cancellationToken);

    [Authorize(Roles = Roles.Planning)]
    [HttpDelete("{id:int}/bom-lines/{lineId:int}")]
    public async Task<IActionResult> DeleteBomLine(int id, int lineId, CancellationToken cancellationToken)
    {
        await bom.DeleteLineAsync(id, lineId, cancellationToken);
        return NoContent();
    }
}
