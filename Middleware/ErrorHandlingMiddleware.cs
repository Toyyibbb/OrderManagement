using System.Text.Json;
using OrderManagement.Exceptions;

namespace OrderManagement.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(
            RequestDelegate next,
            ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ApiException ex)
            {
                await HandleApiExceptionAsync(context, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception");

                await HandleUnexpectedExceptionAsync(context);
            }
        }

        private static async Task HandleApiExceptionAsync(
            HttpContext context,
            ApiException exception)
        {
            var traceId =
                context.Items["CorrelationId"]?.ToString()
                ?? context.TraceIdentifier;

            context.Response.StatusCode =
                exception.StatusCode;

            context.Response.ContentType =
                "application/json";

            var response = new
            {
                status = exception.StatusCode,
                code = exception.Code,
                message = exception.Message,
                traceId
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }

        private static async Task HandleUnexpectedExceptionAsync(
            HttpContext context)
        {
            var traceId =
                context.Items["CorrelationId"]?.ToString()
                ?? context.TraceIdentifier;

            context.Response.StatusCode = 500;
            context.Response.ContentType =
                "application/json";

            var response = new
            {
                status = 500,
                code = "INTERNAL_SERVER_ERROR",
                message = "Terjadi kesalahan pada server.",
                traceId
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    }
}