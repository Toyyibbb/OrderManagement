namespace OrderManagement.Models
{
    public class Order
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public string ShippingAddress { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public byte[] ConcurrencyVersion { get; set; } = Array.Empty<byte>();

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}