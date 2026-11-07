using Typee.Core.Abstractions;

namespace Typee.Core;

public sealed class TypeePaths : IAppPaths
{
    public TypeePaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Typee");
        SettingsFilePath = Path.Combine(RootDirectory, "settings.json");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        DatabaseFilePath = Path.Combine(RootDirectory, "typee.db");
        ModelsDirectory = Path.Combine(RootDirectory, "models");
    }

    public string RootDirectory { get; }
    public string SettingsFilePath { get; }
    public string LogsDirectory { get; }
    public string DatabaseFilePath { get; }
    public string ModelsDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ModelsDirectory);
    }
}
