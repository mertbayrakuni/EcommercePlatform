namespace OrderService.Dtos;

public sealed class InventoryDecreaseRequestDto
{
    public List<InventoryDecreaseItemDto> Items { get; set; } = new();
}
public sealed class InventoryDecreaseItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}