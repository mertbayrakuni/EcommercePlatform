using CatalogService.Dtos;

namespace CatalogService.Services;

public interface IInventoryService
{
    Task<InventoryDecreaseResponseDto> DecreaseStockAsync(InventoryDecreaseRequestDto req, CancellationToken ct = default);
    Task<InventoryDecreaseResponseDto> IncreaseStockAsync(InventoryDecreaseRequestDto req, CancellationToken ct = default);
}