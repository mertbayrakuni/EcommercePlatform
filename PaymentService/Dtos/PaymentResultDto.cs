namespace PaymentService.Dtos;

public sealed class PaymentResultDto
{
    public bool Succeeded { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
