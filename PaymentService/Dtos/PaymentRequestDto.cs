namespace PaymentService.Dtos;

public sealed class PaymentRequestDto
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public bool SimulateFailure { get; set; }
}
