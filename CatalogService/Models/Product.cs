using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogService.Models
{
    /// <summary>
    /// Catalogue product with pricing, stock and optional category association.
    /// Price is stored as numeric(18,2). Stock is managed by <see cref="CatalogService.Services.InventoryService"/>.
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // Money: in Postgres we want decimal(18,2)
        [Column(TypeName = "numeric(18,2)")]
        public decimal Price { get; set; }

        public int Stock { get; set; }

        // Optional
        [MaxLength(2000)]
        public string? Description { get; set; }

        // Professional fields
        [Required, MaxLength(64)]
        public string Sku { get; set; } = Guid.NewGuid().ToString("N"); // default for new rows

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}