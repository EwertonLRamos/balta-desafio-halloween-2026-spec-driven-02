namespace LinkShortener.Application.Links;

public sealed class LinkExpirationValidator
{
    public LinkExpirationValidationResult Validate(DateTimeOffset? expiresAtUtc)
    {
        if (expiresAtUtc is null)
        {
            return LinkExpirationValidationResult.Valid(null);
        }

        var expirationDateUtc = DateOnly.FromDateTime(expiresAtUtc.Value.UtcDateTime);
        var currentDateUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (expirationDateUtc < currentDateUtc)
        {
            return LinkExpirationValidationResult.Invalid(
                "A data de expiração não pode estar no passado.");
        }

        var endOfExpirationDateUtc = new DateTimeOffset(
            expirationDateUtc.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        return LinkExpirationValidationResult.Valid(endOfExpirationDateUtc);
    }
}
