using Cap1.LogiTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cap1.LogiTrack;

public class LogiTrackContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=logitrack.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure one-to-many relationship between Order and InventoryItem
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.SetNull); // When order is deleted, set OrderId to null

        // Configure InventoryItem properties
        modelBuilder.Entity<InventoryItem>()
            .Property(i => i.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<InventoryItem>()
            .Property(i => i.Location)
            .HasMaxLength(100);

        // Configure Order properties
        modelBuilder.Entity<Order>()
            .Property(o => o.CustomerName)
            .HasMaxLength(100);

        base.OnModelCreating(modelBuilder);
    }
}