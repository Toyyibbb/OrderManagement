using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Data;
using OrderManagement.DTOs;
using OrderManagement.Models;
using OrderManagement.Exceptions;
using System.Text.Json;
using System.Data;
using System.Data.Common;
using OrderManagement.Clients;

namespace OrderManagement.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;
        private readonly InventoryClient _inventoryClient;

        public OrderService(
            AppDbContext context,
            ILogger<OrderService> logger,
            InventoryClient inventoryClient)
        {
            _context = context;
            _logger = logger;
            _inventoryClient = inventoryClient;
        }

        private async Task<Order?> ReadOrderAsync(DbDataReader reader)
        {
            if (!await reader.ReadAsync())
                return null;

            return new Order
            {
                Id = reader.GetInt32(
                    reader.GetOrdinal("Id")),

                CustomerId = reader.GetInt32(
                    reader.GetOrdinal("CustomerId")),

                ShippingAddress = reader.GetString(
                    reader.GetOrdinal("ShippingAddress")),

                Status = reader.GetString(
                    reader.GetOrdinal("Status")),

                CreatedAt = reader.GetDateTime(
                    reader.GetOrdinal("CreatedAt")),

                ConcurrencyVersion =
                    (byte[])reader["ConcurrencyVersion"],

                OrderItems = new List<OrderItem>()
            };
        }

        private async Task ReadOrderItemsAsync(
            DbDataReader reader,
            Order order)
        {
            while (await reader.ReadAsync())
            {
                order.OrderItems.Add(
                    new OrderItem
                    {
                        Id = reader.GetInt32(
                            reader.GetOrdinal("Id")),

                        OrderId = reader.GetInt32(
                            reader.GetOrdinal("OrderId")),

                        ProductId = reader.GetInt32(
                            reader.GetOrdinal("ProductId")),

                        Quantity = reader.GetInt32(
                            reader.GetOrdinal("Quantity"))
                    });
            }
        }

        private DbCommand CreateCommand(
            string procedureName,
            params (string Name, object Value)[] parameters)
        {
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var command = connection.CreateCommand();

            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            foreach (var parameter in parameters)
            {
                var dbParameter = command.CreateParameter();

                dbParameter.ParameterName = parameter.Name;
                dbParameter.Value = parameter.Value;

                command.Parameters.Add(dbParameter);
            }

            return command;
        }

        private void ValidateCreateOrder(
            CreateOrderRequest request,
            string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                throw new BadRequestException(
                    "IDEMPOTENCY_KEY_REQUIRED",
                    "Idempotency-Key wajib diisi.");
            }

            if (idempotencyKey.Trim().Length > 100)
            {
                throw new BadRequestException(
                    "IDEMPOTENCY_KEY_TOO_LONG",
                    "Idempotency-Key maksimal 100 karakter.");
            }

            if (request.CustomerId <= 0)
            {
                throw new BadRequestException(
                    "INVALID_CUSTOMER_ID",
                    "CustomerId harus lebih besar dari 0.");
            }

            if (string.IsNullOrWhiteSpace(request.ShippingAddress))
            {
                throw new BadRequestException(
                    "INVALID_SHIPPING_ADDRESS",
                    "ShippingAddress wajib diisi.");
            }

            if (request.Items == null || !request.Items.Any())
            {
                throw new BadRequestException(
                    "ORDER_ITEMS_REQUIRED",
                    "Order harus memiliki minimal satu item.");
            }

            if (request.Items.Any(x => x.ProductId <= 0))
            {
                throw new BadRequestException(
                    "INVALID_PRODUCT_ID",
                    "ProductId harus lebih besar dari 0.");
            }

            if (request.Items.Any(x => x.Quantity <= 0))
            {
                throw new BadRequestException(
                    "INVALID_QUANTITY",
                    "Quantity harus lebih besar dari 0.");
            }
        }

        private async Task ReleaseReservedStockAsync(
            List<CreateOrderItemRequest> reservedItems)
        {
            foreach (var item in reservedItems)
            {
                try
                {
                    await _inventoryClient.ReleaseStockAsync(
                        item.ProductId,
                        item.Quantity);
                }
                catch (Exception releaseEx)
                {
                    _logger.LogError(
                        releaseEx,
                        "Failed to release stock compensation | " +
                        "ProductId={ProductId} | " +
                        "Quantity={Quantity}",
                        item.ProductId,
                        item.Quantity);
                }
            }
        }

        private Exception HandleCreateOrderException(SqlException ex)
        {
            return ex.Number switch
            {
                50001 => new BadRequestException(
                    "ORDER_ITEMS_REQUIRED",
                    "Order harus memiliki minimal satu item."),

                50002 => new BadRequestException(
                    "INVALID_ITEM",
                    "ProductId dan Quantity harus lebih besar dari 0."),

                50003 => new ConflictException(
                    "IDEMPOTENCY_IN_PROGRESS",
                    "Request dengan IdempotencyKey tersebut sedang diproses. Silakan coba lagi."),

                50004 => new ConflictException(
                    "IDEMPOTENCY_INVALID_STATE",
                    "Idempotency request sudah Completed tetapi OrderId kosong."),

                50005 => new ConflictException(
                    "IDEMPOTENCY_NOT_FOUND",
                    "IdempotencyKey tidak ditemukan."),

                _ => ex
            };
        }

        public async Task<Order> CreateOrderAsync(
            CreateOrderRequest request,
            string idempotencyKey)
        {
            ValidateCreateOrder(request, idempotencyKey);

            var key = idempotencyKey.Trim();

            var reservedItems =
                new List<CreateOrderItemRequest>();

            bool orderCreated = false;

            try
            {

                int? existingOrderId = null;

                await using (var claimCommand = CreateCommand(
                    "dbo.sp_ClaimIdempotencyKey",
                    ("@IdempotencyKey", key)))
                {
                    await using var claimReader =
                        await claimCommand.ExecuteReaderAsync();

                    if (await claimReader.ReadAsync())
                    {
                        var status = claimReader.GetString(
                            claimReader.GetOrdinal("Status"));

                        if (status == "Completed")
                        {
                            var orderIdOrdinal =
                                claimReader.GetOrdinal("OrderId");

                            if (claimReader.IsDBNull(orderIdOrdinal))
                            {
                                throw new Exception(
                                    "Idempotency request Completed tetapi OrderId kosong.");
                            }

                            existingOrderId =
                                claimReader.GetInt32(orderIdOrdinal);
                        }
                    }
                }


                if (existingOrderId.HasValue)
                {
                    var existingOrder =
                        await GetOrderByIdAsync(
                            existingOrderId.Value);

                    if (existingOrder == null)
                    {
                        throw new NotFoundException(
                            "ORDER_NOT_FOUND",
                            "Order dari IdempotencyKey tidak ditemukan.");
                    }

                    _logger.LogInformation(
                        "Returning existing order | " +
                        "IdempotencyKey={IdempotencyKey} | " +
                        "OrderId={OrderId}",
                        key,
                        existingOrder.Id);

                    return existingOrder;
                }

                foreach (var item in request.Items)
                {
                    await _inventoryClient.ReserveStockAsync(
                        item.ProductId,
                        item.Quantity);

                    reservedItems.Add(item);
                }

                var itemsJson =
                    JsonSerializer.Serialize(request.Items);

                await using var command = CreateCommand(
                    "dbo.sp_CreateOrder",
                    ("@CustomerId", request.CustomerId),
                    ("@ShippingAddress", request.ShippingAddress),
                    ("@IdempotencyKey", key),
                    ("@ItemsJson", itemsJson));

                await using var reader =
                    await command.ExecuteReaderAsync();

                var order =
                    await ReadOrderAsync(reader);

                if (order == null)
                {
                    throw new Exception(
                        "Order tidak berhasil dibuat.");
                }

                orderCreated = true;

                await reader.NextResultAsync();

                await ReadOrderItemsAsync(
                    reader,
                    order);

                _logger.LogInformation(
                    "CreateOrder completed | " +
                    "OrderId={OrderId} | " +
                    "CustomerId={CustomerId} | " +
                    "IdempotencyKey={IdempotencyKey}",
                    order.Id,
                    order.CustomerId,
                    key);

                return order;
            }

            catch (SqlException ex)
            {
                if (ex.Number == 50003)
                {
                    throw HandleCreateOrderException(ex);
                }

                if (!orderCreated)
                {
                    try
                    {
                        await using var failCommand =
                            CreateCommand(
                                "dbo.sp_FailIdempotencyKey",
                                ("@IdempotencyKey", key));

                        await failCommand.ExecuteNonQueryAsync();
                    }
                    catch (Exception failEx)
                    {
                        _logger.LogError(
                            failEx,
                            "Failed to update Idempotency status | " +
                            "IdempotencyKey={IdempotencyKey}",
                            key);
                    }

                    await ReleaseReservedStockAsync(
                        reservedItems);
                }

                _logger.LogError(
                    ex,
                    "CreateOrder SQL failed | " +
                    "IdempotencyKey={IdempotencyKey}",
                    key);

                throw HandleCreateOrderException(ex);
            }

            catch (Exception ex)
            {
                if (!orderCreated)
                {
                    try
                    {
                        await using var failCommand =
                            CreateCommand(
                                "dbo.sp_FailIdempotencyKey",
                                ("@IdempotencyKey", key));

                        await failCommand.ExecuteNonQueryAsync();
                    }
                    catch (Exception failEx)
                    {
                        _logger.LogError(
                            failEx,
                            "Failed to update Idempotency status | " +
                            "IdempotencyKey={IdempotencyKey}",
                            key);
                    }

                    await ReleaseReservedStockAsync(
                        reservedItems);
                }

                _logger.LogError(
                    ex,
                    "CreateOrder failed | " +
                    "IdempotencyKey={IdempotencyKey}",
                    key);

                throw;
            }
        }

        public async Task<Order?> GetOrderByIdAsync(
            int orderId)
        {
            if (orderId <= 0)
            {
                throw new BadRequestException(
                    "INVALID_ORDER_ID",
                    "OrderId harus lebih besar dari 0.");
            }

            try
            {
                await using var command = CreateCommand(
                    "sp_GetOrderById",
                    ("@OrderId", orderId));

                await using var reader =
                    await command.ExecuteReaderAsync();

                var order =
                    await ReadOrderAsync(reader);

                if (order == null)
                {
                    _logger.LogWarning(
                        "Order not found | OrderId={OrderId}",
                        orderId);

                    return null;
                }

                await reader.NextResultAsync();

                await ReadOrderItemsAsync(
                    reader,
                    order);

                _logger.LogInformation(
                    "Order retrieved | " +
                    "OrderId={OrderId} | " +
                    "Status={Status}",
                    order.Id,
                    order.Status);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "GetOrderById failed | " +
                    "OrderId={OrderId}",
                    orderId);

                throw;
            }
        }

        public async Task<Order> UpdateOrderStatusAsync(
            int orderId,
            UpdateOrderStatusRequest request)
        {
            if (orderId <= 0)
            {
                throw new BadRequestException(
                    "INVALID_ORDER_ID",
                    "OrderId harus lebih besar dari 0.");
            }

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                throw new BadRequestException(
                    "STATUS_REQUIRED",
                    "Status wajib diisi.");
            }

            if (string.IsNullOrWhiteSpace(
                request.ConcurrencyVersion))
            {
                throw new BadRequestException(
                    "CONCURRENCY_VERSION_REQUIRED",
                    "ConcurrencyVersion wajib diisi.");
            }

            byte[] version;

            try
            {
                version =
                    Convert.FromBase64String(
                        request.ConcurrencyVersion);
            }
            catch (FormatException)
            {
                throw new BadRequestException(
                    "INVALID_CONCURRENCY_VERSION",
                    "ConcurrencyVersion tidak valid.");
            }

            var validStatuses = new[]
            {
                OrderStatuses.Pending,
                OrderStatuses.Confirmed,
                OrderStatuses.Shipped,
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            };

            var normalizedStatus =
                validStatuses.FirstOrDefault(x =>
                    string.Equals(
                        x,
                        request.Status.Trim(),
                        StringComparison.OrdinalIgnoreCase));

            if (normalizedStatus == null)
            {
                throw new UnprocessableEntityException(
                    "INVALID_ORDER_STATUS",
                    "Status order tidak valid.");
            }

            try
            {
                await using var command = CreateCommand(
                    "sp_UpdateOrderStatus",
                    ("@OrderId", orderId),
                    ("@NewStatus", normalizedStatus),
                    ("@ConcurrencyVersion", version));

                await using var reader =
                    await command.ExecuteReaderAsync();

                var order =
                    await ReadOrderAsync(reader);

                if (order == null)
                {
                    throw new NotFoundException(
                        "ORDER_NOT_FOUND",
                        "Order tidak ditemukan.");
                }

                _logger.LogInformation(
                    "Order status updated | " +
                    "OrderId={OrderId} | " +
                    "Status={Status}",
                    order.Id,
                    order.Status);

                return order;
            }
            catch (SqlException ex)
                when (ex.Number == 50010)
            {
                throw new NotFoundException(
                    "ORDER_NOT_FOUND",
                    "Order tidak ditemukan.");
            }
            catch (SqlException ex)
                when (ex.Number == 50011)
            {
                throw new ConflictException(
                    "CONCURRENCY_CONFLICT",
                    "Order sudah diubah oleh admin lain.");
            }
            catch (SqlException ex)
                when (ex.Number == 50012)
            {
                throw new ConflictException(
                    "INVALID_STATUS_TRANSITION",
                    "Status order tidak dapat diubah.");
            }
        }

        public async Task<PagedResult<Order>> GetOrdersAsync(
            OrderListRequest request)
        {
            if (request.Page < 1)
            {
                throw new BadRequestException(
                    "INVALID_PAGE",
                    "Page harus lebih besar atau sama dengan 1.");
            }

            if (request.PageSize < 1 ||
                request.PageSize > 100)
            {
                throw new BadRequestException(
                    "INVALID_PAGE_SIZE",
                    "PageSize harus antara 1 sampai 100.");
            }

            if (request.CustomerId.HasValue &&
                request.CustomerId.Value <= 0)
            {
                throw new BadRequestException(
                    "INVALID_CUSTOMER_ID",
                    "CustomerId harus lebih besar dari 0.");
            }

            if (request.FromDate.HasValue &&
                request.ToDate.HasValue &&
                request.FromDate.Value >
                request.ToDate.Value)
            {
                throw new BadRequestException(
                    "INVALID_DATE_RANGE",
                    "FromDate tidak boleh lebih besar dari ToDate.");
            }

            string? status = null;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var validStatuses = new[]
                {
                    OrderStatuses.Pending,
                    OrderStatuses.Confirmed,
                    OrderStatuses.Shipped,
                    OrderStatuses.Delivered,
                    OrderStatuses.Cancelled
                };

                status =
                    validStatuses.FirstOrDefault(x =>
                        string.Equals(
                            x,
                            request.Status.Trim(),
                            StringComparison.OrdinalIgnoreCase));

                if (status == null)
                {
                    throw new UnprocessableEntityException(
                        "INVALID_ORDER_STATUS",
                        "Status order tidak valid.");
                }
            }

            try
            {
                await using var command = CreateCommand(
                    "sp_GetOrders",
                    ("@Status",
                        (object?)status ?? DBNull.Value),
                    ("@CustomerId",
                        (object?)request.CustomerId ??
                        DBNull.Value),
                    ("@FromDate",
                        (object?)request.FromDate ??
                        DBNull.Value),
                    ("@ToDate",
                        (object?)request.ToDate ??
                        DBNull.Value),
                    ("@Page", request.Page),
                    ("@PageSize", request.PageSize));

                await using var reader =
                    await command.ExecuteReaderAsync();

                var totalData = 0;

                if (await reader.ReadAsync())
                {
                    totalData =
                        reader.GetInt32(
                            reader.GetOrdinal("TotalData"));
                }

                await reader.NextResultAsync();

                var orders = new List<Order>();

                while (await reader.ReadAsync())
                {
                    orders.Add(
                        new Order
                        {
                            Id = reader.GetInt32(
                                reader.GetOrdinal("Id")),

                            CustomerId = reader.GetInt32(
                                reader.GetOrdinal("CustomerId")),

                            ShippingAddress = reader.GetString(
                                reader.GetOrdinal("ShippingAddress")),

                            Status = reader.GetString(
                                reader.GetOrdinal("Status")),

                            CreatedAt = reader.GetDateTime(
                                reader.GetOrdinal("CreatedAt")),

                            ConcurrencyVersion =
                                (byte[])reader["ConcurrencyVersion"],

                            OrderItems =
                                new List<OrderItem>()
                        });
                }

                var totalPages =
                    (int)Math.Ceiling(
                        totalData /
                        (double)request.PageSize);

                _logger.LogInformation(
                    "Orders listed | " +
                    "Page={Page} | " +
                    "PageSize={PageSize} | " +
                    "TotalData={TotalData}",
                    request.Page,
                    request.PageSize,
                    totalData);

                return new PagedResult<Order>
                {
                    Data = orders,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalData = totalData,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "GetOrders failed");

                throw;
            }
        }

        public async Task<Order> CancelOrderAsync(
            int orderId,
            string concurrencyVersion)
        {
            if (orderId <= 0)
            {
                throw new BadRequestException(
                    "INVALID_ORDER_ID",
                    "OrderId harus lebih besar dari 0.");
            }

            if (string.IsNullOrWhiteSpace(
                concurrencyVersion))
            {
                throw new BadRequestException(
                    "CONCURRENCY_VERSION_REQUIRED",
                    "ConcurrencyVersion wajib diisi.");
            }

            byte[] version;

            try
            {
                version =
                    Convert.FromBase64String(
                        concurrencyVersion);
            }
            catch (FormatException)
            {
                throw new BadRequestException(
                    "INVALID_CONCURRENCY_VERSION",
                    "ConcurrencyVersion tidak valid.");
            }

            try
            {

                await using var command = CreateCommand(
                    "sp_CancelOrder",
                    ("@OrderId", orderId),
                    ("@ConcurrencyVersion", version));

                await using var reader =
                    await command.ExecuteReaderAsync();

                var order =
                    await ReadOrderAsync(reader);

                if (order == null)
                {
                    throw new NotFoundException(
                        "ORDER_NOT_FOUND",
                        "Order tidak ditemukan.");
                }


                await reader.NextResultAsync();

                await ReadOrderItemsAsync(
                    reader,
                    order);


                foreach (var item in order.OrderItems)
                {
                    await _inventoryClient.ReleaseStockAsync(
                        item.ProductId,
                        item.Quantity);
                }

                _logger.LogInformation(
                    "Order cancelled | " +
                    "OrderId={OrderId} | " +
                    "NewStatus={Status}",
                    order.Id,
                    order.Status);

                return order;
            }
            catch (SqlException ex)
                when (ex.Number == 50020)
            {
                throw new NotFoundException(
                    "ORDER_NOT_FOUND",
                    "Order tidak ditemukan.");
            }
            catch (SqlException ex)
                when (ex.Number == 50021)
            {
                throw new ConflictException(
                    "CONCURRENCY_CONFLICT",
                    "Order sudah diubah oleh admin lain. Silakan refresh data order terlebih dahulu.");
            }
            catch (SqlException ex)
                when (ex.Number == 50022)
            {
                throw new ConflictException(
                    "ORDER_CANNOT_BE_CANCELLED",
                    "Order tidak dapat dibatalkan karena statusnya sudah tidak memungkinkan.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "CancelOrder failed | " +
                    "OrderId={OrderId}",
                    orderId);

                throw;
            }
        }
    }
}