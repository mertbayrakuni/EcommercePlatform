using OrderService.Common;
using OrderService.Dtos;

namespace OrderService.Services;

/// <summary>
/// Core order operations: creation, retrieval, payment and status transitions.
/// All state changes are validated through <see cref="OrderStateMachine"/>.
/// </summary>
public interface IOrderService
{
    Task<OrderResponseDto> CreateAsync(CreateOrderRequestDto req, CancellationToken ct = default);
    Task<OrderResponseDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<OrderResponseDto> CancelAsync(int id, CancellationToken ct = default);
    Task<OrderResponseDto> MarkPaidAsync(int id, CancellationToken ct = default);
    Task<OrderResponseDto> MarkShippedAsync(int id, CancellationToken ct = default);
    Task<OrderResponseDto> MarkDeliveredAsync(int id, CancellationToken ct = default);
    Task<PaymentResponseDto> PayAsync(PaymentRequestDto req, CancellationToken ct = default);

    Task<PagedResult<OrderResponseDto>> GetAllAsync(
        int page,
        int pageSize,
        string? email,
        CancellationToken ct = default);
}