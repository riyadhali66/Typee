using Microsoft.Extensions.DependencyInjection;
using Typee.Core.Abstractions;
using Typee.Platform.Windows.Caret;
using Typee.Platform.Windows.Insertion;
using Typee.Platform.Windows.Process;
using Typee.Platform.Windows.Security;
using Typee.Platform.Windows.Uia;

namespace Typee.Platform.Windows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypeePlatformWindows(this IServiceCollection services)
    {
        services.AddSingleton<UiaThread>();
        services.AddSingleton<UiaElementCache>();
        services.AddSingleton<FocusMonitor>();
        services.AddSingleton<ProcessInfoService>();
        services.AddSingleton<ITargetContextGuard>(sp => sp.GetRequiredService<ProcessInfoService>());
        services.AddSingleton<ICaretLocator, CaretLocator>();
        services.AddSingleton<ITextContextReader, UiaTextContextReader>();
        services.AddSingleton<IFocusSnapshotService, FocusSnapshotService>();
        services.AddSingleton<ITextInserter, TextInserter>();
        services.AddSingleton<IApiKeyStore, WindowsCredentialApiKeyStore>();
        return services;
    }
}
