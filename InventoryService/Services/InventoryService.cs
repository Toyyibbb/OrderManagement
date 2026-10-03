using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.Models;

namespace InventoryService.Services
{
    public class InventoryServiceImplementation : IInventoryService
    {
        private readonly InventoryDbContext _context;
        private readonly ILogger<InventoryServiceImplementation> _logger;

        public InventoryServiceImplementation(
            InventoryDbContext context,
            ILogger<InventoryServiceImplementation> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Product> GetProductAsync(int productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentException(
                    "ProductId harus lebih besar dari 0.");
            }

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == productId);

            if (product == null)
            {
                throw new KeyNotFoundException(
                    "Product tidak ditemukan.");
            }

            return product;
        }

        public async Task<Product> ReserveStockAsync(
            int productId,
            int quantity)
        {
            if (productId <= 0)
            {
                throw new ArgumentException(
                    "ProductId harus lebih besar dari 0.");
            }

            if (quantity <= 0)
            {
                throw new ArgumentException(
                    "Quantity harus lebih besar dari 0.");
            }

            try
            {
                await using var command = CreateCommand(
                    "sp_ReserveStock",
                    ("@ProductId", productId),
                    ("@Quantity", quantity));

                await using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    throw new KeyNotFoundException(
                        "Product tidak ditemukan.");
                }

                var product = ReadProduct(reader);

                _logger.LogInformation(
                    "Stock reserved | ProductId={ProductId} | Quantity={Quantity} | RemainingStock={Stock}",
                    productId,
                    quantity,
                    product.StockQuantity);

                return product;
            }
            catch (SqlException ex) when (ex.Number == 50002)
            {
                throw new KeyNotFoundException(
                    "Product tidak ditemukan.");
            }
            catch (SqlException ex) when (ex.Number == 50003)
            {
                throw new InvalidOperationException(
                    "Stock tidak mencukupi.");
            }
        }

        public async Task<Product> ReleaseStockAsync(
            int productId,
            int quantity)
        {
            if (productId <= 0)
            {
                throw new ArgumentException(
                    "ProductId harus lebih besar dari 0.");
            }

            if (quantity <= 0)
            {
                throw new ArgumentException(
                    "Quantity harus lebih besar dari 0.");
            }

            try
            {
                await using var command = CreateCommand(
                    "sp_ReleaseStock",
                    ("@ProductId", productId),
                    ("@Quantity", quantity));

                await using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    throw new KeyNotFoundException(
                        "Product tidak ditemukan.");
                }

                var product = ReadProduct(reader);

                _logger.LogInformation(
                    "Stock released | ProductId={ProductId} | Quantity={Quantity} | CurrentStock={Stock}",
                    productId,
                    quantity,
                    product.StockQuantity);

                return product;
            }
            catch (SqlException ex) when (ex.Number == 50002)
            {
                throw new KeyNotFoundException(
                    "Product tidak ditemukan.");
            }
        }

        private DbCommand CreateCommand(
            string procedureName,
            params (string Name, object Value)[] parameters)
        {
            var connection =
                _context.Database.GetDbConnection();

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

        private static Product ReadProduct(
            DbDataReader reader)
        {
            return new Product
            {
                Id = reader.GetInt32(
                    reader.GetOrdinal("Id")),

                Name = reader.GetString(
                    reader.GetOrdinal("Name")),

                StockQuantity = reader.GetInt32(
                    reader.GetOrdinal("StockQuantity")),

                Price = reader.GetDecimal(
                    reader.GetOrdinal("Price")),

                CreatedAt = reader.GetDateTime(
                    reader.GetOrdinal("CreatedAt"))
            };
        }
    }
}