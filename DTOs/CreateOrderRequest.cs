namespace OrderManagement.DTOs
{
    public class CreateOrderRequest
    {
        public int CustomerId { get; set; }

        public string ShippingAddress { get; set; }
            = string.Empty;

        public List<CreateOrderItemRequest> Items { get; set; } = new List<CreateOrderItemRequest>();
    }
}