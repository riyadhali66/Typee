using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Typee.App.Resources;
using Typee.Core.Abstractions;
using Typee.Core.Input;
using Typee.Core.Models;
using Typee.Core.Suggestions;
using Typee.Core.Typing;

namespace Typee.App.ViewModels;

public sealed partial class SuggestionOverlayViewModel : ObservableObject, ISuggestionOverlay
{
    private readonly KeyboardInterceptionState _interception;
    private string _remainingPrimary = string.Empty;

    public SuggestionOverlayViewModel(KeyboardInterceptionState interception)
    {
        _interception = interception;
    }

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _prefix = string.Empty;

    [ObservableProperty]
    private string _highlight = string.Empty;

    [ObservableProperty]
    private string _suffix = string.Empty;

    [ObservableProperty]
    private string _hint = string.Empty;

    [ObservableProperty]
    private string _wordmark = string.Empty;

    [ObservableProperty]
    private FlowDirection _flowDirection = FlowDirection.LeftToRight;

    public ObservableCollection<string> Options { get; } = [];

    public Suggestion? Current { get; private set; }

    public string RemainingPrimary => _remainingPrimary;

    public event EventHandler? VisibilityChanged;

    public event EventHandler? Accepted;

    public void Show(Suggestion suggestion)
    {
        Current = suggestion;
        _remainingPrimary = suggestion.PrimaryText;
        Header = ResolveHeader(suggestion);
        Prefix = suggestion.Prefix;
        Highlight = string.IsNullOrEmpty(suggestion.Highlight) ? suggestion.PrimaryText : suggestion.Highlight;
        Suffix = suggestion.Suffix;
        Hint = Strings.Suggestion_Hint;
        Wordmark = Strings.Suggestion_Wordmark;
        Options.Clear();
        foreach (var option in suggestion.Options.Take(3))
        {
            Options.Add(option);
        }

        IsVisible = true;
        SyncSwallowFlags();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Hide()
    {
        if (!IsVisible && Current is null)
        {
            return;
        }

        Current = null;
        _remainingPrimary = string.Empty;
        IsVisible = false;
        Options.Clear();
        SyncSwallowFlags();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles a key while the overlay is visible. Returns true if the key was consumed
    /// for overlay UX (Esc/Tab/Alt+n/trim). Never logs the character.
    /// Printable trim lets host-app keystrokes through (intentional); only Tab/Esc are swallowed.
    /// </summary>
    public bool HandleKey(int virtualKey, KeyModifiers modifiers)
    {
        if (!IsVisible || Current is null)
        {
            return false;
        }

        if (virtualKey == VirtualKeys.Escape)
        {
            Hide();
            return true;
        }

        if (virtualKey == VirtualKeys.Tab)
        {
            Accepted?.Invoke(this, EventArgs.Empty);
            Hide();
            return true;
        }

        // Alt+1/2/3 selects a grammar option (Phase 4: accept/dismiss only; insert is Phase 8).
        if ((modifiers & KeyModifiers.Alt) != 0 && virtualKey is >= 0x31 and <= 0x33)
        {
            var index = virtualKey - 0x31;
            if (index < Options.Count)
            {
                Accepted?.Invoke(this, EventArgs.Empty);
                Hide();
                return true;
            }
        }

        var shift = (modifiers & KeyModifiers.Shift) != 0;
        var mapped = SuggestionTrim.TryMapPrintable(virtualKey, shift);
        if (mapped is null)
        {
            return false;
        }

        if (SuggestionTrim.TryTrimPrefix(_remainingPrimary, mapped.Value, out var trimmed))
        {
            _remainingPrimary = trimmed;
            if (_remainingPrimary.Length == 0)
            {
                Hide();
            }
            else
            {
                Highlight = _remainingPrimary;
                Prefix = string.Empty;
                Suffix = string.Empty;
            }

            return true;
        }

        Hide();
        return true;
    }

    public void ApplyFlowDirection(FlowDirection direction) => FlowDirection = direction;

    private void SyncSwallowFlags()
    {
        _interception.SwallowTab = IsVisible;
        _interception.SwallowEscape = IsVisible;
    }

    private static string ResolveHeader(Suggestion suggestion) => suggestion.HeaderKey switch
    {
        nameof(Strings.Suggestion_GrammarFix) => Strings.Suggestion_GrammarFix,
        nameof(Strings.Suggestion_Completion) => Strings.Suggestion_Completion,
        nameof(Strings.Suggestion_List) => Strings.Suggestion_List,
        nameof(Strings.Suggestion_PromptAssist) => Strings.Suggestion_PromptAssist,
        _ => suggestion.HeaderKey
    };
}
