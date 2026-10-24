using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrderInventory.Application.Common;
using OrderInventory.Application.Products;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/bom")]
public sealed class BomController(BomService bom) : ControllerBase
{
    // Upper bound keeps "N units" × QtyPerUnit through several levels well inside decimal's range.
    private const double MaxExplosionQuantity = 1_000_000;

    [HttpGet]
    public Task<IReadOnlyList<BomLineDto>> GetLines(int productId, CancellationToken cancellationToken) =>
        bom.GetLinesAsync(productId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<BomLineDto>> AddLine(int productId, AddBomLineRequest request, CancellationToken cancellationToken)
    {
        var line = await bom.AddLineAsync(productId, request, cancellationToken);
        return CreatedAtAction(nameof(GetLines), new { productId }, line);
    }

    [HttpDelete("{lineId:int}")]
    public async Task<IActionResult> DeleteLine(int productId, int lineId, CancellationToken cancellationToken)
    {
        await bom.DeleteLineAsync(productId, lineId, cancellationToken);
        return NoContent();
    }

    [HttpGet("explosion")]
    public Task<BomExplosionDto> Explode(
        int productId,
        [FromQuery, BindRequired, Range(QuantityLimits.MinPositive, MaxExplosionQuantity)] decimal qty,
        CancellationToken cancellationToken) =>
        bom.ExplodeAsync(productId, qty, cancellationToken);
}
