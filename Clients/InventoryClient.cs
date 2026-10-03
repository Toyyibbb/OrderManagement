using System.Net;
using System.Net.Http.Json;
using OrderManagement.DTOs;
using OrderManagement.Exceptions;
using OrderManagement.Models;

namespace OrderManagement.Clients
{
    public class InventoryClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<InventoryClient> _logger;

        public InventoryClient(
            HttpClient httpClient,
            ILogger<InventoryClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<InventoryProductResponse> GetProductAsync(
            int productId)
        {
            var response =
                await _httpClient.GetAsync(
                    $"api/inventory/products/{productId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException(
                    "PRODUCT_NOT_FOUND",
                    "Product tidak ditemukan.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new BadRequestException(
                    "INVALID_PRODUCT_ID",
                    "ProductId tidak valid.");
            }

            response.EnsureSuccessStatusCode();

            var product =
                await response.Content.ReadFromJsonAsync<InventoryProductResponse>();

            if (product == null)
            {
                throw new Exception(
                    "Response product dari Inventory Service kosong.");
            }

            return product;
        }

        public async Task<InventoryProductResponse> ReserveStockAsync(
            int productId,
            int quantity)
        {
            var request = new
            {
                productId,
                quantity
            };

            var response =
                await _httpClient.PostAsJsonAsync(
                    "api/inventory/reserve",
                    request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException(
                    "PRODUCT_NOT_FOUND",
                    "Product tidak ditemukan.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new BadRequestException(
                    "INVALID_STOCK_REQUEST",
                    "Data reserve stock tidak valid.");
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new ConflictException(
                    "INSUFFICIENT_STOCK",
                    "Stock tidak mencukupi.");
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new Exception(
                    "Inventory Service mengalami error.");
            }

            response.EnsureSuccessStatusCode();

            var product =
                await response.Content.ReadFromJsonAsync<InventoryProductResponse>();

            if (product == null)
            {
                throw new Exception(
                    "Response reserve stock kosong.");
            }

            _logger.LogInformation(
                "Stock reserved through Inventory Service | ProductId={ProductId} | Quantity={Quantity}",
                productId,
                quantity);

            return product;
        }

        public async Task<InventoryProductResponse> ReleaseStockAsync(
            int productId,
            int quantity)
        {
            var request = new
            {
                productId,
                quantity
            };

            var response =
                await _httpClient.PostAsJsonAsync(
                    "api/inventory/release",
                    request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException(
                    "PRODUCT_NOT_FOUND",
                    "Product tidak ditemukan.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new BadRequestException(
                    "INVALID_STOCK_REQUEST",
                    "Data release stock tidak valid.");
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new Exception(
                    "Inventory Service mengalami error.");
            }

            response.EnsureSuccessStatusCode();

            var product =
                await response.Content.ReadFromJsonAsync<InventoryProductResponse>();

            if (product == null)
            {
                throw new Exception(
                    "Response release stock kosong.");
            }

            _logger.LogInformation(
                "Stock released through Inventory Service | ProductId={ProductId} | Quantity={Quantity}",
                productId,
                quantity);

            return product;
        }
    }
}