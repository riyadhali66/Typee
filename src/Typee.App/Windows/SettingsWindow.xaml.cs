using System.Windows;
using System.Windows.Input;
using Typee.App.ViewModels;

namespace Typee.App.Windows;

/// <summary>
/// Callers: TrayIconHost.ShowSettings. Companion to SettingsWindow.xaml (already created).
/// No prior code-behind existed. Hotkey capture UI only; settings persist via ViewModel/ISettingsStore.
/// User instruction: "Settings window (simple WPF form) that edits and saves settings.json, including a hotkey capture box."
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsWindowViewModel _viewModel;

    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void CaptureToggleWidget_OnClick(object sender, RoutedEventArgs e)
        => _viewModel.BeginHotkeyCapture(nameof(SettingsWindowViewModel.ToggleWidgetHotkey));

    private void CapturePushToTalk_OnClick(object sender, RoutedEventArgs e)
        => _viewModel.BeginHotkeyCapture(nameof(SettingsWindowViewModel.PushToTalkHotkey));

    private void CaptureAccept_OnClick(object sender, RoutedEventArgs e)
        => _viewModel.BeginHotkeyCapture(nameof(SettingsWindowViewModel.AcceptSuggestionHotkey));

    private void CaptureDismiss_OnClick(object sender, RoutedEventArgs e)
        => _viewModel.BeginHotkeyCapture(nameof(SettingsWindowViewModel.DismissHotkey));

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.CapturingHotkeyTarget))
        {
            return;
        }

        if (e.Key is Key.Escape)
        {
            _viewModel.CancelHotkeyCapture();
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var parts = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(NormalizeKeyName(key));
        _viewModel.TryApplyCapturedHotkey(string.Join("+", parts));
        e.Handled = true;
    }

    private static string NormalizeKeyName(Key key)
    {
        return key switch
        {
            Key.Space => "Space",
            Key.OemPeriod => ".",
            Key.OemComma => ",",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            _ when key >= Key.D0 && key <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            _ => key.ToString()
        };
    }
}
