using Microsoft.EntityFrameworkCore;
using OrderManagement.Models;

namespace OrderManagement.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<IdempotencyRequest> IdempotencyRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>()
                .Property(x => x.ShippingAddress)
                .HasMaxLength(500)
                .IsRequired();

            modelBuilder.Entity<Order>()
                .Property(x => x.Status)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<Order>()
                .Property(x => x.ConcurrencyVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            modelBuilder.Entity<OrderItem>()
                .Property(x => x.Quantity)
                .IsRequired();

            modelBuilder.Entity<OrderItem>()
                .HasOne(x => x.Order)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IdempotencyRequest>()
                .Property(x => x.IdempotencyKey)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<IdempotencyRequest>()
                .HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            modelBuilder.Entity<IdempotencyRequest>()
                .HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}