using Microsoft.AspNetCore.Diagnostics;
using Serilog;
using System.Text.Json;

namespace ServerService.Middleware
{
    public static class GlobalExceptionHandler
    {
        public static void ConfigureExceptionHandler(this IApplicationBuilder app)
        {
            app.UseExceptionHandler(exceptionHandlerApp =>
            {
                exceptionHandlerApp.Run(async context =>
                {
                    context.Response.ContentType = "application/json";

                    var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                    var exception = exceptionHandlerPathFeature?.Error;

                    if (exception != null)
                    {
                        var statusCode = GetStatusCode(exception);
                        context.Response.StatusCode = statusCode;

                        // Log the exception with Serilog
                        Log.Error(exception, 
                            "Unhandled exception occurred. StatusCode: {StatusCode}, Path: {Path}, Method: {Method}, User: {User}",
                            statusCode,
                            context.Request.Path,
                            context.Request.Method,
                            context.User?.Identity?.Name ?? "Anonymous");

                        var problemDetails = new
                        {
                            type = $"https://httpstatuses.com/{statusCode}",
                            title = GetTitleForStatusCode(statusCode),
                            status = statusCode,
                            detail = exception.Message,
                            instance = context.Request.Path.ToString()
                        };

                        await context.Response.WriteAsync(
                            JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            })
                        );
                    }
                });
            });
        }

        private static int GetStatusCode(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => StatusCodes.Status400BadRequest,
                ArgumentException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                JsonException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };
        }

        private static string GetTitleForStatusCode(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => "An error occurred"
            };
        }
    }
}