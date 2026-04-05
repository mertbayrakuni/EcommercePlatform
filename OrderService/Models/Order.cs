using System.ComponentModel.DataAnnotations;

namespace OrderService.Models;

/// <summary>
/// Aggregate root for an order. Status transitions are governed
/// by <see cref="OrderService.Services.OrderStateMachine"/>.
/// </summary>
public class Order
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string CustomerEmail { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Line items associated with this order
    public List<OrderItem> Items { get; set; } = new();
}