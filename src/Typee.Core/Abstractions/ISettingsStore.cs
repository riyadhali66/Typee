using Typee.Core.Settings;

namespace Typee.Core.Abstractions;

public interface ISettingsStore
{
    string SettingsPath { get; }
    AppSettings Current { get; }
    event EventHandler<AppSettings>? SettingsChanged;
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
