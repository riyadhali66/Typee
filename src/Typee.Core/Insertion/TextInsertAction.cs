namespace Typee.Core.Insertion;

public enum TextInsertAction
{
    /// <summary>Type/paste at the caret without selecting a prior span.</summary>
    AppendAtCaret = 0,

    /// <summary>Select a document span, replace it, then restore caret.</summary>
    ReplaceSpan = 1
}
