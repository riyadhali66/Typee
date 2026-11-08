using Typee.Core.Input;

namespace Typee.Core.Typing;

/// <summary>
/// The ONLY mechanism by which Typee may swallow keys: volatile flags checked inside the
/// low-level hook callback. Tab/Esc while a suggestion is visible; Tab for list indent
/// arrives in Phase 8B. Everything else always passes through to the focused app.
/// </summary>
public sealed class KeyboardInterceptionState
{
    private volatile bool _swallowTab;
    private volatile bool _swallowEscape;

    public bool SwallowTab
    {
        get => _swallowTab;
        set => _swallowTab = value;
    }

    public bool SwallowEscape
    {
        get => _swallowEscape;
        set => _swallowEscape = value;
    }

    /// <summary>Called from the hook callback — must stay branch-cheap.</summary>
    public bool ShouldSwallow(int virtualKey, bool isKeyDown) => isKeyDown && virtualKey switch
    {
        VirtualKeys.Tab => _swallowTab,
        VirtualKeys.Escape => _swallowEscape,
        _ => false
    };
}
