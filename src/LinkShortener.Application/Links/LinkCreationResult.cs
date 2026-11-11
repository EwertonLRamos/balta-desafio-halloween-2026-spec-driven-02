namespace LinkShortener.Application.Links;

public sealed record LinkCreationResult(
    string? Code,
    string? TargetUrl,
    string? ManagementKey,
    string? ErrorMessage,
    string? ErrorField = null,
    bool IsConflict = false)
{
    public bool IsSuccessful => Code is not null;

    public static LinkCreationResult Success(string code, string targetUrl, string managementKey) =>
        new(code, targetUrl, managementKey, null);

    public static LinkCreationResult Invalid(string errorMessage, string errorField = "targetUrl") =>
        new(null, null, null, errorMessage, errorField);

    public static LinkCreationResult Conflict(string errorMessage) =>
        new(null, null, null, errorMessage, IsConflict: true);
}
