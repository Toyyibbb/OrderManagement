using OrderManagement.DTOs;
using OrderManagement.Models;

namespace OrderManagement.Services
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(CreateOrderRequest request,string idempotencyKey);

        Task<Order?> GetOrderByIdAsync(int orderId);

        Task<PagedResult<Order>> GetOrdersAsync(OrderListRequest request);

        Task<Order> UpdateOrderStatusAsync(int orderId,UpdateOrderStatusRequest request);

        Task<Order> CancelOrderAsync(int orderId,string concurrencyVersion);
    }
}