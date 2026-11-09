using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Typee.Core.Models;

namespace Typee.Grammar.Lsp;

/// <summary>
/// StreamJsonRpc client for a single virtual plaintext document over harper-ls stdio.
/// Never logs document text, diagnostic messages, or replacements.
/// Callers: GrammarService (Start/Check/config). No prior LSP client exists.
/// Data: LSP JSON-RPC only (no local files). User: "HarperLspClient (StreamJsonRpc)..."
/// </summary>
public sealed class HarperLspClient : IAsyncDisposable
{
    public const string DocumentUri = "untitled:typee-buffer";
    public const string LanguageId = "plaintext";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<HarperLspClient> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<int, TaskCompletionSource<PublishDiagnosticsParams>> _pendingByVersion = new();

    private JsonRpc? _rpc;
    private int _version;
    private bool _documentOpen;
    private JsonObject _harperSettings = new();
    private bool _disposed;

    public HarperLspClient(ILogger<HarperLspClient> logger)
    {
        _logger = logger;
    }

    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _rpc is { IsDisposed: false };
            }
        }
    }

    public async Task AttachAsync(System.Diagnostics.Process process, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await DetachAsync().ConfigureAwait(false);

        var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = JsonOptions };
        var handler = new HeaderDelimitedMessageHandler(
            process.StandardInput.BaseStream,
            process.StandardOutput.BaseStream,
            formatter);

        var target = new NotificationTarget(this);
        var rpc = new JsonRpc(handler);
        rpc.AddLocalRpcTarget(target);
        rpc.StartListening();

        lock (_gate)
        {
            _rpc = rpc;
            _version = 0;
            _documentOpen = false;
            _pendingByVersion.Clear();
        }

        var initParams = new
        {
            processId = Environment.ProcessId,
            clientInfo = new { name = "Typee", version = "0.6.0" },
            rootUri = (string?)null,
            capabilities = new
            {
                workspace = new
                {
                    configuration = true,
                    didChangeConfiguration = new { dynamicRegistration = false }
                },
                textDocument = new
                {
                    synchronization = new { didSave = false },
                    publishDiagnostics = new { versionSupport = true },
                    codeAction = new
                    {
                        codeActionLiteralSupport = new
                        {
                            codeActionKind = new { valueSet = new[] { "quickfix" } }
                        }
                    }
                }
            }
        };

        await rpc.InvokeWithParameterObjectAsync<object>("initialize", initParams, cancellationToken)
            .ConfigureAwait(false);
        await rpc.NotifyWithParameterObjectAsync("initialized", new { }).ConfigureAwait(false);

        _logger.LogInformation("harper-ls LSP initialized");
    }

    public async Task ApplyConfigurationAsync(JsonObject harperLsSettings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(harperLsSettings);
        var rpc = RequireRpc();

        lock (_gate)
        {
            _harperSettings = harperLsSettings;
        }

        var settings = new JsonObject
        {
            ["harper-ls"] = harperLsSettings.DeepClone()
        };

        await rpc.NotifyWithParameterObjectAsync(
            "workspace/didChangeConfiguration",
            new { settings }).ConfigureAwait(false);

        _logger.LogDebug("Sent workspace/didChangeConfiguration to harper-ls");
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<IReadOnlyList<GrammarIssue>> CheckParagraphAsync(
        string paragraph,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        var rpc = RequireRpc();

        int version;
        TaskCompletionSource<PublishDiagnosticsParams> tcs;
        lock (_gate)
        {
            _version++;
            version = _version;
            tcs = new TaskCompletionSource<PublishDiagnosticsParams>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingByVersion[version] = tcs;

            foreach (var pair in _pendingByVersion.ToArray())
            {
                if (pair.Key < version)
                {
                    pair.Value.TrySetCanceled();
                    _pendingByVersion.Remove(pair.Key);
                }
            }
        }

        try
        {
            if (!_documentOpen)
            {
                await rpc.NotifyWithParameterObjectAsync("textDocument/didOpen", new
                {
                    textDocument = new
                    {
                        uri = DocumentUri,
                        languageId = LanguageId,
                        version,
                        text = paragraph
                    }
                }).ConfigureAwait(false);
                _documentOpen = true;
            }
            else
            {
                await rpc.NotifyWithParameterObjectAsync("textDocument/didChange", new
                {
                    textDocument = new { uri = DocumentUri, version },
                    contentChanges = new[] { new { text = paragraph } }
                }).ConfigureAwait(false);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            PublishDiagnosticsParams diagnostics;
            try
            {
                diagnostics = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("harper-ls diagnostics timed out for version {Version}", version);
                return [];
            }

            if (diagnostics.Version is int reported && reported != version)
            {
                _logger.LogDebug(
                    "Ignoring stale diagnostics version {Reported} (wanted {Version})",
                    reported,
                    version);
                return [];
            }

            var issues = new List<GrammarIssue>(diagnostics.Diagnostics.Count);
            foreach (var diagnostic in diagnostics.Diagnostics)
            {
                var issue = await MapDiagnosticAsync(rpc, paragraph, diagnostic, cancellationToken)
                    .ConfigureAwait(false);
                if (issue is not null)
                {
                    issues.Add(issue);
                }
            }

            _logger.LogDebug(
                "harper-ls returned {Count} diagnostics for version {Version}",
                issues.Count,
                version);
            return issues;
        }
        finally
        {
            lock (_gate)
            {
                _pendingByVersion.Remove(version);
            }
        }
    }

    private async Task<GrammarIssue?> MapDiagnosticAsync(
        JsonRpc rpc,
        string paragraph,
        LspDiagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var start = LspPositionConverter.PositionToOffset(
            paragraph,
            diagnostic.Range.Start.Line,
            diagnostic.Range.Start.Character);
        var end = LspPositionConverter.PositionToOffset(
            paragraph,
            diagnostic.Range.End.Line,
            diagnostic.Range.End.Character);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var length = Math.Max(0, end - start);
        var replacements = new List<string>();
        var canAdd = false;
        var ruleName = diagnostic.Code?.ToString() ?? diagnostic.Source ?? "Harper";

        try
        {
            var actions = await rpc.InvokeWithParameterObjectAsync<JsonElement>(
                "textDocument/codeAction",
                new
                {
                    textDocument = new { uri = DocumentUri },
                    range = new
                    {
                        start = new
                        {
                            line = diagnostic.Range.Start.Line,
                            character = diagnostic.Range.Start.Character
                        },
                        end = new
                        {
                            line = diagnostic.Range.End.Line,
                            character = diagnostic.Range.End.Character
                        }
                    },
                    context = new
                    {
                        diagnostics = new[]
                        {
                            new
                            {
                                range = new
                                {
                                    start = new
                                    {
                                        line = diagnostic.Range.Start.Line,
                                        character = diagnostic.Range.Start.Character
                                    },
                                    end = new
                                    {
                                        line = diagnostic.Range.End.Line,
                                        character = diagnostic.Range.End.Character
                                    }
                                },
                                message = diagnostic.Message,
                                severity = diagnostic.Severity,
                                code = diagnostic.Code,
                                source = diagnostic.Source
                            }
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);

            if (actions.ValueKind == JsonValueKind.Array)
            {
                foreach (var actionElement in actions.EnumerateArray())
                {
                    var action = JsonSerializer.Deserialize<CodeAction>(actionElement.GetRawText(), JsonOptions);
                    if (action is null)
                    {
                        continue;
                    }

                    var commandName = action.Command?.Command;
                    if (string.Equals(commandName, "HarperAddToUserDict", StringComparison.Ordinal)
                        || string.Equals(commandName, "HarperAddToWSDict", StringComparison.Ordinal)
                        || string.Equals(commandName, "HarperAddToFileDict", StringComparison.Ordinal))
                    {
                        canAdd = true;
                    }

                    if (action.Edit?.Changes is null)
                    {
                        ExtractReplacementFromTitle(action.Title, replacements);
                        continue;
                    }

                    foreach (var edits in action.Edit.Changes.Values)
                    {
                        foreach (var edit in edits)
                        {
                            if (!string.IsNullOrEmpty(edit.NewText)
                                && !replacements.Contains(edit.NewText, StringComparer.Ordinal))
                            {
                                replacements.Add(edit.NewText);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "codeAction failed for a diagnostic (rule={Rule})", ruleName);
        }

        var kind = canAdd && replacements.Count == 0
            ? GrammarIssueKind.Spelling
            : GrammarIssueKind.Grammar;

        return new GrammarIssue
        {
            Start = start,
            Length = length,
            Message = diagnostic.Message,
            RuleName = ruleName,
            Replacements = replacements,
            Kind = kind,
            CanAddToDictionary = canAdd
        };
    }

    private static void ExtractReplacementFromTitle(string? title, List<string> replacements)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        const string prefix = "Replace with:";
        var idx = title.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return;
        }

        var rest = title[(idx + prefix.Length)..].Trim();
        if (rest.Length >= 2 && rest[0] == '"' && rest[^1] == '"')
        {
            rest = rest[1..^1];
        }

        if (!string.IsNullOrEmpty(rest) && !replacements.Contains(rest, StringComparer.Ordinal))
        {
            replacements.Add(rest);
        }
    }

    internal void OnPublishDiagnostics(PublishDiagnosticsParams parameters)
    {
        if (!string.Equals(parameters.Uri, DocumentUri, StringComparison.OrdinalIgnoreCase)
            && !parameters.Uri.Contains("typee-buffer", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_gate)
        {
            if (parameters.Version is int version)
            {
                if (_pendingByVersion.TryGetValue(version, out var tcs))
                {
                    tcs.TrySetResult(parameters);
                }

                return;
            }

            if (_pendingByVersion.Count == 0)
            {
                return;
            }

            var newest = _pendingByVersion.Keys.Max();
            if (_pendingByVersion.TryGetValue(newest, out var latest))
            {
                latest.TrySetResult(parameters);
            }
        }
    }

    internal JsonObject GetHarperSettingsSnapshot()
    {
        lock (_gate)
        {
            return (JsonObject)_harperSettings.DeepClone();
        }
    }

    public async Task ShutdownAsync()
    {
        JsonRpc? rpc;
        lock (_gate)
        {
            rpc = _rpc;
        }

        if (rpc is null || rpc.IsDisposed)
        {
            return;
        }

        try
        {
            await rpc.InvokeAsync("shutdown").ConfigureAwait(false);
            await rpc.NotifyAsync("exit").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "harper-ls shutdown/exit failed");
        }
    }

    public async Task DetachAsync()
    {
        JsonRpc? rpc;
        lock (_gate)
        {
            foreach (var tcs in _pendingByVersion.Values)
            {
                tcs.TrySetCanceled();
            }

            _pendingByVersion.Clear();
            rpc = _rpc;
            _rpc = null;
            _documentOpen = false;
        }

        if (rpc is null)
        {
            return;
        }

        try
        {
            rpc.Dispose();
        }
        catch
        {
            // ignored
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private JsonRpc RequireRpc()
    {
        lock (_gate)
        {
            if (_rpc is null || _rpc.IsDisposed)
            {
                throw new InvalidOperationException("harper-ls LSP client is not connected.");
            }

            return _rpc;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await ShutdownAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        await DetachAsync().ConfigureAwait(false);
    }

    private sealed class NotificationTarget(HarperLspClient owner)
    {
        [JsonRpcMethod("textDocument/publishDiagnostics")]
        public void PublishDiagnostics(PublishDiagnosticsParams parameters)
            => owner.OnPublishDiagnostics(parameters);

        [JsonRpcMethod("window/logMessage")]
        public void LogMessage(JsonElement _)
        {
        }

        [JsonRpcMethod("window/showMessage")]
        public void ShowMessage(JsonElement _)
        {
        }

        [JsonRpcMethod("workspace/configuration", UseSingleObjectParameterDeserialization = true)]
        public object?[] Configuration(JsonElement _)
        {
            return [owner.GetHarperSettingsSnapshot()];
        }
    }
}
