namespace Typee.Core.Abstractions;

/// <summary>
/// Stores LLM API keys outside settings.json (Windows Credential Manager).
/// Never log key material; UI may show last 4 characters only.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>Default profile name used until multi-profile (Phase 8F).</summary>
    const string DefaultProfile = "default";

    void Save(string profileName, string apiKey);

    void Clear(string profileName);

    /// <summary>Reads Credential Manager only (does not fall back to env).</summary>
    bool TryRead(string profileName, out string? apiKey);

    /// <summary>Last 4 chars from CredMan when present; otherwise null.</summary>
    string? GetMaskedLast4(string profileName);
}
