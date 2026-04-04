namespace OrderService.Models;

/// <summary>
/// Represents canonical order states used across the service.
/// Persisted as strings in the database for clarity.
/// </summary>
public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Delivered,
    Cancelled
}
