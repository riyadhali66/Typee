using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Typee.Core.Abstractions;

namespace Typee.App.Services;

internal static class CrashDiagnostics
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
            MessageBox.Show($"Could not copy diagnostics: {ex.Message}", "Typee", MessageBoxButton.OK, MessageBoxImage.Error);
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

        builder.AppendLine($"Log file: {newestLog ?? "(none)"}");
        builder.AppendLine();
        if (newestLog is null || !File.Exists(newestLog))
        {
            builder.AppendLine("(No log lines available.)");
            return builder.ToString();
        }

        var tail = File.ReadLines(newestLog).TakeLast(50).ToList();
        foreach (var line in tail)
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }
}
