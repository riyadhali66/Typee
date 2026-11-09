using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Typee.App.Resources;
using Typee.Core.Abstractions;

namespace Typee.App.ViewModels;

public sealed partial class DiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IDiagnosticsCollector _collector;
    private readonly DispatcherTimer _timer;

    public DiagnosticsViewModel(IDiagnosticsCollector collector)
    {
        _collector = collector;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        Title = Strings.Diagnostics_Title;
        Refresh();
        _timer.Start();
    }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _cpuText = "—";

    [ObservableProperty]
    private string _ramText = "—";

    [ObservableProperty]
    private string _hookText = "—";

    [ObservableProperty]
    private string _lastReinstallText = "—";

    [ObservableProperty]
    private string _caretMethodText = "—";

    [ObservableProperty]
    private string _harperText = "—";

    [ObservableProperty]
    private string _llmText = "—";

    public ObservableCollection<string> RecentErrors { get; } = [];

    public void Refresh()
    {
        var snap = _collector.Capture();
        CpuText = $"{Strings.Diagnostics_Cpu}: {snap.CpuPercent:0.0}%";
        RamText = $"{Strings.Diagnostics_Ram}: {snap.WorkingSetBytes / (1024.0 * 1024.0):0.0} MB";
        HookText = $"{Strings.Diagnostics_Hook}: {(snap.HookInstalled ? Strings.Diagnostics_Installed : Strings.Diagnostics_NotInstalled)}"
            + (snap.HookDroppedEvents > 0 ? $" (dropped {snap.HookDroppedEvents})" : string.Empty);
        LastReinstallText = $"{Strings.Diagnostics_LastReinstall}: "
            + (snap.LastHookReinstallUtc is { } t
                ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : Strings.Diagnostics_Never);
        CaretMethodText = $"{Strings.Diagnostics_CaretMethod}: {snap.CaretMethod}";
        HarperText = $"{Strings.Diagnostics_Harper}: {snap.HarperStatus}";
        LlmText = $"{Strings.Diagnostics_Llm}: {snap.LlmStatus}";

        RecentErrors.Clear();
        foreach (var error in snap.RecentErrors)
        {
            RecentErrors.Add(error);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
