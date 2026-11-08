using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using static Typee.App.Interop.NativeMethods;

namespace Typee.App.Windows;

public partial class CaretMarkerWindow : Window
{
  private const int GwlExstyle = -20;
  private const int WsExNoactivate = 0x08000000;
  private const int WsExToolwindow = 0x00000080;
  private const int WsExTopmost = 0x00000008;

  public CaretMarkerWindow()
  {
    InitializeComponent();
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

  public void MoveToPhysical(double left, double top, double width, double height, double dpiX, double dpiY)
  {
    var dipX = PhysicalToDip(left, dpiX);
    var dipY = PhysicalToDip(top, dpiY);
    Width = PhysicalToDip(width, dpiX);
    Height = PhysicalToDip(height, dpiY);
    Left = dipX;
    Top = dipY;
  }

  private static double PhysicalToDip(double physical, double dpi) =>
      dpi <= 0 ? physical : physical * 96.0 / dpi;
}
