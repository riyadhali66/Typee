namespace Typee.Core.Abstractions;

/// <summary>Loads prompt templates from Typee.Llm/Prompts (name without extension).</summary>
public interface IPromptTemplateProvider
{
    string GetTemplate(string name);
}
