using PaymentService.Dtos;

namespace PaymentService.Services;

public interface IPaymentProcessor
{
    Task<PaymentResultDto> ProcessAsync(PaymentRequestDto req);
}
