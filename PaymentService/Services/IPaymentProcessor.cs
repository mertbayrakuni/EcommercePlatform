using PaymentService.Dtos;

namespace PaymentService.Services;

/// <summary>
/// Processes a payment for an order and persists the result.
/// </summary>
public interface IPaymentProcessor
{
    /// <summary>
    /// Processes the payment request. Idempotent — a second call for the same
    /// order that already succeeded returns the existing transaction immediately.
    /// </summary>
    Task<PaymentResultDto> ProcessAsync(PaymentRequestDto req, CancellationToken ct = default);
}
