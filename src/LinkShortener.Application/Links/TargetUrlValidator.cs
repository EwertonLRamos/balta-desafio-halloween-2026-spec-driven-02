using System.Net;

namespace LinkShortener.Application.Links;

public sealed class TargetUrlValidator
{
    private const int MaximumLength = 2048;

    public TargetUrlValidationResult Validate(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return TargetUrlValidationResult.Invalid("A URL de destino é obrigatória.");
        }

        if (targetUrl.Length > MaximumLength)
        {
            return TargetUrlValidationResult.Invalid(
                $"A URL de destino não pode ultrapassar {MaximumLength} caracteres.");
        }

        if (targetUrl.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return TargetUrlValidationResult.Invalid(
                "A URL de destino deve ser absoluta e usar o protocolo HTTP ou HTTPS.");
        }

        if (!IsPublicHost(uri))
        {
            return TargetUrlValidationResult.Invalid(
                "A URL de destino deve apontar para um endereço publicamente acessível.");
        }

        return TargetUrlValidationResult.Valid();
    }

    private static bool IsPublicHost(Uri uri)
    {
        var host = uri.Host.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.HostNameType == UriHostNameType.IPv6)
        {
            host = host.Trim('[', ']');
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return host.Contains('.');
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return !IPAddress.IsLoopback(address)
                && !address.IsIPv6LinkLocal
                && !address.IsIPv6UniqueLocal
                && !address.IsIPv6Multicast
                && !address.Equals(IPAddress.IPv6Any);
        }

        return IsPublicIpv4(address.GetAddressBytes());
    }

    private static bool IsPublicIpv4(byte[] address)
    {
        var first = address[0];
        var second = address[1];
        var third = address[2];

        if (first is 0 or 10 or 127 or >= 224)
        {
            return false;
        }

        if ((first == 100 && second is >= 64 and <= 127)
            || (first == 169 && second == 254)
            || (first == 172 && second is >= 16 and <= 31)
            || (first == 192 && second == 168)
            || (first == 192 && second == 0 && third == 0)
            || (first == 192 && second == 0 && third == 2)
            || (first == 192 && second == 88 && third == 99)
            || (first == 198 && second is 18 or 19)
            || (first == 198 && second == 51 && third == 100)
            || (first == 203 && second == 0 && third == 113))
        {
            return false;
        }

        return true;
    }
}

public readonly record struct TargetUrlValidationResult(bool IsValid, string? ErrorMessage)
{
    public static TargetUrlValidationResult Valid() => new(true, null);

    public static TargetUrlValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}
