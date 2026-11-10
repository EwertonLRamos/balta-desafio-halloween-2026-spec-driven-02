namespace LinkShortener.Domain;

public sealed class Link
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public long AccessCount { get; set; }

    public string ManagementKeyHash { get; set; } = string.Empty;
}
