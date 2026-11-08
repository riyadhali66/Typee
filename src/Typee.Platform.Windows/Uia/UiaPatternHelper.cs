using System.Runtime.InteropServices;
using Interop.UIAutomationClient;

namespace Typee.Platform.Windows.Uia;

internal static class UiaPatternHelper
{
    public static IUIAutomationTextPattern? TryGetTextPattern(IUIAutomationElement element)
    {
        try
        {
            return element.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId) as IUIAutomationTextPattern;
        }
        catch
        {
            return null;
        }
    }

    public static IUIAutomationTextPattern2? TryGetTextPattern2(IUIAutomationElement element)
    {
        try
        {
            var guid = typeof(IUIAutomationTextPattern2).GUID;
            var pattern = element.GetCurrentPatternAs(UIA_PatternIds.UIA_TextPattern2Id, ref guid);
            if (pattern == 0)
            {
                return null;
            }

            return Marshal.GetObjectForIUnknown(pattern) as IUIAutomationTextPattern2;
        }
        catch
        {
            return null;
        }
    }
}
