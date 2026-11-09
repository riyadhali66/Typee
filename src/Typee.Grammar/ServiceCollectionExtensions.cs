using Microsoft.Extensions.DependencyInjection;
using Typee.Core.Abstractions;
using Typee.Grammar.Lsp;
using Typee.Grammar.Process;

namespace Typee.Grammar;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypeeGrammar(this IServiceCollection services)
    {
        services.AddSingleton<HarperProcessSupervisor>();
        services.AddSingleton<HarperLspClient>();
        services.AddSingleton<IGrammarChecker, GrammarService>();
        return services;
    }
}
