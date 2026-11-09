using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Core.Models;
using Typee.Core.Settings;
using Typee.Llm;

namespace Typee.Llm.Tests;

public sealed class OpenAiCompatibleClientTests
{
    [Fact]
    public async Task CompleteAsync_ParsesSseStream()
    {
        var sse = """
                  data: {"choices":[{"delta":{"content":"Hello"}}]}

                  data: {"choices":[{"delta":{"content":" world"}}]}

                  data: [DONE]

                  """;
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        });

        var settings = new FakeSettingsStore(AppSettings.CreateDefault());
        settings.Current.Llm.BaseUrl = "http://localhost:11434/v1";
        var client = new OpenAiCompatibleClient(
            settings,
            new ApiKeyResolver(new MemoryApiKeyStore()),
            new PromptTemplateProvider(),
            NullLogger<OpenAiCompatibleClient>.Instance,
            handler);

        var text = await client.CompleteAsync(new LlmCompletionRequest
        {
            SystemPrompt = "sys",
            UserPrompt = "user"
        });

        Assert.Equal("Hello world", text);
        Assert.True(client.IsOnline);
    }

    [Fact]
    public async Task CompleteAsync_RetriesOnce_ThenSucceeds()
    {
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new HttpRequestException("boom");
            }

            var sse = "data: {\"choices\":[{\"delta\":{\"content\":\"OK\"}}]}\n\ndata: [DONE]\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            };
        });

        var settings = new FakeSettingsStore(AppSettings.CreateDefault());
        var client = new OpenAiCompatibleClient(
            settings,
            new ApiKeyResolver(new MemoryApiKeyStore()),
            new PromptTemplateProvider(),
            NullLogger<OpenAiCompatibleClient>.Instance,
            handler);

        var text = await client.CompleteAsync(new LlmCompletionRequest
        {
            SystemPrompt = "sys",
            UserPrompt = "user"
        });

        Assert.Equal("OK", text);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void PromptTemplates_AreEmbedded()
    {
        var prompts = new PromptTemplateProvider();
        Assert.Contains("STRICT JSON", prompts.GetTemplate("style-check-system"), StringComparison.Ordinal);
        Assert.Contains("{{paragraph}}", prompts.GetTemplate("style-check-user"), StringComparison.Ordinal);
        Assert.Contains("15 words", prompts.GetTemplate("completion-system"), StringComparison.Ordinal);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public FakeSettingsStore(AppSettings current) => Current = current;
        public string SettingsPath => "test-settings.json";
        public AppSettings Current { get; private set; }
        public IReadOnlyList<string> LastLoadNotices { get; } = [];
        public event EventHandler<AppSettings>? SettingsChanged;
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsChanged?.Invoke(this, settings);
            return Task.CompletedTask;
        }
        public Task ExportAsync(string destinationPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ImportAsync(string sourcePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetToDefaultsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
