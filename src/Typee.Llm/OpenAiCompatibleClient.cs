using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Core.Models;
using Typee.Core.Settings;

namespace Typee.Llm;

/// <summary>
/// OpenAI-compatible /v1/chat/completions client with SSE streaming, timeout, one retry.
/// Never logs prompts, completions, or API keys.
/// </summary>
public sealed class OpenAiCompatibleClient : ILlmClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ISettingsStore _settings;
    private readonly IApiKeyResolver _keys;
    private readonly IPromptTemplateProvider _prompts;
    private readonly HttpMessageHandler? _handler;
    private readonly bool _ownsHandler;
    private readonly ILogger<OpenAiCompatibleClient> _logger;
    private readonly object _gate = new();

    private string _status = "Idle";
    private bool _isOnline = true;
    private bool _disposed;

    public OpenAiCompatibleClient(
        ISettingsStore settings,
        IApiKeyResolver keys,
        IPromptTemplateProvider prompts,
        ILogger<OpenAiCompatibleClient> logger,
        HttpMessageHandler? handler = null,
        bool ownsHandler = false)
    {
        _settings = settings;
        _keys = keys;
        _prompts = prompts;
        _logger = logger;
        _handler = handler;
        _ownsHandler = ownsHandler;
    }

    public string Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public bool IsOnline
    {
        get
        {
            lock (_gate)
            {
                return _isOnline;
            }
        }
    }

    public event EventHandler? StatusChanged;

    public async Task<string> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (attempt > 0)
                {
                    _logger.LogInformation("LLM request retry attempt {Attempt}", attempt + 1);
                }

                var builder = new StringBuilder();
                await foreach (var chunk in StreamOnceAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    builder.Append(chunk);
                }

                SetOnline("Online");
                return builder.ToString();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        SetOffline(DescribeError(lastError));
        throw lastError ?? new InvalidOperationException("LLM request failed.");
    }

    public async IAsyncEnumerable<string> StreamCompleteAsync(
        LlmCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming exposes the first attempt; CompleteAsync applies the one-retry policy.
        await foreach (var chunk in StreamOnceAsync(request, cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }

        SetOnline("Online");
    }

    public async Task<LlmConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var system = _prompts.GetTemplate("connection-test-system");
            var user = _prompts.GetTemplate("connection-test-user");
            var reply = await CompleteAsync(
                new LlmCompletionRequest
                {
                    SystemPrompt = system,
                    UserPrompt = user,
                    MaxTokens = 8,
                    Temperature = 0
                },
                cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var ok = reply.Contains("OK", StringComparison.OrdinalIgnoreCase);
            return new LlmConnectionTestResult
            {
                Success = ok,
                Message = ok ? "Connection OK" : "Unexpected response",
                Latency = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "LLM connection test failed");
            return new LlmConnectionTestResult
            {
                Success = false,
                Message = "Connection failed",
                Latency = sw.Elapsed
            };
        }
    }

    private async IAsyncEnumerable<string> StreamOnceAsync(
        LlmCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureNotDisposed();

        var settings = _settings.Current.Llm;
        if (!LlmEndpointPolicy.TryValidateBaseUrl(settings.BaseUrl, out var urlError))
        {
            SetOffline($"Invalid base URL");
            throw new InvalidOperationException(urlError);
        }

        using var client = CreateClient(settings);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(500, settings.TimeoutMs)));

        var body = new ChatCompletionRequest
        {
            Model = settings.Model,
            Stream = true,
            Temperature = request.Temperature ?? settings.Temperature,
            MaxTokens = request.MaxTokens ?? settings.MaxTokens,
            Messages =
            [
                new ChatMessage { Role = "system", Content = request.SystemPrompt },
                new ChatMessage { Role = "user", Content = request.UserPrompt }
            ]
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        var key = _keys.Resolve(IApiKeyStore.DefaultProfile, settings.ApiKeyEnvVar, out _);
        if (!string.IsNullOrEmpty(key))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            SetOffline("Offline");
            throw;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LLM HTTP {StatusCode}", (int)response.StatusCode);
                throw new HttpRequestException($"LLM HTTP {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                timeoutCts.Token.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0 || line.StartsWith(':'))
                {
                    continue;
                }

                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                var payload = line["data:".Length..].Trim();
                if (payload is "[DONE]")
                {
                    yield break;
                }

                var delta = TryReadDelta(payload);
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return delta;
                }
            }
        }
    }

    private HttpClient CreateClient(LlmSettings settings)
    {
        var client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);

        var baseUrl = settings.BaseUrl.Trim().TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        client.Timeout = Timeout.InfiniteTimeSpan; // per-request CancelAfter
        return client;
    }

    private static string? TryReadDelta(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }

            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var msgContent)
                && msgContent.ValueKind == JsonValueKind.String)
            {
                return msgContent.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private void SetOnline(string status)
    {
        var changed = false;
        lock (_gate)
        {
            if (!_isOnline || !string.Equals(_status, status, StringComparison.Ordinal))
            {
                _isOnline = true;
                _status = status;
                changed = true;
            }
        }

        if (changed)
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetOffline(string status)
    {
        var changed = false;
        lock (_gate)
        {
            if (_isOnline || !string.Equals(_status, status, StringComparison.Ordinal))
            {
                _isOnline = false;
                _status = status;
                changed = true;
            }
        }

        if (changed)
        {
            _logger.LogWarning("LLM offline: {Status}", status);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string DescribeError(Exception? ex) => ex switch
    {
        HttpRequestException => "HTTP error / offline",
        TaskCanceledException => "Timed out",
        OperationCanceledException => "Cancelled",
        _ => "Error"
    };

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsHandler)
        {
            _handler?.Dispose();
        }
    }

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public double? Temperature { get; set; }
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
        public List<ChatMessage> Messages { get; set; } = [];
    }

    private sealed class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
