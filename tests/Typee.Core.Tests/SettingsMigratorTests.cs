using Typee.Core.Settings;

namespace Typee.Core.Tests;

public sealed class SettingsMigratorTests
{
    [Fact]
    public void Migrate_FromMissingSchema_SetsVersionOneAndNewDefaults()
    {
        var settings = AppSettings.CreateDefault();
        settings.SchemaVersion = 0;
        settings.PauseInFullscreen = false;
        settings.UiLanguage = string.Empty;

        var changed = SettingsMigrator.Migrate(settings);

        Assert.True(changed);
        Assert.Equal(1, settings.SchemaVersion);
        Assert.True(settings.PauseInFullscreen);
        Assert.Equal("auto", settings.UiLanguage);
    }

    [Fact]
    public void Migrate_WhenAlreadyCurrent_IsNoOp()
    {
        var settings = AppSettings.CreateDefault();
        Assert.False(SettingsMigrator.Migrate(settings));
    }
}
