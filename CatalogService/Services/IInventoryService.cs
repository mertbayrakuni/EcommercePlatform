using CatalogService.Dtos;

namespace CatalogService.Services;

/// <summary>
/// Manages product stock levels. DecreaseStock runs inside a DB transaction;
/// IncreaseStock is used by the order-cancelled event consumer to restore stock.
/// </summary>
public interface IInventoryService
{
    /// <summary>Atomically decreases stock for the given items. Throws if any product is inactive or has insufficient stock.</summary>
    Task<InventoryDecreaseResponseDto> DecreaseStockAsync(InventoryDecreaseRequestDto req, CancellationToken ct = default);

    /// <summary>Increases stock for the given items. Used to restore stock on order cancellation.</summary>
    Task<InventoryDecreaseResponseDto> IncreaseStockAsync(InventoryDecreaseRequestDto req, CancellationToken ct = default);
}