using Microsoft.Extensions.DependencyInjection;
using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Core.Suggestions;
using Typee.Core.Suggestions.Providers;

namespace Typee.Llm;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypeeLlm(this IServiceCollection services)
    {
        services.AddSingleton<IPromptTemplateProvider, PromptTemplateProvider>();
        services.AddSingleton<IApiKeyResolver, ApiKeyResolver>();
        services.AddSingleton<ILlmRateGuard, LlmRateGuard>();
        services.AddSingleton<SuggestionRequestCoordinator>();
        services.AddSingleton<ISuggestionProvider, LocalPhraseSuggestionProvider>();
        services.AddSingleton<ISuggestionProvider, CompletionCacheSuggestionProvider>();
        services.AddSingleton<ISuggestionProvider, GrammarSuggestionProvider>();
        services.AddSingleton<ISuggestionProvider, LlmStyleCheckSuggestionProvider>();
        services.AddSingleton<ISuggestionProvider, LlmCompletionSuggestionProvider>();
        services.AddSingleton<SuggestionRouter>();
        services.AddSingleton<SuggestionPipelineService>();
        services.AddSingleton<OpenAiCompatibleClient>();
        services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<OpenAiCompatibleClient>());
        return services;
    }
}
