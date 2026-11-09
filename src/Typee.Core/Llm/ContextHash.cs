using System.Security.Cryptography;
using System.Text;

namespace Typee.Core.Llm;

/// <summary>Stable SHA-256 hex of UTF-8 context for completion_cache keys. Never logs input.</summary>
public static class ContextHash
{
    public static string Compute(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
