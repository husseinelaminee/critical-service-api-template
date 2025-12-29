using System.Text.Json;

namespace CriticalService.Api.Middleware;

public sealed class ProblemDetailsExceptionMiddleware : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {

            if (context.Response.HasStarted) throw;
            context.Response.Clear();

            var cid = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var v) ? v?.ToString() : null;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://httpstatuses.com/500",
                title = "Unexpected error",
                status = 500,
                detail = "An unexpected error occurred.",
                correlationId = cid,
                exception = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true
                    ? new { ex.Message, ex.GetType().FullName }
                    : null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
