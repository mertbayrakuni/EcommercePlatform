namespace OrderService.Events;

public static class OrderEvents
{
    public record OrderItemEvent(int ProductId, int Quantity);

    public record OrderCreated(int OrderId, string CustomerEmail, decimal TotalAmount);
    public record OrderCancelled(int OrderId, IReadOnlyList<OrderItemEvent> Items);
    public record OrderPaid(int OrderId, string TransactionId);
}
