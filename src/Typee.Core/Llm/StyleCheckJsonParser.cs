using System.Text.Json;
using Typee.Core.Models;

namespace Typee.Core.Llm;

/// <summary>
/// Parses LLM style-check JSON. Tolerates markdown fences and minor noise; malformed → null.
/// Never logs the payload (may contain user text).
/// </summary>
public static class StyleCheckJsonParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static StyleCheckResult? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var json = ExtractJsonObject(raw);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = doc.RootElement;
            if (!TryReadBool(root, "hasIssue", out var hasIssue)
                && !TryReadBool(root, "has_issue", out hasIssue))
            {
                return null;
            }

            var corrected = TryReadString(root, "corrected") ?? string.Empty;
            var explanation = TryReadString(root, "explanation") ?? string.Empty;

            return new StyleCheckResult
            {
                HasIssue = hasIssue,
                Corrected = corrected.Trim(),
                Explanation = explanation.Trim()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = trimmed.IndexOf('\n');
            if (firstNl > 0)
            {
                trimmed = trimmed[(firstNl + 1)..];
            }

            var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                trimmed = trimmed[..fence];
            }

            trimmed = trimmed.Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }

    private static bool TryReadBool(JsonElement root, string name, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(name, out var prop))
        {
            return false;
        }

        switch (prop.ValueKind)
        {
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.String:
                return bool.TryParse(prop.GetString(), out value);
            case JsonValueKind.Number:
                if (prop.TryGetInt32(out var n))
                {
                    value = n != 0;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private static string? TryReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Null => string.Empty,
            _ => prop.ToString()
        };
    }
}
