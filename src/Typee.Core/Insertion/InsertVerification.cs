namespace Typee.Core.Insertion;

public enum InsertVerificationDecision
{
    Allow = 0,
    AbortFocusChanged = 1,
    AbortSpanChanged = 2,
    AbortContextChanged = 3,
    AbortMissingData = 4
}

/// <summary>Pure pre-insert verification decisions (no Win32 / no logging of text).</summary>
public static class InsertVerification
{
    public static InsertVerificationDecision VerifyFocus(FocusIdentity expected, FocusIdentity actual)
    {
        if (expected.IsEmpty || actual.IsEmpty)
        {
            return InsertVerificationDecision.AbortMissingData;
        }

        // Replace-sensitive path: require RuntimeId on both sides when either captured one.
        if ((expected.RuntimeIdKey.Length == 0) != (actual.RuntimeIdKey.Length == 0))
        {
            return InsertVerificationDecision.AbortMissingData;
        }

        return expected.Matches(actual)
            ? InsertVerificationDecision.Allow
            : InsertVerificationDecision.AbortFocusChanged;
    }

    public static InsertVerificationDecision VerifySpanText(string? expected, string? actual)
    {
        if (expected is null)
        {
            return InsertVerificationDecision.AbortMissingData;
        }

        if (actual is null)
        {
            return InsertVerificationDecision.AbortSpanChanged;
        }

        return string.Equals(expected, actual, StringComparison.Ordinal)
            ? InsertVerificationDecision.Allow
            : InsertVerificationDecision.AbortSpanChanged;
    }

    public static InsertVerificationDecision VerifyContextSuffix(string? expectedSuffix, string? textBeforeCaret)
    {
        if (string.IsNullOrEmpty(expectedSuffix))
        {
            return InsertVerificationDecision.Allow;
        }

        if (textBeforeCaret is null)
        {
            return InsertVerificationDecision.AbortContextChanged;
        }

        return textBeforeCaret.EndsWith(expectedSuffix, StringComparison.Ordinal)
            ? InsertVerificationDecision.Allow
            : InsertVerificationDecision.AbortContextChanged;
    }

    /// <summary>
    /// Combines focus + span/context checks for a plan. Span text applies to replace;
    /// context suffix applies to append.
    /// </summary>
    public static InsertVerificationDecision VerifyPlan(
        TextInsertPlan plan,
        FocusIdentity actualFocus,
        string? actualSpanText,
        string? actualTextBeforeCaret)
    {
        var focus = VerifyFocus(plan.Target, actualFocus);
        if (focus != InsertVerificationDecision.Allow)
        {
            return focus;
        }

        if (plan.Action == TextInsertAction.ReplaceSpan)
        {
            return VerifySpanText(plan.ExpectedSpanText, actualSpanText);
        }

        return VerifyContextSuffix(plan.ExpectedContextSuffix, actualTextBeforeCaret);
    }
}
