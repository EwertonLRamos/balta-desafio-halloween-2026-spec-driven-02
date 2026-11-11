namespace LinkShortener.Application.Links;

public sealed record LinkExpirationValidationResult(
    bool IsValid,
    DateTimeOffset? ExpiresAtUtc,
    string? ErrorMessage)
{
    public static LinkExpirationValidationResult Valid(DateTimeOffset? expiresAtUtc) =>
        new(true, expiresAtUtc, null);

    public static LinkExpirationValidationResult Invalid(string errorMessage) =>
        new(false, null, errorMessage);
}
