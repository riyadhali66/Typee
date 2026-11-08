using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Typee.Core.Abstractions;

namespace Typee.App.Services;

internal static partial class CrashDiagnostics
{
    private const string MarkerContent = "running";

    public static void OfferDiagnosticReportIfNeeded(IAppPaths paths)
    {
        if (!File.Exists(paths.CrashMarkerFilePath))
        {
            return;
        }

        var answer = MessageBox.Show(
            "Typee closed unexpectedly — copy diagnostic report?",
            "Typee",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var report = BuildDiagnosticReport(paths);
            Clipboard.SetText(report);
            MessageBox.Show("Diagnostic report copied to the clipboard.", "Typee", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not copy diagnostics: {ex.GetType().Name}", "Typee", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void WriteRunningMarker(IAppPaths paths)
    {
        Directory.CreateDirectory(paths.RootDirectory);
        File.WriteAllText(paths.CrashMarkerFilePath, MarkerContent);
    }

    public static void ClearMarker(IAppPaths paths)
    {
        if (File.Exists(paths.CrashMarkerFilePath))
        {
            File.Delete(paths.CrashMarkerFilePath);
        }
    }

    private static string BuildDiagnosticReport(IAppPaths paths)
    {
        var builder = new StringBuilder();
        var newestLog = Directory.Exists(paths.LogsDirectory)
            ? Directory.EnumerateFiles(paths.LogsDirectory, "typee-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        builder.AppendLine("Typee diagnostic report (no keystrokes / typed text by policy)");
        builder.AppendLine($"Log file: {newestLog ?? "(none)"}");
        builder.AppendLine();
        if (newestLog is null || !File.Exists(newestLog))
        {
            builder.AppendLine("(No log lines available.)");
            return builder.ToString();
        }

        // Only Serilog-shaped lines, truncated; strip control chars. Never invent content.
        foreach (var line in File.ReadLines(newestLog).TakeLast(50))
        {
            if (!SerilogLineRegex().IsMatch(line))
            {
                continue;
            }

            var sanitized = SanitizeLine(line);
            builder.AppendLine(sanitized);
        }

        return builder.ToString();
    }

    private static string SanitizeLine(string line)
    {
        var buffer = new StringBuilder(Math.Min(line.Length, 400));
        foreach (var ch in line)
        {
            if (buffer.Length >= 400)
            {
                buffer.Append('…');
                break;
            }

            buffer.Append(ch is >= ' ' or '\t' ? ch : ' ');
        }

        return buffer.ToString();
    }

    [GeneratedRegex(@"\[[A-Z]{3}\]", RegexOptions.CultureInvariant)]
    private static partial Regex SerilogLineRegex();
}
