namespace OrderManagement.Exceptions
{
    public class NotFoundException : ApiException
    {
        public NotFoundException(
            string code,
            string message)
            : base(404, code, message)
        {
        }
    }
}