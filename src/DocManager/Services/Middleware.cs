using System.Net;
using System.Text.Json;

namespace DocManager.Services;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        context.Items["RequestId"] = requestId;

        _logger.LogInformation(
            "[{RequestId}] {Method} {Path} started",
            requestId, context.Request.Method, context.Request.Path);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{RequestId}] Unhandled exception on {Method} {Path}",
                requestId, context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var error = new { message = "An internal error occurred", requestId };
            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
            return;
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation(
                "[{RequestId}] {Method} {Path} completed {StatusCode} in {ElapsedMs}ms",
                requestId, context.Request.Method, context.Request.Path,
                context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}

public class FileSizeValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly long _maxFileSize;

    public FileSizeValidationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _maxFileSize = configuration.GetValue<long>("FileStorage:MaxFileSizeBytes", 50 * 1024 * 1024); // 50MB default
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > _maxFileSize)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
            context.Response.ContentType = "application/json";
            var error = new { message = $"Request body exceeds maximum size of {_maxFileSize} bytes" };
            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
            return;
        }

        await _next(context);
    }
}

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }

    public static IApplicationBuilder UseFileSizeValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<FileSizeValidationMiddleware>();
    }
}
