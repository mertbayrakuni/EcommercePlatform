namespace CatalogService.Dtos;

public sealed class InventoryDecreaseResponseDto
{
    public List<InventoryDecreaseResultDto> Items { get; set; } = new();
}

public sealed class InventoryDecreaseResultDto
{
    public int ProductId { get; set; }
    public int NewStock { get; set; }
}