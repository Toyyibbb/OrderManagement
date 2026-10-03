namespace OrderManagement.DTOs
{
    public class OrderListRequest
    {
        public string? Status { get; set; }

        public int? CustomerId { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;
    }
}