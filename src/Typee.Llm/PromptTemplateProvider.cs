using System.Reflection;
using Typee.Core.Abstractions;

namespace Typee.Llm;

public sealed class PromptTemplateProvider : IPromptTemplateProvider
{
    private readonly Assembly _assembly = typeof(PromptTemplateProvider).Assembly;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public string GetTemplate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            if (_cache.TryGetValue(name, out var cached))
            {
                return cached;
            }
        }

        var resourceName = FindResource(name)
            ?? throw new FileNotFoundException($"Prompt template '{name}' was not found as an embedded resource.");

        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Prompt template resource '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Trim();

        lock (_gate)
        {
            _cache[name] = text;
        }

        return text;
    }

    private string? FindResource(string name)
    {
        var suffix = $".Prompts.{name}.txt";
        return _assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
