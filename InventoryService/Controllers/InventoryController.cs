using Microsoft.AspNetCore.Mvc;
using InventoryService.DTOs;
using InventoryService.Services;

namespace InventoryService.Controllers
{
    [ApiController]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(
            IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpGet("products/{productId}")]
        public async Task<IActionResult> GetProduct(
            int productId)
        {
            try
            {
                var product =
                    await _inventoryService.GetProductAsync(
                        productId);

                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpPost("reserve")]
        public async Task<IActionResult> ReserveStock(
            [FromBody] ReserveStockRequest request)
        {
            try
            {
                var product =
                    await _inventoryService.ReserveStockAsync(
                        request.ProductId,
                        request.Quantity);

                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpPost("release")]
        public async Task<IActionResult> ReleaseStock(
            [FromBody] ReserveStockRequest request)
        {
            try
            {
                var product =
                    await _inventoryService.ReleaseStockAsync(
                        request.ProductId,
                        request.Quantity);

                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
        }
    }
}