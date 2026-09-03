using Microsoft.AspNetCore.Mvc;

namespace AsyncDocumentProcessing.Api.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
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
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception occurred. TraceId: {TraceId}",
                    context.TraceIdentifier);

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(
            HttpContext context,
            Exception exception)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            var problemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Beklenmeyen bir hata oluştu."
            };

            problemDetails.Extensions["traceId"] =
                context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}