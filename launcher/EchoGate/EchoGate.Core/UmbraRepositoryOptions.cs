namespace EchoGate.Core;

public static class UmbraRepositoryOptions
{
    public const string OfficialRepositoryUrl = "https://launcher.dev.demidevunit.com/launcher/umbra/plugin-catalog";

    public static IReadOnlyList<string> BuildEffectiveRepositoryUrls(UmbraSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        List<string> urls = new();
        if (settings.UseOfficialRepository)
            urls.Add(OfficialRepositoryUrl);

        urls.AddRange(NormalizeCustomRepositoryUrls(settings.CustomRepositoryUrls));
        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> NormalizeCustomRepositoryUrls(IEnumerable<string>? urls)
    {
        if (urls is null)
            return Array.Empty<string>();

        List<string> normalized = new();
        foreach (string? source in urls)
        {
            string value = (source ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
                throw new ArgumentException($"Umbra repository URL is invalid: {value}");

            if (!IsAllowedRepositoryUri(uri))
                throw new ArgumentException($"Umbra repository URL must use HTTPS, except localhost development URLs: {value}");

            normalized.Add(uri.ToString());
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsAllowedRepositoryUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> ParseRepositoryList(string value)
    {
        return NormalizeCustomRepositoryUrls(
            (value ?? "")
                .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static string FormatRepositoryList(IEnumerable<string> urls)
    {
        return string.Join(Environment.NewLine, NormalizeCustomRepositoryUrls(urls));
    }
}
