namespace Typee.Core.Models;

public sealed class LlmCompletionRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
}
