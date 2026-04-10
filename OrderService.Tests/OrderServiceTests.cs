using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderService.Common;
using OrderService.Data;
using OrderService.Infrastructure;
using OrderService.Models;

namespace OrderService.Tests;

public class OrderServiceTests : IDisposable
{
    private readonly OrderDbContext _db;
    private readonly Mock<IRabbitPublisher> _publisher;
    private readonly Mock<IHttpClientFactory> _httpClientFactory;
    private readonly Services.OrderService _sut;

    public OrderServiceTests()
    {
        var opts = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OrderDbContext(opts);

        _publisher = new Mock<IRabbitPublisher>();
        _publisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<It.IsAnyType>()))
            .Returns(Task.CompletedTask);

        _httpClientFactory = new Mock<IHttpClientFactory>();

        _sut = new Services.OrderService(
            _db,
            _httpClientFactory.Object,
            _publisher.Object,
            Mock.Of<ILogger<Services.OrderService>>());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<Order> SeedOrderAsync(OrderStatus status = OrderStatus.Pending, decimal total = 100m)
    {
        var order = new Order
        {
            CustomerEmail = "test@example.com",
            Status = status,
            TotalAmount = total,
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = 1,
                    ProductSku = "SKU-001",
                    ProductName = "Test Product",
                    UnitPrice = total,
                    Quantity = 1,
                    LineTotal = total
                }
            }
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    // ── CancelAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_OrderNotFound_Throws()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CancelAsync(999));
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_IsIdempotent()
    {
        var order = await SeedOrderAsync(OrderStatus.Cancelled);

        var result = await _sut.CancelAsync(order.Id);

        Assert.Equal(OrderStatus.Cancelled.ToString(), result.Status);
        _publisher.Verify(p => p.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<It.IsAnyType>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_InvalidTransition_Throws()
    {
        var order = await SeedOrderAsync(OrderStatus.Delivered);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CancelAsync(order.Id));
    }

    [Fact]
    public async Task CancelAsync_ValidPendingOrder_UpdatesStatusAndPublishesEvent()
    {
        var order = await SeedOrderAsync(OrderStatus.Pending);

        var result = await _sut.CancelAsync(order.Id);

        Assert.Equal(OrderStatus.Cancelled.ToString(), result.Status);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<string>(s => s == "orders"),
            It.Is<string>(s => s == "order.cancelled"),
            It.IsAny<It.IsAnyType>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ValidPaidOrder_UpdatesStatusAndPublishesEvent()
    {
        var order = await SeedOrderAsync(OrderStatus.Paid);

        var result = await _sut.CancelAsync(order.Id);

        Assert.Equal(OrderStatus.Cancelled.ToString(), result.Status);
    }

    // ── MarkPaidAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkPaidAsync_ValidTransition_UpdatesStatus()
    {
        var order = await SeedOrderAsync(OrderStatus.Pending);

        var result = await _sut.MarkPaidAsync(order.Id);

        Assert.Equal(OrderStatus.Paid.ToString(), result.Status);
    }

    [Fact]
    public async Task MarkPaidAsync_InvalidTransition_Throws()
    {
        var order = await SeedOrderAsync(OrderStatus.Shipped);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.MarkPaidAsync(order.Id));
    }

    // ── MarkShippedAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task MarkShippedAsync_ValidTransition_UpdatesStatus()
    {
        var order = await SeedOrderAsync(OrderStatus.Paid);

        var result = await _sut.MarkShippedAsync(order.Id);

        Assert.Equal(OrderStatus.Shipped.ToString(), result.Status);
    }

    [Fact]
    public async Task MarkShippedAsync_InvalidTransition_Throws()
    {
        var order = await SeedOrderAsync(OrderStatus.Pending);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.MarkShippedAsync(order.Id));
    }

    // ── MarkDeliveredAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkDeliveredAsync_ValidTransition_UpdatesStatus()
    {
        var order = await SeedOrderAsync(OrderStatus.Shipped);

        var result = await _sut.MarkDeliveredAsync(order.Id);

        Assert.Equal(OrderStatus.Delivered.ToString(), result.Status);
    }

    [Fact]
    public async Task MarkDeliveredAsync_InvalidTransition_Throws()
    {
        var order = await SeedOrderAsync(OrderStatus.Pending);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.MarkDeliveredAsync(order.Id));
    }

    // ── PayAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PayAsync_OrderNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.PayAsync(new Dtos.PaymentRequestDto { OrderId = 999, Amount = 50m }));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PayAsync_AmountMismatch_Throws()
    {
        var order = await SeedOrderAsync(OrderStatus.Pending, total: 100m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.PayAsync(new Dtos.PaymentRequestDto { OrderId = order.Id, Amount = 99m }));

        Assert.Contains("amount", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PayAsync_InvalidTransition_Throws()
    {
        var order = await SeedOrderAsync(OrderStatus.Delivered, total: 100m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.PayAsync(new Dtos.PaymentRequestDto { OrderId = order.Id, Amount = 100m }));

        Assert.Contains("Cannot pay", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _db.Dispose();
}
