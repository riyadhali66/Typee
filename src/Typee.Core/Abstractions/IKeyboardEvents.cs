using Typee.Core.Input;

namespace Typee.Core.Abstractions;

/// <summary>
/// Observed, non-injected keyboard events (off the hook thread; handlers must be fast and
/// thread-safe). Injected events (SendInput/automation) are filtered out by the producer.
/// </summary>
public interface IKeyboardEvents
{
    event EventHandler<KeyboardEvent>? KeyEvent;
}
