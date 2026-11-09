using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Typee.App.ViewModels;
using Typee.Core.Text;
using static Typee.App.Interop.NativeMethods;

namespace Typee.App.Windows;

public partial class SuggestionOverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExNoactivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const double FadeMs = 120;

    public SuggestionOverlayWindow(SuggestionOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ApplyThemeColors();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            var exStyle = GetWindowLongPtr(source.Handle, GwlExstyle);
            var desired = exStyle.ToInt64() | WsExNoactivate | WsExToolwindow | WsExTopmost;
            SetWindowLongPtr(source.Handle, GwlExstyle, new IntPtr(desired));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnClosed(e);
    }

    public void PlacePhysical(double left, double top, uint dpiX, uint dpiY)
    {
        Left = OverlayLayoutMath.PhysicalPixelsToDip(left, dpiX);
        Top = OverlayLayoutMath.PhysicalPixelsToDip(top, dpiY);
    }

    public void FadeIn()
    {
        CardRoot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeMs)));
    }

    public void FadeOutAndHide(Action onCompleted)
    {
        var animation = new DoubleAnimation(CardRoot.Opacity, 0, TimeSpan.FromMilliseconds(FadeMs));
        animation.Completed += (_, _) => onCompleted();
        CardRoot.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    public bool ContainsPhysicalPoint(double physicalX, double physicalY, uint dpiX, uint dpiY)
    {
        var dipX = OverlayLayoutMath.PhysicalPixelsToDip(physicalX, dpiX);
        var dipY = OverlayLayoutMath.PhysicalPixelsToDip(physicalY, dpiY);
        return dipX >= Left && dipX <= Left + ActualWidth && dipY >= Top && dipY <= Top + ActualHeight;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            Dispatcher.BeginInvoke(ApplyThemeColors);
        }
    }

    private void ApplyThemeColors()
    {
        var highContrast = SystemParameters.HighContrast;
        var light = !highContrast && IsAppsUseLightTheme();

        if (highContrast)
        {
            CardRoot.Background = SystemColors.WindowBrush;
            CardRoot.BorderBrush = SystemColors.WindowFrameBrush;
            Foreground = SystemColors.WindowTextBrush;
            return;
        }

        if (light)
        {
            CardRoot.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            CardRoot.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
            Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
        }
        else
        {
            CardRoot.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
            CardRoot.BorderBrush = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
            Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB));
        }
    }

    private static bool IsAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i ? i != 0 : true;
        }
        catch
        {
            return true;
        }
    }
}
