namespace Typee.Core.Llm;

/// <summary>
/// Requires HTTPS for any non-localhost baseUrl; localhost may use HTTP (e.g. Ollama).
/// </summary>
public static class LlmEndpointPolicy
{
    public static bool TryValidateBaseUrl(string? baseUrl, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            error = "Base URL is empty.";
            return false;
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Base URL is not a valid absolute URI.";
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            error = "Base URL must use http or https.";
            return false;
        }

        if (IsLocalhost(uri))
        {
            return true;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Remote LLM base URL must use HTTPS.";
            return false;
        }

        return true;
    }

    public static bool IsAllowed(string? baseUrl) => TryValidateBaseUrl(baseUrl, out _);

    public static bool IsLocalhost(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.IsLoopback)
        {
            return true;
        }

        var host = uri.Host;
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase);
    }
}
