namespace CriticalService.Api.Idempotency;

public class IdempotencyRecord
{
    public Guid Id { get; set; }

    public string Key { get; set; } = "";
    public string PathAndQuery { get; set; } = "";
    public string Method { get; set; } = "";
    public string RequestHash { get; set; } = "";

    public int StatusCode { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string ResponseBody { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
