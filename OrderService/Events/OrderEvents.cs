namespace OrderService.Events;

/// <summary>
/// Domain events published to RabbitMQ on the "orders" exchange.
/// Consumers (e.g. CatalogService) bind queues to the matching routing keys.
/// </summary>
public static class OrderEvents
{
    public record OrderItemEvent(int ProductId, int Quantity);

    public record OrderCreated(int OrderId, string CustomerEmail, decimal TotalAmount);
    public record OrderCancelled(int OrderId, IReadOnlyList<OrderItemEvent> Items);
    public record OrderPaid(int OrderId, string TransactionId);
}
