using Typee.Core.Diagnostics;

namespace Typee.Core.Abstractions;

public interface IDiagnosticsCollector
{
    DiagnosticSnapshot Capture();

    /// <summary>Records a short error id/message with no user text.</summary>
    void RecordError(string message);

    void NoteCaretMethod(string methodName, string? processName);
}
