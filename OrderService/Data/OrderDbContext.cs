using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasMany(o => o.Items)
             .WithOne(i => i.Order!)
             .HasForeignKey(i => i.OrderId);

            e.HasIndex(o => o.CreatedAt);
            e.HasIndex(o => o.CustomerEmail);

            // Persist OrderStatus enum as string for readability and stability in DB
            e.Property(o => o.Status)
             .HasConversion<string>()
             .HasMaxLength(30);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasIndex(i => i.ProductId);
            e.HasIndex(i => i.ProductSku);
        });
    }
}