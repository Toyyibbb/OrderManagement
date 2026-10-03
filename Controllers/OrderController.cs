using Microsoft.AspNetCore.Mvc;
using OrderManagement.DTOs;
using OrderManagement.Models;
using OrderManagement.Services;

namespace OrderManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService,ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,[FromBody] CreateOrderRequest request)
        {
            _logger.LogInformation(
                "CreateOrder received | CustomerId={CustomerId} | IdempotencyKey={IdempotencyKey}",
                request.CustomerId,
                idempotencyKey);

            var order = await _orderService.CreateOrderAsync(
                request,
                idempotencyKey ?? string.Empty);

            _logger.LogInformation(
                "CreateOrder completed | OrderId={OrderId} | CustomerId={CustomerId} | Status={Status}",
                order.Id,
                order.CustomerId,
                order.Status);

            return CreatedAtAction(
                nameof(GetOrder),
                new { id = order.Id },
                MapOrderResponse(order));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            _logger.LogInformation(
                "GetOrder received | OrderId={OrderId}",
                id);

            var order = await _orderService.GetOrderByIdAsync(id);

            if (order == null)
            {
                throw new OrderManagement.Exceptions.NotFoundException(
                    "ORDER_NOT_FOUND",
                    "Order tidak ditemukan.");
            }

            return Ok(MapOrderResponse(order));
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id,[FromBody] UpdateOrderStatusRequest request)
        {
            _logger.LogInformation(
                "UpdateOrderStatus received | OrderId={OrderId} | NewStatus={Status}",
                id,
                request.Status);

            var order = await _orderService.UpdateOrderStatusAsync(
                id,
                request);

            _logger.LogInformation(
                "UpdateOrderStatus completed | OrderId={OrderId} | Status={Status}",
                order.Id,
                order.Status);

            return Ok(MapOrderResponse(order));
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders([FromQuery] OrderListRequest request)
        {
            _logger.LogInformation(
                "GetOrders received | Page={Page} | PageSize={PageSize} | Status={Status} | CustomerId={CustomerId}",
                request.Page,
                request.PageSize,
                request.Status,
                request.CustomerId);

            var result = await _orderService.GetOrdersAsync(request);

            return Ok(new
            {
                page = result.Page,
                pageSize = result.PageSize,
                totalData = result.TotalData,
                totalPages = result.TotalPages,
                data = result.Data.Select(MapOrderResponse)
            });
        }

        [HttpPut("{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int id,[FromBody] UpdateOrderStatusRequest request)
        {
            _logger.LogInformation(
                "CancelOrder received | OrderId={OrderId}",
                id);

            var order = await _orderService.CancelOrderAsync(
                id,
                request.ConcurrencyVersion);

            _logger.LogInformation(
                "CancelOrder completed | OrderId={OrderId} | Status={Status}",
                order.Id,
                order.Status);

            return Ok(MapOrderResponse(order));
        }

        private static object MapOrderResponse(Order order)
        {
            return new
            {
                id = order.Id,
                customerId = order.CustomerId,
                shippingAddress = order.ShippingAddress,
                status = order.Status,
                createdAt = order.CreatedAt,

                concurrencyVersion =
                    order.ConcurrencyVersion.Length > 0
                        ? Convert.ToBase64String(order.ConcurrencyVersion)
                        : null,

                items = order.OrderItems.Select(item => new
                {
                    productId = item.ProductId,
                    quantity = item.Quantity
                })
            };
        }
    }
}