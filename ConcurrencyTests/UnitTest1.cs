using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OrderManagement.Tests
{
    public class OrderServiceTests
    {
        private const string BaseUrl = "https://localhost:7045";

        [Fact]
        public async Task CreateOrder_ShouldDecreaseStock()
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };

            var request = new
            {
                customerId = 1001,
                shippingAddress = "Tangerang",
                items = new[]
                {
                    new
                    {
                        productId = 1,
                        quantity = 2
                    }
                }
            };

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/Order");

            httpRequest.Headers.Add(
                "Idempotency-Key",
                $"TEST-STOCK-{Guid.NewGuid()}");

            httpRequest.Content = JsonContent.Create(request);

            var response = await client.SendAsync(httpRequest);

            var body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.IsSuccessStatusCode,
                $"Request gagal. Status: {(int)response.StatusCode}, Body: {body}");
        }


        [Fact]
        public async Task CreateOrder_ShouldReject_WhenStockInsufficient()
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };

            var request = new
            {
                customerId = 1002,
                shippingAddress = "Bogor",
                items = new[]
                {
                    new
                    {
                        productId = 1,
                        quantity = 999999
                    }
                }
            };

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/Order");

            httpRequest.Headers.Add(
                "Idempotency-Key",
                $"TEST-INSUFFICIENT-{Guid.NewGuid()}");

            httpRequest.Content = JsonContent.Create(request);

            var response = await client.SendAsync(httpRequest);

            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(
                HttpStatusCode.Conflict,
                response.StatusCode);
        }


    }
}