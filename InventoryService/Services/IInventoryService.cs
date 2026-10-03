using InventoryService.Models;

namespace InventoryService.Services
{
    public interface IInventoryService
    {
        Task<Product> GetProductAsync(int productId);

        Task<Product> ReserveStockAsync(
            int productId,
            int quantity);

        Task<Product> ReleaseStockAsync(
            int productId,
            int quantity);
    }
}