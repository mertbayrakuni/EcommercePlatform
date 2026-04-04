namespace OrderService.Dtos;

public sealed class PaymentRequestDto
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    // simple simulation: method can be used to trigger failures (e.g. "fail")
    public string? Method { get; set; }
    public bool SimulateFailure { get; set; }
}

public sealed class PaymentResponseDto
{
    public bool Succeeded { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
