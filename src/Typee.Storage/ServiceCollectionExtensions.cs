using Microsoft.Extensions.DependencyInjection;
using Typee.Core.Abstractions;
using Typee.Core.Phrases;
using Typee.Storage.Repositories;
using Typee.Storage.Settings;
using Typee.Storage.Sqlite;

namespace Typee.Storage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypeeStorage(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<SqliteDbConnectionFactory>();
        services.AddSingleton<MigrationRunner>();

        services.AddSingleton<IPhraseRepository, PhraseRepository>();
        services.AddSingleton<ICompletionCacheRepository, CompletionCacheRepository>();
        services.AddSingleton<IUserDictionaryRepository, UserDictionaryRepository>();
        services.AddSingleton<ISnippetRepository, SnippetRepository>();
        services.AddSingleton<ICustomCommandRepository, CustomCommandRepository>();
        services.AddSingleton<IRecentInstructionRepository, RecentInstructionRepository>();
        services.AddSingleton<IActionProfileMemoryRepository, ActionProfileMemoryRepository>();
        services.AddSingleton<IEndpointAcknowledgmentRepository, EndpointAcknowledgmentRepository>();
        services.AddSingleton<ICaretMethodMemoryRepository, CaretMethodMemoryRepository>();

        services.AddSingleton<IPhraseService, PhraseService>();
        services.AddSingleton<ITypeeDatabase, TypeeDatabase>();
        return services;
    }
}
