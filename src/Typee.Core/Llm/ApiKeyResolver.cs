using Typee.Core.Abstractions;

namespace Typee.Core.Llm;

public sealed class ApiKeyResolver : IApiKeyResolver
{
    private readonly IApiKeyStore _store;

    public ApiKeyResolver(IApiKeyStore store)
    {
        _store = store;
    }

    public string? Resolve(string profileName, string? apiKeyEnvVar, out string source)
    {
        if (_store.TryRead(profileName, out var fromCred) && !string.IsNullOrWhiteSpace(fromCred))
        {
            source = "credman";
            return fromCred;
        }

        if (!string.IsNullOrWhiteSpace(apiKeyEnvVar))
        {
            var fromEnv = Environment.GetEnvironmentVariable(apiKeyEnvVar.Trim());
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                source = "env";
                return fromEnv;
            }
        }

        source = "none";
        return null;
    }
}
