using Microsoft.Extensions.Primitives;
using log4net;

namespace OrderManagement.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-ID";

        private readonly RequestDelegate _next;
        private readonly ILog _logger;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
            _logger = LogManager.GetLogger(
                typeof(CorrelationIdMiddleware));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string correlationId;

            if (context.Request.Headers.TryGetValue(
                    HeaderName,
                    out StringValues existingCorrelationId)
                && !StringValues.IsNullOrEmpty(
                    existingCorrelationId))
            {
                correlationId =
                    existingCorrelationId.ToString();
            }
            else
            {
                correlationId =
                    Guid.NewGuid().ToString();
            }

            context.Items["CorrelationId"] =
                correlationId;

            context.Response.Headers[HeaderName] =
                correlationId;

            var shortCorrelationId =
                correlationId.Length > 8
                    ? correlationId.Substring(0, 8)
                    : correlationId;

            LogicalThreadContext.Properties["CorrelationId"] =
                shortCorrelationId;

            _logger.Info(
                $"Request started | Method={context.Request.Method} | Path={context.Request.Path}");

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    "Unhandled exception occurred.",
                    ex);

                throw;
            }
            finally
            {
                _logger.Info(
                    $"Request completed | StatusCode={context.Response.StatusCode}");

                LogicalThreadContext.Properties.Remove(
                    "CorrelationId");
            }
        }
    }
}