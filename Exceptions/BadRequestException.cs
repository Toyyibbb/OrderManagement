namespace OrderManagement.Exceptions
{
    public class BadRequestException : ApiException
    {
        public BadRequestException(
            string code,
            string message)
            : base(400, code, message)
        {
        }
    }
}