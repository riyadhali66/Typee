using Microsoft.Extensions.DependencyInjection;
using Typee.Core.Abstractions;
using Typee.Storage.Settings;

namespace Typee.Storage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypeeStorage(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        return services;
    }
}
