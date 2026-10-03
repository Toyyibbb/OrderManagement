namespace OrderManagement.DTOs
{
    public class InventoryProductResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int StockQuantity { get; set; }

        public decimal Price { get; set; }
    }
}