namespace Typee.Core.Settings;

public static class SettingsMigrator
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Migrates in-place to <see cref="CurrentSchemaVersion"/>.</summary>
    public static bool Migrate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var changed = false;

        if (settings.SchemaVersion < 1)
        {
            settings.SchemaVersion = 1;
            settings.PauseInFullscreen = true;
            settings.UiLanguage = "auto";
            changed = true;
        }

        return changed;
    }
}
