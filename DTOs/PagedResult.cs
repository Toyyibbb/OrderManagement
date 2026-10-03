namespace OrderManagement.DTOs
{
    public class PagedResult<T>
    {
        public List<T> Data { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalData { get; set; }

        public int TotalPages { get; set; }
    }
}