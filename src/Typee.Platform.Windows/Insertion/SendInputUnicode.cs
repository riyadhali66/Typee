using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Insertion;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Insertion;

/// <summary>
/// Batches KEYEVENTF_UNICODE SendInput calls. Called by <c>TextInserter</c> for append/replace
/// and Shift+arrow / Ctrl+V fallbacks. Never logs the text. No prior file provided this.
/// </summary>
internal static class SendInputUnicode
{
    public static bool TrySend(string text, ILogger logger)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var units = UnicodeInputBatcher.ToCodeUnits(text);
        var batches = UnicodeInputBatcher.SplitBatches(units);
        var inputSize = Marshal.SizeOf<Input>();
        var sentBatches = 0;

        foreach (var batch in batches)
        {
            var inputs = new Input[batch.Count * 2];
            var i = 0;
            for (var c = 0; c < batch.Count; c++)
            {
                var ch = batch.Array![batch.Offset + c];
                inputs[i++] = UnicodeKey(ch, keyUp: false);
                inputs[i++] = UnicodeKey(ch, keyUp: true);
            }

            var sent = SendInput((uint)inputs.Length, inputs, inputSize);
            if (sent != inputs.Length)
            {
                var error = Marshal.GetLastWin32Error();
                logger.LogWarning(
                    "SendInput UNICODE partial/fail (sent={Sent}/{Expected}, win32={Error}, batch={Batch})",
                    sent,
                    inputs.Length,
                    error,
                    sentBatches);
                return false;
            }

            sentBatches++;
        }

        logger.LogDebug("SendInput UNICODE ok (batches={Batches}, units={Units})", sentBatches, units.Count);
        return true;
    }

    public static bool TrySendVirtualKey(ushort virtualKey, bool keyUp, ILogger logger)
    {
        var inputs = new[]
        {
            new Input
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Ki = new KeybdInput
                    {
                        WVk = virtualKey,
                        WScan = 0,
                        DwFlags = keyUp ? KeyeventfKeyup : 0,
                        Time = 0,
                        DwExtraInfo = 0
                    }
                }
            }
        };

        var sent = SendInput(1, inputs, Marshal.SizeOf<Input>());
        if (sent != 1)
        {
            logger.LogDebug("SendInput VK={Vk} keyUp={KeyUp} failed win32={Error}", virtualKey, keyUp, Marshal.GetLastWin32Error());
            return false;
        }

        return true;
    }

    public static bool TryChord(ushort modifierVk, ushort keyVk, ILogger logger)
    {
        if (!TrySendVirtualKey(modifierVk, keyUp: false, logger))
        {
            return false;
        }

        if (!TrySendVirtualKey(keyVk, keyUp: false, logger)
            || !TrySendVirtualKey(keyVk, keyUp: true, logger))
        {
            _ = TrySendVirtualKey(modifierVk, keyUp: true, logger);
            return false;
        }

        return TrySendVirtualKey(modifierVk, keyUp: true, logger);
    }

    /// <summary>Shift+arrow repeated <paramref name="count"/> times (selection fallback).</summary>
    public static bool TryShiftArrows(int count, bool right, ILogger logger)
    {
        if (count <= 0)
        {
            return true;
        }

        var arrow = (ushort)(right ? VkRight : VkLeft);
        for (var i = 0; i < count; i++)
        {
            if (!TrySendVirtualKey(VkShift, keyUp: false, logger)
                || !TrySendVirtualKey(arrow, keyUp: false, logger)
                || !TrySendVirtualKey(arrow, keyUp: true, logger)
                || !TrySendVirtualKey(VkShift, keyUp: true, logger))
            {
                logger.LogDebug("Shift+arrow fallback aborted at step={Step}/{Count}", i, count);
                return false;
            }
        }

        return true;
    }

    public static bool TryMoveCaretByArrows(int delta, ILogger logger)
    {
        if (delta == 0)
        {
            return true;
        }

        var right = delta > 0;
        var count = Math.Abs(delta);
        var arrow = (ushort)(right ? VkRight : VkLeft);
        for (var i = 0; i < count; i++)
        {
            if (!TrySendVirtualKey(arrow, keyUp: false, logger) || !TrySendVirtualKey(arrow, keyUp: true, logger))
            {
                return false;
            }
        }

        return true;
    }

    private static Input UnicodeKey(char ch, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Ki = new KeybdInput
            {
                WVk = 0,
                WScan = ch,
                DwFlags = KeyeventfUnicode | (keyUp ? KeyeventfKeyup : 0u),
                Time = 0,
                DwExtraInfo = 0
            }
        }
    };
}
