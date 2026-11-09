using Typee.Core.Suggestions;

namespace Typee.Core.Tests;

public sealed class SuggestionRequestCoordinatorTests
{
    [Fact]
    public void BeginNewRequest_CancelsPreviousToken()
    {
        using var coordinator = new SuggestionRequestCoordinator();
        var gen1 = coordinator.BeginNewRequest(out var token1);
        Assert.False(token1.IsCancellationRequested);

        var gen2 = coordinator.BeginNewRequest(out var token2);
        Assert.True(token1.IsCancellationRequested);
        Assert.False(token2.IsCancellationRequested);
        Assert.True(gen2 > gen1);
        Assert.True(coordinator.IsCurrent(gen2));
        Assert.False(coordinator.IsCurrent(gen1));
    }

    [Fact]
    public void CancelCurrent_CancelsActiveToken()
    {
        using var coordinator = new SuggestionRequestCoordinator();
        _ = coordinator.BeginNewRequest(out var token);
        coordinator.CancelCurrent();
        Assert.True(token.IsCancellationRequested);
    }
}
