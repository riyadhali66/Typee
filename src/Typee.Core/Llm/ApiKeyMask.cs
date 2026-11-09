namespace Typee.Core.Llm;

public static class ApiKeyMask
{
    public static string? Last4(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length <= 4
            ? trimmed
            : trimmed[^4..];
    }

    public static string FormatMasked(string? last4) =>
        string.IsNullOrEmpty(last4) ? "(not set)" : $"••••{last4}";
}
