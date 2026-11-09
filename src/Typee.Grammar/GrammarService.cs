using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Grammar.Fallback;
using Typee.Grammar.Lsp;
using Typee.Grammar.Process;

namespace Typee.Grammar;

/// <summary>
/// <see cref="IGrammarChecker"/> backed by harper-ls, with a mini-checker fallback.
/// Never logs paragraph text.
/// </summary>
public sealed class GrammarService : IGrammarChecker
{
    private readonly HarperProcessSupervisor _supervisor;
    private readonly HarperLspClient _lsp;
    private readonly ISettingsStore _settings;
    private readonly IUserDictionaryRepository _dictionary;
    private readonly IAppPaths _paths;
    private readonly ILogger<GrammarService> _logger;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _statusGate = new();

    private string _status = "Not started";
    private bool _harperActive;
    private bool _disabled;
    private bool _useFallback;
    private string? _exePath;
    private bool _started;
    private bool _disposed;

    public GrammarService(
        HarperProcessSupervisor supervisor,
        HarperLspClient lsp,
        ISettingsStore settings,
        IUserDictionaryRepository dictionary,
        IAppPaths paths,
        ILogger<GrammarService> logger)
    {
        _supervisor = supervisor;
        _lsp = lsp;
        _settings = settings;
        _dictionary = dictionary;
        _paths = paths;
        _logger = logger;

        _supervisor.ProcessExitedUnexpectedly += OnProcessExitedUnexpectedly;
        _supervisor.GaveUp += OnGaveUp;
    }

    public string Status
    {
        get
        {
            lock (_statusGate)
            {
                return _status;
            }
        }
    }

    public bool IsHarperActive
    {
        get
        {
            lock (_statusGate)
            {
                return _harperActive;
            }
        }
    }

    public bool IsDisabled
    {
        get
        {
            lock (_statusGate)
            {
                return _disabled;
            }
        }
    }

    public event EventHandler? StatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _exePath = ResolveHarperExecutable();
            if (_exePath is null)
            {
                _useFallback = true;
                SetStatus("Fallback (harper-ls missing)", harperActive: false, disabled: false);
                _logger.LogWarning("harper-ls not found — using fallback mini-checker");
                return;
            }

            if (!_supervisor.TryStart(_exePath))
            {
                _useFallback = true;
                SetStatus("Fallback (harper-ls start failed)", harperActive: false, disabled: false);
                return;
            }

            var process = _supervisor.CurrentProcess
                ?? throw new InvalidOperationException("harper-ls process missing after start.");

            await _lsp.AttachAsync(process, cancellationToken).ConfigureAwait(false);
            await PushConfigurationAsync(cancellationToken).ConfigureAwait(false);
            _useFallback = false;
            SetStatus("Ready (harper-ls)", harperActive: true, disabled: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GrammarService start failed — falling back");
            _useFallback = true;
            SetStatus("Fallback (start error)", harperActive: false, disabled: false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task EnsureAliveAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disabled || _useFallback || _exePath is null)
            {
                return;
            }

            if (_supervisor.IsRunning && _lsp.IsConnected)
            {
                return;
            }

            _logger.LogInformation("Ensuring harper-ls alive after resume/exit");
            await _lsp.DetachAsync().ConfigureAwait(false);
            _supervisor.EnsureAliveAfterResume();

            if (!_supervisor.IsRunning)
            {
                if (!_supervisor.TryStart(_exePath))
                {
                    _useFallback = true;
                    SetStatus("Fallback (restart failed)", harperActive: false, disabled: _supervisor.HasGaveUp);
                    return;
                }
            }

            var process = _supervisor.CurrentProcess;
            if (process is null)
            {
                _useFallback = true;
                SetStatus("Fallback (no process)", harperActive: false, disabled: false);
                return;
            }

            await _lsp.AttachAsync(process, cancellationToken).ConfigureAwait(false);
            await PushConfigurationAsync(cancellationToken).ConfigureAwait(false);
            _useFallback = false;
            SetStatus("Ready (harper-ls)", harperActive: true, disabled: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnsureAlive failed — using fallback");
            _useFallback = true;
            SetStatus("Fallback (ensure-alive error)", harperActive: false, disabled: false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task<IReadOnlyList<GrammarIssue>> CheckAsync(
        string paragraph,
        int caretOffsetInParagraph,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        if (!_started)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!ScriptHeuristics.IsMostlyLatin(paragraph))
        {
            _logger.LogDebug("Skipping grammar check for mostly non-Latin paragraph (len={Len})", paragraph.Length);
            return [];
        }

        IReadOnlyList<GrammarIssue> raw;
        if (_disabled || _useFallback || !_lsp.IsConnected)
        {
            raw = FallbackMiniGrammarChecker.Check(paragraph);
        }
        else
        {
            try
            {
                var timeoutMs = Math.Clamp(_settings.Current.Grammar.RequestTimeoutMs, 50, 10_000);
                raw = await _lsp.CheckParagraphAsync(
                    paragraph,
                    TimeSpan.FromMilliseconds(timeoutMs),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Harper check failed — using fallback");
                raw = FallbackMiniGrammarChecker.Check(paragraph);
            }
        }

        var filtered = FilterIssues(paragraph, caretOffsetInParagraph, raw);
        return PrioritizeNearestBeforeCaret(filtered, caretOffsetInParagraph);
    }

    public async Task AddToDictionaryAsync(string word, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        var trimmed = word.Trim();
        await _dictionary.UpsertAsync(trimmed, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await WriteDictionaryFileAsync(cancellationToken).ConfigureAwait(false);

        if (_lsp.IsConnected && !_useFallback && !_disabled)
        {
            await PushConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Added word to user dictionary (length={Len})", trimmed.Length);
    }

    private static IReadOnlyList<GrammarIssue> FilterIssues(
        string paragraph,
        int caretOffset,
        IReadOnlyList<GrammarIssue> issues)
    {
        var (wordStart, wordLen) = ScriptHeuristics.GetWordSpanAtCaret(paragraph, caretOffset);
        if (wordLen <= 0)
        {
            return issues;
        }

        var wordEnd = wordStart + wordLen;
        return issues
            .Where(i =>
            {
                var issueEnd = i.Start + i.Length;
                var overlapsTypingWord = i.Start < wordEnd && issueEnd > wordStart;
                return !overlapsTypingWord;
            })
            .ToList();
    }

    private static IReadOnlyList<GrammarIssue> PrioritizeNearestBeforeCaret(
        IReadOnlyList<GrammarIssue> issues,
        int caretOffset)
    {
        if (issues.Count <= 1)
        {
            return issues;
        }

        return issues
            .OrderBy(i =>
            {
                if (i.Start + i.Length <= caretOffset)
                {
                    return caretOffset - (i.Start + i.Length); // nearer before caret first
                }

                if (i.Start >= caretOffset)
                {
                    return 1_000_000 + (i.Start - caretOffset);
                }

                return 500_000; // overlapping caret
            })
            .ThenBy(i => i.Start)
            .ToList();
    }

    private async Task PushConfigurationAsync(CancellationToken cancellationToken)
    {
        await WriteDictionaryFileAsync(cancellationToken).ConfigureAwait(false);
        var settings = BuildHarperSettings();
        await _lsp.ApplyConfigurationAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private JsonObject BuildHarperSettings()
    {
        var grammar = _settings.Current.Grammar;
        var dialect = string.IsNullOrWhiteSpace(grammar.Dialect) ? "American" : grammar.Dialect;

        var linters = new JsonObject();
        foreach (var rule in grammar.DisabledRules)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                continue;
            }

            linters[rule.Trim()] = false;
        }

        var obj = new JsonObject
        {
            ["dialect"] = dialect,
            ["userDictPath"] = GetDictionaryFilePath(),
            ["diagnosticSeverity"] = "hint"
        };

        if (linters.Count > 0)
        {
            obj["linters"] = linters;
        }

        return obj;
    }

    private async Task WriteDictionaryFileAsync(CancellationToken cancellationToken)
    {
        var path = GetDictionaryFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        IReadOnlyList<DictionaryWord> words;
        try
        {
            words = await _dictionary.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read user dictionary for Harper sync");
            words = [];
        }

        var lines = words
            .Select(w => w.Word.Trim())
            .Where(w => w.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(w => w, StringComparer.OrdinalIgnoreCase);

        await File.WriteAllLinesAsync(path, lines, cancellationToken).ConfigureAwait(false);
    }

    private string GetDictionaryFilePath() =>
        Path.Combine(_paths.RootDirectory, "harper-user-dictionary.txt");

    private string? ResolveHarperExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "runtimes", "harper", "harper-ls.exe"),
            Path.Combine(AppContext.BaseDirectory, "harper-ls.exe"),
            Path.Combine(
                Path.GetDirectoryName(typeof(GrammarService).Assembly.Location) ?? AppContext.BaseDirectory,
                "runtimes",
                "harper",
                "harper-ls.exe")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private void OnProcessExitedUnexpectedly(object? sender, string reason)
    {
        _logger.LogWarning("harper-ls exited unexpectedly ({Reason}) — scheduling reconnect", reason);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500).ConfigureAwait(false);
                await EnsureAliveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-restart after harper-ls exit failed");
            }
        });
    }

    private void OnGaveUp(object? sender, string reason)
    {
        _useFallback = true;
        SetStatus($"Disabled ({reason})", harperActive: false, disabled: true);
        _logger.LogError("Grammar disabled after harper-ls restart budget exhausted");
    }

    private void SetStatus(string status, bool harperActive, bool disabled)
    {
        lock (_statusGate)
        {
            _status = status;
            _harperActive = harperActive;
            _disabled = disabled;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _supervisor.ProcessExitedUnexpectedly -= OnProcessExitedUnexpectedly;
        _supervisor.GaveUp -= OnGaveUp;

        try
        {
            await _lsp.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        _supervisor.Dispose();
        _lifecycle.Dispose();
    }
}
