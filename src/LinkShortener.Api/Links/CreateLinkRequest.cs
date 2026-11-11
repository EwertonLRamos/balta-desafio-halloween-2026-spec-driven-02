namespace LinkShortener.Api.Links;

/// <summary>Dados necessários para criar um link curto.</summary>
public sealed record CreateLinkRequest(string? TargetUrl);
