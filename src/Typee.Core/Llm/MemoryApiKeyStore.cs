using Typee.Core.Abstractions;

namespace Typee.Core.Llm;

/// <summary>In-memory API key store for tests and non-Windows fallbacks. Never logs keys.</summary>
public sealed class MemoryApiKeyStore : IApiKeyStore
{
    private readonly Dictionary<string, string> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public void Save(string profileName, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        lock (_gate)
        {
            _keys[profileName] = apiKey.Trim();
        }
    }

    public void Clear(string profileName)
    {
        lock (_gate)
        {
            _keys.Remove(profileName);
        }
    }

    public bool TryRead(string profileName, out string? apiKey)
    {
        lock (_gate)
        {
            if (_keys.TryGetValue(profileName, out var value))
            {
                apiKey = value;
                return true;
            }
        }

        apiKey = null;
        return false;
    }

    public string? GetMaskedLast4(string profileName)
    {
        return TryRead(profileName, out var key) ? ApiKeyMask.Last4(key) : null;
    }
}
