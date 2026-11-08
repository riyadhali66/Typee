using Typee.Core.Settings;

namespace Typee.Core.Abstractions;

public interface ISettingsStore
{
    string SettingsPath { get; }
    AppSettings Current { get; }
    IReadOnlyList<string> LastLoadNotices { get; }
    event EventHandler<AppSettings>? SettingsChanged;
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task ExportAsync(string destinationPath, CancellationToken cancellationToken = default);
    Task ImportAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}
