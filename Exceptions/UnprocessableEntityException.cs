namespace OrderManagement.Exceptions
{
    public class UnprocessableEntityException : ApiException
    {
        public UnprocessableEntityException(
            string code,
            string message)
            : base(422, code, message)
        {
        }
    }
}