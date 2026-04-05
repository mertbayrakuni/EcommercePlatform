using Microsoft.EntityFrameworkCore;
using OrderService.Common;
using OrderService.Data;
using OrderService.Dtos;
using OrderService.Infrastructure;
using OrderService.Mappings;
using OrderService.Models;
using System.Net.Http.Json;

namespace OrderService.Services;

/// <summary>
/// Application service that encapsulates order-related business logic.
/// - Creates orders by validating request data, checking product availability
///   with CatalogService, decreasing inventory and storing the order.
/// - Exposes state transition operations (Paid / Shipped / Delivered / Cancel)
///   that use a central state machine to validate transitions.
/// - Communicates with CatalogService using an IHttpClientFactory named client.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly OrderDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRabbitPublisher _publisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderDbContext db, IHttpClientFactory httpClientFactory, IRabbitPublisher publisher, ILogger<OrderService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _publisher = publisher;
        _logger = logger;
    }

    // Simple payment simulation. This is synchronous and meant for demo/poc only.
    // It validates the order exists and the amount matches TotalAmount and then
    // marks the order as Paid when simulation succeeds. SimulateFailure can be
    // used to test failure handling.
    public async Task<PaymentResponseDto> PayAsync(PaymentRequestDto req, CancellationToken ct = default)
    {
        if (req is null) throw new InvalidOperationException("Payment request is required.");

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == req.OrderId, ct);

        if (order is null)
            throw new InvalidOperationException("Order not found.");

        if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.Paid))
            throw new InvalidOperationException($"Cannot pay an order in status '{order.Status}'. Allowed: {OrderStateMachine.AllowedTargetsText(order.Status)}");

        if (req.Amount != order.TotalAmount)
            throw new InvalidOperationException("Payment amount does not match order total.");

        var client = _httpClientFactory.CreateClient("Payment");

        // call PaymentService endpoint with retry/circuit-breaker (Polly configured)
        var payReq = new PaymentRequestDto
        {
            OrderId = req.OrderId,
            Amount = req.Amount,
            Method = req.Method,
            SimulateFailure = req.SimulateFailure
        };

        var payResp = await client.PostAsJsonAsync("/api/Payments/pay", payReq, ct);
        if (!payResp.IsSuccessStatusCode)
        {
            var msg = await payResp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Payment service failed: {msg}");
        }

        var pr = await payResp.Content.ReadFromJsonAsync<PaymentResponseDto>(cancellationToken: ct);
        if (pr is null || !pr.Succeeded)
            throw new InvalidOperationException($"Payment did not succeed. {pr?.ErrorMessage}");

        // mark paid after successful external payment
        order.Status = OrderStatus.Paid;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // publish OrderPaid event
        try
        {
            await _publisher.PublishAsync("orders", "order.paid", new Events.OrderEvents.OrderPaid(order.Id, pr.TransactionId));
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to publish order.paid event for order {OrderId}", order.Id); }

        return pr;
    }

        // small helper to reduce duplication for status transitions
        private async Task<OrderResponseDto> TransitionAsync(int id, OrderStatus to, CancellationToken ct)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (order is null)
                throw new InvalidOperationException("Order not found.");

            if (!OrderStateMachine.CanTransition(order.Status, to))
                throw new InvalidOperationException($"Cannot transition from '{order.Status}' to '{to}'. Allowed: {OrderStateMachine.AllowedTargetsText(order.Status)}");

            order.Status = to;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var updated = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == order.Id, ct);

            return updated.ToDto();
        }

    public async Task<OrderResponseDto> MarkPaidAsync(int id, CancellationToken ct = default)
    {
        return await TransitionAsync(id, OrderStatus.Paid, ct);
    }

    public async Task<OrderResponseDto> MarkShippedAsync(int id, CancellationToken ct = default)
    {
        return await TransitionAsync(id, OrderStatus.Shipped, ct);
    }

    public async Task<OrderResponseDto> MarkDeliveredAsync(int id, CancellationToken ct = default)
    {
        return await TransitionAsync(id, OrderStatus.Delivered, ct);
    }

    public async Task<OrderResponseDto> CancelAsync(int id, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            throw new InvalidOperationException("Order not found.");

        if (order.Status == OrderStatus.Cancelled)
            return order.ToDto(); // idempotent

        // Use state machine to validate transition
        if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.Cancelled))
            throw new InvalidOperationException($"Order cannot be cancelled from status '{order.Status}'. Allowed: {OrderStateMachine.AllowedTargetsText(order.Status)}");

        // Capture items before SaveChanges so EF doesn't clear them
        var items = order.Items
            .Select(i => new Events.OrderEvents.OrderItemEvent(i.ProductId, i.Quantity))
            .ToList();

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Stock restore is handled asynchronously by CatalogService via the event
        try
        {
            await _publisher.PublishAsync("orders", "order.cancelled", new Events.OrderEvents.OrderCancelled(order.Id, items));
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to publish order.cancelled event for order {OrderId}", order.Id); }

        var updated = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == order.Id, ct);

        return updated.ToDto();
    }

    public async Task<OrderResponseDto> CreateAsync(CreateOrderRequestDto req, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("Catalog");

        if (req is null)
            throw new InvalidOperationException("Request body is required.");

        if (string.IsNullOrWhiteSpace(req.CustomerEmail))
            throw new InvalidOperationException("CustomerEmail is required.");

        if (req.Items is null || req.Items.Count == 0)
            throw new InvalidOperationException("Order must contain at least one item.");

        var order = new Order
        {
            CustomerEmail = req.CustomerEmail.Trim(),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in req.Items)
        {
            if (item.ProductId <= 0)
                throw new InvalidOperationException("ProductId must be > 0.");

            if (item.Quantity <= 0)
                throw new InvalidOperationException("Quantity must be >= 1.");

            // assumes CatalogService: GET /api/Products/{id}
            var product = await client.GetFromJsonAsync<CatalogProductDto>(
                $"/api/Products/{item.ProductId}", ct);

            if (product is null)
                throw new InvalidOperationException($"Product not found: {item.ProductId}");

            if (!product.IsActive)
                throw new InvalidOperationException($"Product is inactive: {product.Id}");

            if (product.Stock < item.Quantity)
                throw new InvalidOperationException(
                    $"Not enough stock for product {product.Id}. Stock={product.Stock}");

            var lineTotal = product.Price * item.Quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductSku = product.Sku,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });
        }

        order.TotalAmount = order.Items.Sum(x => x.LineTotal);

        var decReq = new InventoryDecreaseRequestDto
        {
            Items = order.Items.Select(i => new InventoryDecreaseItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        // Save order first so a DB failure never leaves inventory decremented with no order record.
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        var decResp = await client.PostAsJsonAsync("/api/Inventory/decrease", decReq, ct);
        if (!decResp.IsSuccessStatusCode)
        {
            // Compensate: remove the saved order so we don't leave an orphaned Pending record.
            _db.Orders.Remove(order);
            await _db.SaveChangesAsync(ct);
            var msg = await decResp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Stock decrease failed: {msg}");
        }

        // publish OrderCreated event (fire-and-forget)
        try
        {
            await _publisher.PublishAsync("orders", "order.created", new Events.OrderEvents.OrderCreated(order.Id, order.CustomerEmail, order.TotalAmount));
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to publish order.created event for order {OrderId}", order.Id); }

        var created = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == order.Id, ct);

        return created.ToDto();
    }

    public async Task<OrderResponseDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order?.ToDto();
    }

    public async Task<PagedResult<OrderResponseDto>> GetAllAsync(
        int page,
        int pageSize,
        string? email,
        CancellationToken ct = default)
    {
        page = Paging.ClampPage(page);
        pageSize = Paging.ClampPageSize(pageSize, defaultSize: 10, maxSize: 200);

        var q = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
            q = q.Where(o => o.CustomerEmail == email.Trim());

        var totalCount = await q.CountAsync(ct);

        var orders = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderResponseDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = Paging.TotalPages(totalCount, pageSize),
            Items = orders.Select(o => o.ToDto()).ToList()
        };
    }
}