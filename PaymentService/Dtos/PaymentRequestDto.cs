namespace PaymentService.Dtos;

public sealed class PaymentRequestDto
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string? Method { get; set; }
    /// <summary>
    /// Stripe PaymentMethod token (e.g. pm_card_visa in test mode).
    /// When null the processor falls back to simulation.
    /// </summary>
    public string? PaymentMethodId { get; set; }
    /// <summary>Set to true to force a simulated failure (used when PaymentMethodId is null).</summary>
    public bool SimulateFailure { get; set; }
}
