using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Dtos;
using PaymentService.Models;

namespace PaymentService.Services;

public sealed class PaymentProcessor : IPaymentProcessor
{
    private readonly PaymentDbContext _db;

    public PaymentProcessor(PaymentDbContext db) => _db = db;

    public async Task<PaymentResultDto> ProcessAsync(PaymentRequestDto req)
    {
        // Idempotency — return the stored result if this order was already processed
        var existing = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == req.OrderId);
        if (existing is not null)
            return new PaymentResultDto
            {
                Succeeded = existing.Succeeded,
                TransactionId = existing.TransactionId,
                ErrorMessage = existing.ErrorMessage
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

        await _db.SaveChangesAsync();

        return result;
    }
}

