namespace LinkShortener.Api.Links;

/// <summary>Link curto criado e sua chave de gerenciamento.</summary>
public sealed record CreateLinkResponse(string ShortUrl, string Code, string ManagementKey);
