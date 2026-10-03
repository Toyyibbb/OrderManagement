using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderManagement.Clients;
using OrderManagement.Data;
using OrderManagement.DTOs;
using OrderManagement.Models;
using OrderManagement.Services;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OrderManagement.Tests
{
    // =========================================================
    // FAKE HTTP MESSAGE HANDLER (Untuk Mocking HttpClient)
    // =========================================================
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    public class OrderServiceTests
    {
        private const string OrderConnectionString =
            "Server=localhost;Database=OrderManagementDb;Trusted_Connection=True;TrustServerCertificate=True;";

        private const string InventoryConnectionString =
            "Server=localhost;Database=InventoryDb;Trusted_Connection=True;TrustServerCertificate=True;";

        // =========================================================
        // DB CONTEXT
        // =========================================================

        private static AppDbContext CreateDbContext()
        {
            var options =
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(OrderConnectionString)
                    .Options;

            return new AppDbContext(options);
        }

        // =========================================================
        // SERVICE
        // =========================================================

        private static OrderService CreateService(
            AppDbContext context)
        {
            // Buat respons palsu untuk InventoryClient agar tidak perlu menembak server eksternal
            var fakeResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true}")
            };

            var fakeHandler = new FakeHttpMessageHandler(fakeResponse);

            var httpClient = new HttpClient(fakeHandler)
            {
                BaseAddress = new Uri("https://localhost:7046/")
            };

            var inventoryClient =
                new InventoryClient(
                    httpClient,
                    NullLogger<InventoryClient>.Instance);

            return new OrderService(
                context,
                NullLogger<OrderService>.Instance,
                inventoryClient);
        }

        // =========================================================
        // CREATE TEST PRODUCT
        // =========================================================

        private static async Task<int> CreateTestProductAsync(
            string name,
            int stock,
            decimal price = 100000)
        {
            await using var connection =
                new SqlConnection(InventoryConnectionString);

            await connection.OpenAsync();

            const string sql = """
                INSERT INTO Products
                (
                    Name,
                    StockQuantity,
                    Price,
                    CreatedAt
                )
                OUTPUT INSERTED.Id
                VALUES
                (
                    @Name,
                    @StockQuantity,
                    @Price,
                    GETDATE()
                );
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@Name",
                name);

            command.Parameters.AddWithValue(
                "@StockQuantity",
                stock);

            command.Parameters.AddWithValue(
                "@Price",
                price);

            var result =
                await command.ExecuteScalarAsync();

            return Convert.ToInt32(result);
        }

        // =========================================================
        // GET PRODUCT STOCK
        // =========================================================

        private static async Task<int> GetProductStockAsync(
            int productId)
        {
            await using var connection =
                new SqlConnection(InventoryConnectionString);

            await connection.OpenAsync();

            const string sql = """
                SELECT StockQuantity
                FROM Products
                WHERE Id = @ProductId;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@ProductId",
                productId);

            var result =
                await command.ExecuteScalarAsync();

            return Convert.ToInt32(result);
        }

        // =========================================================
        // DELETE TEST PRODUCT
        // =========================================================

        private static async Task DeleteTestProductAsync(
            int productId)
        {
            await using var connection =
                new SqlConnection(InventoryConnectionString);

            await connection.OpenAsync();

            const string sql = """
                DELETE FROM Products
                WHERE Id = @ProductId;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@ProductId",
                productId);

            await command.ExecuteNonQueryAsync();
        }

        // =========================================================
        // CLEANUP ORDER
        // =========================================================

        private static async Task CleanupOrderAsync(
            int orderId)
        {
            if (orderId <= 0)
                return;

            await using var connection =
                new SqlConnection(OrderConnectionString);

            await connection.OpenAsync();

            await using var transaction =
                (SqlTransaction)
                await connection.BeginTransactionAsync();

            try
            {
                const string deleteItems = """
                    DELETE FROM OrderItems
                    WHERE OrderId = @OrderId;
                    """;

                await using (var command =
                    new SqlCommand(
                        deleteItems,
                        connection,
                        transaction))
                {
                    command.Parameters.AddWithValue(
                        "@OrderId",
                        orderId);

                    await command.ExecuteNonQueryAsync();
                }

                const string deleteIdempotency = """
                    DELETE FROM IdempotencyRequests
                    WHERE OrderId = @OrderId;
                    """;

                await using (var command =
                    new SqlCommand(
                        deleteIdempotency,
                        connection,
                        transaction))
                {
                    command.Parameters.AddWithValue(
                        "@OrderId",
                        orderId);

                    await command.ExecuteNonQueryAsync();
                }

                const string deleteOrder = """
                    DELETE FROM Orders
                    WHERE Id = @OrderId;
                    """;

                await using (var command =
                    new SqlCommand(
                        deleteOrder,
                        connection,
                        transaction))
                {
                    command.Parameters.AddWithValue(
                        "@OrderId",
                        orderId);

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // TEST #1
        // =========================================================

        [Fact]
        public async Task CreateOrder_ShouldDecreaseStock()
        {
            const int customerId = 90001;

            var productId =
                await CreateTestProductAsync(
                    "TEST Laptop",
                    10,
                    15000000);

            int orderId = 0;

            try
            {
                await using var context =
                    CreateDbContext();

                var service =
                    CreateService(context);

                var request =
                    new CreateOrderRequest
                    {
                        CustomerId = customerId,

                        ShippingAddress = "Jakarta",

                        Items =
                        [
                            new CreateOrderItemRequest
                            {
                                ProductId = productId,
                                Quantity = 3
                            }
                        ]
                    };

                // CREATE ORDER
                var order =
                    await service.CreateOrderAsync(
                        request,
                        $"test-stock-{Guid.NewGuid()}");

                orderId = order.Id;

                // CHECK STOCK
                var stock =
                    await GetProductStockAsync(
                        productId);

                Assert.Equal(7, stock);

                // CHECK ORDER
                Assert.NotNull(order);

                Assert.Equal(
                    customerId,
                    order.CustomerId);

                Assert.Equal(
                    OrderStatuses.Pending,
                    order.Status);
            }
            finally
            {
                await CleanupOrderAsync(orderId);

                await DeleteTestProductAsync(
                    productId);
            }
        }
    }
}