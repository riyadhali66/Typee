namespace Typee.Core.Insertion;

public enum TextInsertMode
{
    /// <summary>SendInput with KEYEVENTF_UNICODE (preferred; Arabic + emoji safe when batched).</summary>
    SendInput = 0,

    /// <summary>Clipboard paste with full-format save/restore after a short delay.</summary>
    ClipboardPaste = 1
}
