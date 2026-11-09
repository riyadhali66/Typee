using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Typee.Grammar.Process;

/// <summary>
/// Owns the hidden harper-ls --stdio child, bound to a Job Object.
/// Restart budget: max 3 exits within 5 minutes, then give up.
/// </summary>
public sealed class HarperProcessSupervisor : IDisposable
{
    public const int MaxRestartsInWindow = 3;
    public static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(5);

    private readonly ILogger<HarperProcessSupervisor> _logger;
    private readonly object _gate = new();
    private readonly List<DateTimeOffset> _exitTimes = [];
    private JobObject? _job;
    private System.Diagnostics.Process? _process;
    private bool _disposed;
    private bool _gaveUp;
    private string? _executablePath;

    public HarperProcessSupervisor(ILogger<HarperProcessSupervisor> logger)
    {
        _logger = logger;
    }

    public event EventHandler<string>? ProcessExitedUnexpectedly;
    public event EventHandler<string>? GaveUp;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _process is { HasExited: false };
            }
        }
    }

    public bool HasGaveUp
    {
        get
        {
            lock (_gate)
            {
                return _gaveUp;
            }
        }
    }

    public System.Diagnostics.Process? CurrentProcess
    {
        get
        {
            lock (_gate)
            {
                return _process is { HasExited: false } ? _process : null;
            }
        }
    }

    public bool TryStart(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (_gaveUp)
            {
                return false;
            }

            if (_process is { HasExited: false })
            {
                return true;
            }

            if (!File.Exists(executablePath))
            {
                _logger.LogWarning("harper-ls executable missing at configured path");
                return false;
            }

            _executablePath = executablePath;
            return StartCore_NoLock(executablePath);
        }
    }

    public bool TryRestartIfNeeded()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (_gaveUp || string.IsNullOrWhiteSpace(_executablePath))
            {
                return false;
            }

            if (_process is { HasExited: false })
            {
                return true;
            }

            return StartCore_NoLock(_executablePath);
        }
    }

    public void EnsureAliveAfterResume()
    {
        lock (_gate)
        {
            if (_gaveUp)
            {
                _logger.LogInformation("Harper supervisor still disabled after resume (restart budget exhausted)");
                return;
            }

            if (string.IsNullOrWhiteSpace(_executablePath))
            {
                return;
            }

            if (_process is { HasExited: false })
            {
                return;
            }

            _ = StartCore_NoLock(_executablePath);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            TearDownProcess_NoLock();
        }
    }

    private bool StartCore_NoLock(string executablePath)
    {
        TearDownProcess_NoLock();

        try
        {
            _job ??= new JobObject();

            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--stdio",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            };

            var process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!process.Start())
            {
                process.Dispose();
                _logger.LogError("Failed to start harper-ls process");
                return false;
            }

            try
            {
                _job.Assign(process);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to assign harper-ls to Job Object — continuing without kill-on-close");
            }

            process.Exited += OnProcessExited;
            _ = DrainStderrAsync(process);

            _process = process;
            _logger.LogInformation("harper-ls started (pid={Pid})", process.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "harper-ls start failed");
            return false;
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        int? code = null;
        lock (_gate)
        {
            if (_process is not null)
            {
                try
                {
                    code = _process.ExitCode;
                }
                catch
                {
                    // ignored
                }
            }
        }

        _logger.LogWarning("harper-ls exited (code={Code})", code);

        string? giveUpReason = null;
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            _exitTimes.RemoveAll(t => now - t > RestartWindow);
            _exitTimes.Add(now);

            if (_exitTimes.Count > MaxRestartsInWindow)
            {
                _gaveUp = true;
                giveUpReason = $"exited {_exitTimes.Count} times within {RestartWindow.TotalMinutes:0} min";
            }
        }

        if (giveUpReason is not null)
        {
            _logger.LogError("Harper supervisor giving up: {Reason}", giveUpReason);
            GaveUp?.Invoke(this, giveUpReason);
            return;
        }

        ProcessExitedUnexpectedly?.Invoke(this, $"exit code {code}");
    }

    private static async Task DrainStderrAsync(System.Diagnostics.Process process)
    {
        try
        {
            // Discard stderr so the pipe never blocks. Do not log line content.
            while (!process.HasExited)
            {
                var line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }
            }
        }
        catch
        {
            // Process teardown races are expected.
        }
    }

    private void TearDownProcess_NoLock()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            process.Exited -= OnProcessExited;
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignored
                }

                try
                {
                    _ = process.WaitForExit(2000);
                }
                catch
                {
                    // ignored
                }
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_gate)
        {
            TearDownProcess_NoLock();
            _job?.Dispose();
            _job = null;
        }
    }
}
