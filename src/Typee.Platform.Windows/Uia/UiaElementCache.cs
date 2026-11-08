using Microsoft.Extensions.Logging;
using Interop.UIAutomationClient;

namespace Typee.Platform.Windows.Uia;

/// <summary>Caches the focused UIA element per foreground window; refreshed on focus events.</summary>
public sealed class UiaElementCache
{
    private readonly UiaThread _uiaThread;
    private readonly ILogger<UiaElementCache> _logger;
    private readonly object _gate = new();
    private nint _foregroundWindow;
    private IUIAutomationElement? _focusedElement;
    private volatile bool _dirty = true;

    public UiaElementCache(UiaThread uiaThread, ILogger<UiaElementCache> logger)
    {
        _uiaThread = uiaThread;
        _logger = logger;
    }

    public void Invalidate() => _dirty = true;

    public IUIAutomationElement? GetFocusedElement(CUIAutomation8 automation)
    {
        lock (_gate)
        {
            var hwnd = Interop.NativeMethods.GetForegroundWindow();
            if (hwnd == 0)
            {
                return null;
            }

            if (!_dirty && _focusedElement is not null && hwnd == _foregroundWindow)
            {
                return _focusedElement;
            }

            try
            {
                var element = automation.GetFocusedElement();
                _focusedElement = element;
                _foregroundWindow = hwnd;
                _dirty = false;
                return element;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetFocusedElement failed");
                _focusedElement = null;
                _foregroundWindow = 0;
                return null;
            }
        }
    }

    public void RefreshOn(UiaThread thread) =>
        thread.Invoke(automation => GetFocusedElement(automation));
}
