namespace OrderManagement.Models
{
    public class IdempotencyRequest
    {
        public int Id { get; set; }

        public string IdempotencyKey { get; set; } = string.Empty;

        public int? OrderId { get; set; }

        public DateTime CreatedAt { get; set; }

        public Order? Order { get; set; }
    }
}