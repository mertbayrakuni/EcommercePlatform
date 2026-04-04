using CatalogService.Dtos;
using CatalogService.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryService _inv;
    public InventoryController(IInventoryService inv) => _inv = inv;

    [HttpPost("decrease")]
    public async Task<ActionResult<InventoryDecreaseResponseDto>> Decrease([FromBody] InventoryDecreaseRequestDto req, CancellationToken ct)
    {
        try
        {
            var result = await _inv.DecreaseStockAsync(req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Increase endpoint used by OrderService when an order is cancelled and
    // items must be returned to stock. Mirrors the validation and behavior
    // of the decrease endpoint but adds quantities instead.

    [HttpPost("increase")]
    public async Task<ActionResult<InventoryDecreaseResponseDto>> Increase([FromBody] InventoryDecreaseRequestDto req, CancellationToken ct)
    {
        try
        {
            var result = await _inv.IncreaseStockAsync(req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}