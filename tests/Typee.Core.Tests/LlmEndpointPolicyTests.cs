using Typee.Core.Llm;

namespace Typee.Core.Tests;

public sealed class LlmEndpointPolicyTests
{
    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://example.com/v1")]
    public void Allows_LocalHttp_And_RemoteHttps(string url)
    {
        Assert.True(LlmEndpointPolicy.IsAllowed(url));
        Assert.True(LlmEndpointPolicy.TryValidateBaseUrl(url, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("http://api.openai.com/v1")]
    [InlineData("http://example.com/v1")]
    [InlineData("ftp://localhost/v1")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void Rejects_RemoteHttp_And_Invalid(string url)
    {
        Assert.False(LlmEndpointPolicy.IsAllowed(url));
        Assert.False(LlmEndpointPolicy.TryValidateBaseUrl(url, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
