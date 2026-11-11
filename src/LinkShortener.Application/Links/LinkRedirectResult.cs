namespace LinkShortener.Application.Links;

public enum LinkRedirectStatus
{
    Found,
    NotFound,
    Expired
}

public sealed record LinkRedirectResult(LinkRedirectStatus Status, string? TargetUrl)
{
    public static LinkRedirectResult Found(string targetUrl) =>
        new(LinkRedirectStatus.Found, targetUrl);

    public static LinkRedirectResult NotFound() =>
        new(LinkRedirectStatus.NotFound, null);

    public static LinkRedirectResult Expired() =>
        new(LinkRedirectStatus.Expired, null);
}
