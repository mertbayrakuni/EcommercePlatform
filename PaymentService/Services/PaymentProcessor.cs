using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Dtos;
using PaymentService.Models;

namespace PaymentService.Services;

/// <summary>
/// Simulates payment processing and persists the result.
/// Set <c>SimulateFailure = true</c> on the request to test failure paths.
/// </summary>
public sealed class PaymentProcessor : IPaymentProcessor
{
    private readonly PaymentDbContext _db;

    public PaymentProcessor(PaymentDbContext db) => _db = db;

    public async Task<PaymentResultDto> ProcessAsync(PaymentRequestDto req, CancellationToken ct = default)
    {
        var existing = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == req.OrderId && p.Succeeded, ct);
        if (existing is not null)
            return new PaymentResultDto
            {
                Succeeded = true,
                TransactionId = existing.TransactionId
            };

        var result = req.SimulateFailure
            ? new PaymentResultDto { Succeeded = false, ErrorMessage = "Simulated failure" }
            : new PaymentResultDto { Succeeded = true, TransactionId = Guid.NewGuid().ToString() };

        _db.Payments.Add(new Payment
        {
            OrderId = req.OrderId,
            Amount = req.Amount,
            Method = req.Method,
            TransactionId = result.TransactionId,
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            ProcessedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        return result;
    }
}

