namespace LinkShortener.Application.Links;

public sealed record LinkCreationResult(
    string? Code,
    string? TargetUrl,
    string? ManagementKey,
    string? ErrorMessage)
{
    public bool IsSuccessful => Code is not null;

    public static LinkCreationResult Success(string code, string targetUrl, string managementKey) =>
        new(code, targetUrl, managementKey, null);

    public static LinkCreationResult Invalid(string errorMessage) =>
        new(null, null, null, errorMessage);
}
