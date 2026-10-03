using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ConcurrencyTests
{
    public class ConcurrentStockDeductionTests
    {
        private const string BaseUrl = "https://localhost:7045";

        [Fact]
        public async Task ConcurrentStockDeduction_ShouldNotOversellStock()
        {
            using var clientA = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };

            using var clientB = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };

            var requestA = new
            {
                customerId = 1001,
                shippingAddress = "Tangerang",
                items = new[]
                {
                    new
                    {
                        productId = 2,
                        quantity = 1
                    }
                }
            };

            var requestB = new
            {
                customerId = 1002,
                shippingAddress = "Bogor",
                items = new[]
                {
                    new
                    {
                        productId = 1,
                        quantity = 10
                    }
                }
            };

            var httpRequestA = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/order");

            httpRequestA.Headers.Add(
                "Idempotency-Key",
                $"CONCURRENT-A-{Guid.NewGuid()}");

            httpRequestA.Content = JsonContent.Create(requestA);

            var httpRequestB = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/order");

            httpRequestB.Headers.Add(
                "Idempotency-Key",
                $"CONCURRENT-B-{Guid.NewGuid()}");

            httpRequestB.Content = JsonContent.Create(requestB);

            var results = await Task.WhenAll(
                clientA.SendAsync(httpRequestA),
                clientB.SendAsync(httpRequestB));

            var successCount = results.Count(
                response => response.IsSuccessStatusCode);

            var conflictCount = results.Count(
                response => response.StatusCode == HttpStatusCode.Conflict);

            Assert.Equal(1, successCount);
            Assert.Equal(1, conflictCount);
        }
    }
}