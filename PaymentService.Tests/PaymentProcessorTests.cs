using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Dtos;
using PaymentService.Services;

namespace PaymentService.Tests;

public class PaymentProcessorTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly PaymentProcessor _sut;

    public PaymentProcessorTests()
    {
        var opts = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new PaymentDbContext(opts);
        _sut = new PaymentProcessor(_db);
    }

    // ── ProcessAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_Success_ReturnsTrueAndTransactionId()
    {
        var result = await _sut.ProcessAsync(new PaymentRequestDto { OrderId = 1, Amount = 50m, Method = "Card" });

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.TransactionId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessAsync_SimulateFailure_ReturnsFalseWithErrorMessage()
    {
        var result = await _sut.ProcessAsync(new PaymentRequestDto { OrderId = 1, Amount = 50m, SimulateFailure = true });

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task ProcessAsync_Success_PersistsPaymentToDb()
    {
        await _sut.ProcessAsync(new PaymentRequestDto { OrderId = 1, Amount = 99.99m, Method = "Card" });

        var payment = await _db.Payments.SingleAsync();
        Assert.Equal(1, payment.OrderId);
        Assert.Equal(99.99m, payment.Amount);
        Assert.True(payment.Succeeded);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateOrderId_ReturnsStoredResultWithoutInsert()
    {
        var first = await _sut.ProcessAsync(new PaymentRequestDto { OrderId = 1, Amount = 50m });

        // Same OrderId again — different SimulateFailure flag should be ignored
        var second = await _sut.ProcessAsync(new PaymentRequestDto { OrderId = 1, Amount = 50m, SimulateFailure = true });

        Assert.Equal(first.Succeeded, second.Succeeded);
        Assert.Equal(first.TransactionId, second.TransactionId);
        Assert.Equal(1, await _db.Payments.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_Failure_PersistsFailedPaymentToDb()
    {
        await _sut.ProcessAsync(new PaymentRequestDto { OrderId = 1, Amount = 25m, SimulateFailure = true });

        var payment = await _db.Payments.SingleAsync();
        Assert.False(payment.Succeeded);
        Assert.NotNull(payment.ErrorMessage);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
