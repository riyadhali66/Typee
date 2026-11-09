using System.Diagnostics;
using Typee.Core.Abstractions;
using Typee.Core.Diagnostics;
using Typee.Platform.Windows.Keyboard;

namespace Typee.App.Services;

/// <summary>Collects live diagnostics. Never stores or returns user text.</summary>
public sealed class DiagnosticsCollector : IDiagnosticsCollector
{
    private const int MaxErrors = 20;

    private readonly KeyboardHookService _hook;
    private readonly object _gate = new();
    private readonly Queue<string> _errors = new();
    private readonly Process _process = Process.GetCurrentProcess();

    private TimeSpan _lastCpu;
    private DateTime _lastCpuSampleUtc = DateTime.UtcNow;
    private string _caretMethod = "—";
    private string? _foregroundProcess;

    public DiagnosticsCollector(KeyboardHookService hook)
    {
        _hook = hook;
        _lastCpu = _process.TotalProcessorTime;
    }

    public void RecordError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // Guard: refuse obviously long payloads that might contain pasted text.
        var safe = message.Length > 200 ? message[..200] : message;
        lock (_gate)
        {
            _errors.Enqueue($"{DateTimeOffset.UtcNow:HH:mm:ss}Z {safe}");
            while (_errors.Count > MaxErrors)
            {
                _errors.Dequeue();
            }
        }
    }

    public void NoteCaretMethod(string methodName, string? processName)
    {
        lock (_gate)
        {
            _caretMethod = string.IsNullOrWhiteSpace(methodName) ? "—" : methodName;
            _foregroundProcess = processName;
        }
    }

    public DiagnosticSnapshot Capture()
    {
        _process.Refresh();
        var now = DateTime.UtcNow;
        var cpuNow = _process.TotalProcessorTime;
        var elapsed = now - _lastCpuSampleUtc;
        double cpuPercent = 0;
        if (elapsed.TotalMilliseconds > 1)
        {
            cpuPercent = (cpuNow - _lastCpu).TotalMilliseconds
                / (elapsed.TotalMilliseconds * Environment.ProcessorCount)
                * 100.0;
        }

        _lastCpu = cpuNow;
        _lastCpuSampleUtc = now;

        string caret;
        string? process;
        string[] errors;
        lock (_gate)
        {
            caret = _caretMethod;
            process = _foregroundProcess;
            errors = _errors.ToArray();
        }

        return new DiagnosticSnapshot
        {
            CpuPercent = Math.Clamp(cpuPercent, 0, 100),
            WorkingSetBytes = _process.WorkingSet64,
            HookInstalled = _hook.IsHookInstalled,
            LastHookReinstallUtc = _hook.LastReinstallUtc,
            HookDroppedEvents = _hook.DroppedEventCount,
            CaretMethod = process is null ? caret : $"{caret} ({process})",
            ForegroundProcess = process,
            HarperStatus = "Not started (Phase 6)",
            LlmStatus = "Not started (Phase 7)",
            RecentErrors = errors
        };
    }
}
