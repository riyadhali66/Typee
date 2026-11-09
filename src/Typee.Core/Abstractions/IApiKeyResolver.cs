namespace Typee.Core.Abstractions;

/// <summary>Resolves API keys: Credential Manager → environment variable → none.</summary>
public interface IApiKeyResolver
{
    /// <summary>
    /// Returns the key without logging it. <paramref name="source"/> is "credman", "env", or "none".
    /// </summary>
    string? Resolve(string profileName, string? apiKeyEnvVar, out string source);
}
