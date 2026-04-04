using System.ComponentModel.DataAnnotations;

namespace CatalogService.Dtos;

public class ProductCreateDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required, MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}