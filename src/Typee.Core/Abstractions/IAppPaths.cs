namespace Typee.Core.Abstractions;

public interface IAppPaths
{
    string RootDirectory { get; }
    string SettingsFilePath { get; }
    string LogsDirectory { get; }
    string DatabaseFilePath { get; }
    string ModelsDirectory { get; }
}
