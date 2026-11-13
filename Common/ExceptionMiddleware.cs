using System.Net;
using System.Text.Json;

namespace ChatApp.Common
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = HttpStatusCode.InternalServerError;
            var response = new ApiResponse<object>
            {
                Success = false,
                Message = "An unexpected error occurred. Please try again later."
            };

            if (exception is AppException appEx)
            {
                statusCode = appEx.StatusCode;
                response.Message = appEx.Message;
                response.Errors = appEx.Errors;
                _logger.LogWarning("AppException [{StatusCode}]: {Message}", appEx.StatusCode, appEx.Message);
            }
            else
            {
                _logger.LogError(exception, "Unhandled Exception: {Message}", exception.Message);
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }
}

