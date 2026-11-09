using Typee.Core.Insertion;
using Typee.Core.Models;

namespace Typee.Core.Tests.Insertion;

public sealed class InsertVerificationTests
{
    [Fact]
    public void VerifyFocus_MatchingWindowAndRuntime_Allows()
    {
        var expected = new FocusIdentity(0x100, "1.2.3");
        var actual = new FocusIdentity(0x100, "1.2.3");
        Assert.Equal(InsertVerificationDecision.Allow, InsertVerification.VerifyFocus(expected, actual));
    }

    [Fact]
    public void VerifyFocus_DifferentWindow_Aborts()
    {
        var expected = new FocusIdentity(0x100, "1.2.3");
        var actual = new FocusIdentity(0x200, "1.2.3");
        Assert.Equal(
            InsertVerificationDecision.AbortFocusChanged,
            InsertVerification.VerifyFocus(expected, actual));
    }

    [Fact]
    public void VerifyFocus_MissingRuntimeOnOneSide_AbortsMissingData()
    {
        var expected = new FocusIdentity(0x100, string.Empty);
        var actual = new FocusIdentity(0x100, "9.9");
        Assert.Equal(
            InsertVerificationDecision.AbortMissingData,
            InsertVerification.VerifyFocus(expected, actual));
    }

    [Fact]
    public void VerifyFocus_BothMissingRuntime_AllowsWindowMatch()
    {
        var expected = new FocusIdentity(0x100, string.Empty);
        var actual = new FocusIdentity(0x100, string.Empty);
        Assert.Equal(InsertVerificationDecision.Allow, InsertVerification.VerifyFocus(expected, actual));
    }

    [Fact]
    public void VerifySpanText_Mismatch_Aborts()
    {
        Assert.Equal(
            InsertVerificationDecision.AbortSpanChanged,
            InsertVerification.VerifySpanText("teh", "the"));
    }

    [Fact]
    public void VerifySpanText_Match_Allows()
    {
        Assert.Equal(
            InsertVerificationDecision.Allow,
            InsertVerification.VerifySpanText("teh", "teh"));
    }

    [Fact]
    public void VerifyContextSuffix_RequiresEnding()
    {
        Assert.Equal(
            InsertVerificationDecision.Allow,
            InsertVerification.VerifyContextSuffix("world", "hello world"));
        Assert.Equal(
            InsertVerificationDecision.AbortContextChanged,
            InsertVerification.VerifyContextSuffix("world", "hello earth"));
    }

    [Fact]
    public void VerifyPlan_Replace_RequiresSpan()
    {
        var plan = new TextInsertPlan
        {
            Action = TextInsertAction.ReplaceSpan,
            InsertText = "the",
            ReplaceSpan = new TextSpan(10, 3),
            ExpectedSpanText = "teh",
            Target = new FocusIdentity(1, "a")
        };

        Assert.Equal(
            InsertVerificationDecision.Allow,
            InsertVerification.VerifyPlan(plan, new FocusIdentity(1, "a"), "teh", null));
        Assert.Equal(
            InsertVerificationDecision.AbortSpanChanged,
            InsertVerification.VerifyPlan(plan, new FocusIdentity(1, "a"), "xxx", null));
    }

    [Fact]
    public void VerifyPlan_Append_RequiresContextSuffix()
    {
        var plan = new TextInsertPlan
        {
            Action = TextInsertAction.AppendAtCaret,
            InsertText = " next",
            ExpectedContextSuffix = "hello",
            Target = new FocusIdentity(1, "a")
        };

        Assert.Equal(
            InsertVerificationDecision.Allow,
            InsertVerification.VerifyPlan(plan, new FocusIdentity(1, "a"), null, "say hello"));
        Assert.Equal(
            InsertVerificationDecision.AbortContextChanged,
            InsertVerification.VerifyPlan(plan, new FocusIdentity(1, "a"), null, "say hi"));
    }
}
