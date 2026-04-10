using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Dtos;
using PaymentService.Models;
using Stripe;

namespace PaymentService.Services;

/// <summary>
/// Processes payments via Stripe when a PaymentMethodId is supplied, or falls
/// back to simulation for local dev / tests when no token is provided.
/// </summary>
public sealed class PaymentProcessor : IPaymentProcessor
{
    private readonly PaymentDbContext _db;
    private readonly IStripeClient? _stripeClient;

    public PaymentProcessor(PaymentDbContext db, IStripeClient? stripeClient = null)
    {
        _db = db;
        _stripeClient = stripeClient;
    }

    public async Task<PaymentResultDto> ProcessAsync(PaymentRequestDto req, CancellationToken ct = default)
    {
        var existing = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == req.OrderId && p.Succeeded, ct);
        if (existing is not null)
            return new PaymentResultDto { Succeeded = true, TransactionId = existing.TransactionId };

        PaymentResultDto result;

        if (!string.IsNullOrWhiteSpace(req.PaymentMethodId) && _stripeClient is not null)
        {
            result = await ChargeViaStripeAsync(req, ct);
        }
        else
        {
            result = req.SimulateFailure
                ? new PaymentResultDto { Succeeded = false, ErrorMessage = "Simulated failure" }
                : new PaymentResultDto { Succeeded = true, TransactionId = Guid.NewGuid().ToString() };
        }

        _db.Payments.Add(new Payment
        {
            OrderId = req.OrderId,
            Amount = req.Amount,
            Method = req.Method,
            TransactionId = result.TransactionId ?? string.Empty,
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            ProcessedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<PaymentResultDto> ChargeViaStripeAsync(PaymentRequestDto req, CancellationToken ct)
    {
        var service = new PaymentIntentService(_stripeClient);
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(req.Amount * 100),
            Currency = (req.Currency ?? "usd").ToLower(),
            PaymentMethod = req.PaymentMethodId,
            PaymentMethodTypes = new List<string> { "card" },
            Confirm = true,
        };

        try
        {
            var intent = await service.CreateAsync(options, null, ct);
            return intent.Status == "succeeded"
                ? new PaymentResultDto { Succeeded = true, TransactionId = intent.Id }
                : new PaymentResultDto { Succeeded = false, ErrorMessage = $"Unexpected intent status: {intent.Status}" };
        }
        catch (StripeException ex)
        {
            return new PaymentResultDto
            {
                Succeeded = false,
                ErrorMessage = ex.StripeError?.Message ?? ex.Message
            };
        }
    }
}


