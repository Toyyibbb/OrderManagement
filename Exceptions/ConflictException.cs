namespace OrderManagement.Exceptions
{
    public class ConflictException : ApiException
    {
        public ConflictException(
            string code,
            string message)
            : base(409, code, message)
        {
        }
    }
}
