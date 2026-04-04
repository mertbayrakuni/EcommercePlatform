using OrderService.Dtos;
using OrderService.Models;

namespace OrderService.Mappings;

public static class OrderMapping
{
    public static OrderResponseDto ToDto(this Order o) => new()
    {
        Id = o.Id,
        CustomerEmail = o.CustomerEmail,
        // Convert enum to string for external API contract
        Status = o.Status.ToString(),
        TotalAmount = o.TotalAmount,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
        Items = o.Items.Select(i => new OrderItemResponseDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductSku = i.ProductSku,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            LineTotal = i.LineTotal
        }).ToList()
    };
}