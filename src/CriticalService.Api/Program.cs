using CriticalService.Api.Data;
using CriticalService.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);


// DB
var cs = builder.Configuration.GetConnectionString("Db") ?? "Data Source=app.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(cs));

// Middlewares
builder.Services.AddTransient<CorrelationIdMiddleware>();
builder.Services.AddTransient<ProblemDetailsExceptionMiddleware>();
builder.Services.AddTransient<IdempotencyMiddleware>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();


app.UseSwagger();
app.UseSwaggerUI();


app.UseHttpsRedirection();

// Auto-migrate for dev env
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Idempotency 
app.UseMiddleware<IdempotencyMiddleware>();

static string ToEtag(int version) => $"\"{version}\"";
static int FromEtag(string etag)
{
    // ex: "\"1\"" ou "W/\"1\"" ou "  \"1\"  "
    var s = etag.Trim();

    if (s.StartsWith("W/")) s = s[2..].Trim();
    s = s.Trim().Trim('"');

    if (!int.TryParse(s, out var v))
        throw new FormatException($"Invalid ETag/If-Match value: '{etag}'");

    return v;
}

// --- ENDPOINTS ---

// LIST
app.MapGet("/todos", async (AppDbContext db) =>
    await db.TodoItems.AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc)
        .ToListAsync());

// GET by id + ETag
app.MapGet("/todos/{id:guid}", async (Guid id, AppDbContext db, HttpResponse res) =>
{
    var item = await db.TodoItems.AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (item is null)
        return Results.NotFound(new { message = "Todo not found" });

    res.Headers.ETag = ToEtag(item.Version);
    return Results.Ok(item);
});

// POST (idempotent via Idempotency-Key middleware)
app.MapPost("/todos", async (
    AppDbContext db, 
    string title,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey) =>
{
    if (string.IsNullOrWhiteSpace(title))
        return Results.BadRequest(new { message = "title is required" });

    var item = new TodoItem
    {
        Id = Guid.NewGuid(),
        Title = title.Trim(),
        CreatedAtUtc = DateTime.UtcNow,
        Version = 1
    };

    db.TodoItems.Add(item);
    await db.SaveChangesAsync();

    return Results.Created($"/todos/{item.Id}", item);
});

// PUT + If-Match (optimistic concurrency)
app.MapPut("/todos/{id:guid}", async (
    Guid id,
    AppDbContext db,
    HttpRequest req,
    HttpResponse res,
    string? title,
    bool? isDone,
    [FromHeader(Name = "If-Match")] string ? ifMatch) =>
{
    if (!req.Headers.TryGetValue("If-Match", out var ifMatchValues) ||
        string.IsNullOrWhiteSpace(ifMatchValues))
    {
        return Results.StatusCode(StatusCodes.Status428PreconditionRequired);
    }

    var item = await db.TodoItems.FirstOrDefaultAsync(x => x.Id == id);
    if (item is null)
        return Results.NotFound(new { message = "Todo not found" });

    int providedVersion;
    try
    {
        providedVersion = FromEtag(ifMatchValues.ToString());
    }
    catch
    {
        return Results.BadRequest(new { message = "Invalid If-Match header. Expected an integer ETag like \"1\"." });
    }

    if (providedVersion != item.Version)
    {
        return Results.StatusCode(StatusCodes.Status412PreconditionFailed);
    }

    if (!string.IsNullOrWhiteSpace(title))
        item.Title = title.Trim();

    if (isDone.HasValue)
        item.IsDone = isDone.Value;

    item.Version++;

    await db.SaveChangesAsync();

    res.Headers.ETag = ToEtag(item.Version);
    return Results.Ok(item);
});

app.Run();

public partial class Program { }
