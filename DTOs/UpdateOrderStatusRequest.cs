namespace OrderManagement.DTOs
{
    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string ConcurrencyVersion { get; set; } = string.Empty;
    }
}