namespace LinkShortener.Application.Links;

public enum LinkStatisticsStatus
{
    Found,
    NotFound,
    Unauthorized
}

public sealed record LinkStatisticsResult(LinkStatisticsStatus Status, long? AccessCount)
{
    public static LinkStatisticsResult Found(long accessCount) =>
        new(LinkStatisticsStatus.Found, accessCount);

    public static LinkStatisticsResult NotFound() =>
        new(LinkStatisticsStatus.NotFound, null);

    public static LinkStatisticsResult Unauthorized() =>
        new(LinkStatisticsStatus.Unauthorized, null);
}
