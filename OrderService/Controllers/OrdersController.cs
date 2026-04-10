using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Common;
using OrderService.Dtos;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Create([FromBody] CreateOrderRequestDto req, CancellationToken ct)
    {
        try
        {
            var created = await _orders.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Note: controller keeps thin - all domain logic resides in OrderService.

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponseDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? email = null,
        CancellationToken ct = default)
    {
        var result = await _orders.GetAllAsync(page, pageSize, email, ct);
        return Ok(result);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderResponseDto>> Cancel(int id, CancellationToken ct)
    {
        try
        {
            var updated = await _orders.CancelAsync(id, ct);
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/paid")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<OrderResponseDto>> MarkPaid(int id, CancellationToken ct)
    {
        try
        {
            var updated = await _orders.MarkPaidAsync(id, ct);
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/ship")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<OrderResponseDto>> MarkShipped(int id, CancellationToken ct)
    {
        try
        {
            var updated = await _orders.MarkShippedAsync(id, ct);
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/deliver")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<OrderResponseDto>> MarkDelivered(int id, CancellationToken ct)
    {
        try
        {
            var updated = await _orders.MarkDeliveredAsync(id, ct);
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Thin controller - keep orchestration logic in services for testability.

    [HttpPost("pay")]
    public async Task<ActionResult<PaymentResponseDto>> Pay([FromBody] PaymentRequestDto req, CancellationToken ct)
    {
        try
        {
            var resp = await _orders.PayAsync(req, ct);
            return Ok(resp);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}