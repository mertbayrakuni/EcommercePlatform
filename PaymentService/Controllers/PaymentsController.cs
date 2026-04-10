using Microsoft.AspNetCore.Mvc;
using PaymentService.Dtos;
using PaymentService.Services;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentProcessor _proc;
    public PaymentsController(IPaymentProcessor proc) => _proc = proc;

    [HttpPost("pay")]
    public async Task<ActionResult<PaymentResultDto>> Pay([FromBody] PaymentRequestDto req, CancellationToken ct)
    {
        if (req is null) return BadRequest();
        var res = await _proc.ProcessAsync(req, ct);
        if (!res.Succeeded) return UnprocessableEntity(new { error = res.ErrorMessage });
        return Ok(res);
    }
}
