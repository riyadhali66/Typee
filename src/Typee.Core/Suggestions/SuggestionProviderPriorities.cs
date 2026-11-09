namespace Typee.Core.Suggestions;

public static class SuggestionProviderPriorities
{
    public const int LocalPhrase = 10;
    public const int CompletionCache = 20;
    public const int Grammar = 100;
    public const int LlmStyleCheck = 110;
    public const int LlmCompletion = 200;
}
