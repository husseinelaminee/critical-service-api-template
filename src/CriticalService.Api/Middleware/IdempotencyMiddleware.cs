using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CriticalService.Api.Data;
using CriticalService.Api.Idempotency;

namespace CriticalService.Api.Middleware;

public sealed class IdempotencyMiddleware : IMiddleware
{
    public const string HeaderName = "Idempotency-Key";

    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method) || !ctx.Request.Path.Equals("/todos", StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx);
            return;
        }

        if (!ctx.Request.Headers.TryGetValue(HeaderName, out var keyValues) || string.IsNullOrWhiteSpace(keyValues))
        {
            await next(ctx);
            return;
        }

        var key = keyValues.ToString();
        var db = ctx.RequestServices.GetRequiredService<AppDbContext>();

        ctx.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            ctx.Request.Body.Position = 0;
        }

        var pathAndQuery = ctx.Request.Path + ctx.Request.QueryString;
        var requestHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{pathAndQuery}\n{body}"))
        );

        var existing = await db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Key == key &&
                x.Method == ctx.Request.Method &&
                x.PathAndQuery == pathAndQuery &&
                x.RequestHash == requestHash);

        if (existing is not null)
        {
            ctx.Response.StatusCode = existing.StatusCode;
            ctx.Response.ContentType = existing.ContentType;
            await ctx.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        var originalBody = ctx.Response.Body;
        await using var mem = new MemoryStream();
        ctx.Response.Body = mem;

        try
        {
            await next(ctx);
        }
        catch
        {
            ctx.Response.Body = originalBody;
            throw;
        }

        ctx.Response.Body = originalBody;

        mem.Position = 0;
        var responseBody = await new StreamReader(mem).ReadToEndAsync();

        mem.Position = 0;
        await mem.CopyToAsync(originalBody);

        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = key,
            Method = ctx.Request.Method,
            PathAndQuery = pathAndQuery,
            RequestHash = requestHash,
            StatusCode = ctx.Response.StatusCode,
            ContentType = ctx.Response.ContentType ?? "application/json",
            ResponseBody = responseBody,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
