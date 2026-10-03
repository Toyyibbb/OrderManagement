using Microsoft.EntityFrameworkCore;
using InventoryService.Models;
using System.Collections.Generic;

namespace InventoryService.Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
    }
}