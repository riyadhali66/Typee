using Microsoft.Extensions.Logging.Abstractions;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Core.Settings;
using Typee.Core.Suggestions;

namespace Typee.Core.Tests;

public sealed class SuggestionRouterTests
{
    [Fact]
    public async Task RouteAsync_ReturnsFirstNonNull_ByPriority()
    {
        var low = new StubProvider("low", priority: 200, suggestion: Make("late"));
        var high = new StubProvider("high", priority: 10, suggestion: Make("early"));
        var router = new SuggestionRouter([low, high], NullLogger<SuggestionRouter>.Instance);

        var result = await router.RouteAsync(CreateContext());
        Assert.NotNull(result);
        Assert.Equal("early", result!.Body);
    }

    [Fact]
    public async Task RouteAsync_GrammarPriority_BeatsCompletion()
    {
        var grammar = new StubProvider("grammar", SuggestionProviderPriorities.Grammar, Make("fix", SuggestionKind.GrammarFix, SuggestionLevel.Grammar));
        var completion = new StubProvider("completion", SuggestionProviderPriorities.LlmCompletion, Make("cont", SuggestionKind.Completion, SuggestionLevel.Completion));
        var router = new SuggestionRouter([completion, grammar], NullLogger<SuggestionRouter>.Instance);

        var result = await router.RouteAsync(CreateContext());
        Assert.Equal(SuggestionKind.GrammarFix, result!.Kind);
        Assert.Equal("fix", result.Body);
    }

    [Fact]
    public async Task RouteAsync_HonorsCancellation()
    {
        var slow = new SlowProvider();
        var router = new SuggestionRouter([slow], NullLogger<SuggestionRouter>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => router.RouteAsync(CreateContext(), cts.Token));
    }

    [Fact]
    public async Task RouteAsync_SkipsFailingProvider()
    {
        var bad = new ThrowingProvider();
        var good = new StubProvider("good", 50, Make("ok"));
        var router = new SuggestionRouter([bad, good], NullLogger<SuggestionRouter>.Instance);

        var result = await router.RouteAsync(CreateContext());
        Assert.Equal("ok", result!.Body);
    }

    private static SuggestionPipelineContext CreateContext() => new()
    {
        Text = new TextContext { TextBeforeCaret = "hello world enough", CurrentParagraph = "hello world enough", CaretOffset = 18 },
        Settings = AppSettings.CreateDefault(),
        Caret = null,
        LlmAllowed = true
    };

    private static Suggestion Make(
        string body,
        SuggestionKind kind = SuggestionKind.Completion,
        SuggestionLevel level = SuggestionLevel.Local) => new()
    {
        HeaderKey = "Suggestion_Completion",
        Body = body,
        Highlight = body,
        Kind = kind,
        Level = level
    };

    private sealed class StubProvider : ISuggestionProvider
    {
        private readonly Suggestion? _suggestion;
        public StubProvider(string id, int priority, Suggestion? suggestion)
        {
            Id = id;
            Priority = priority;
            _suggestion = suggestion;
        }

        public string Id { get; }
        public int Priority { get; }
        public Task<Suggestion?> TryProvideAsync(SuggestionPipelineContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_suggestion);
    }

    private sealed class SlowProvider : ISuggestionProvider
    {
        public string Id => "slow";
        public int Priority => 1;
        public Task<Suggestion?> TryProvideAsync(SuggestionPipelineContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Suggestion?>(null);
        }
    }

    private sealed class ThrowingProvider : ISuggestionProvider
    {
        public string Id => "throw";
        public int Priority => 1;
        public Task<Suggestion?> TryProvideAsync(SuggestionPipelineContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}
