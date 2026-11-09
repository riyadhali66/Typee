using System.Text.Json.Serialization;

namespace Typee.Grammar.Lsp;

internal sealed class LspPosition
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("character")]
    public int Character { get; set; }
}

internal sealed class LspRange
{
    [JsonPropertyName("start")]
    public LspPosition Start { get; set; } = new();

    [JsonPropertyName("end")]
    public LspPosition End { get; set; } = new();
}

internal sealed class LspDiagnostic
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public int? Severity { get; set; }

    [JsonPropertyName("code")]
    public object? Code { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

internal sealed class PublishDiagnosticsParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("diagnostics")]
    public List<LspDiagnostic> Diagnostics { get; set; } = [];
}

internal sealed class TextEdit
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("newText")]
    public string NewText { get; set; } = string.Empty;
}

internal sealed class WorkspaceEdit
{
    [JsonPropertyName("changes")]
    public Dictionary<string, List<TextEdit>>? Changes { get; set; }
}

internal sealed class LspCommand
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;
}

internal sealed class CodeAction
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("edit")]
    public WorkspaceEdit? Edit { get; set; }

    [JsonPropertyName("command")]
    public LspCommand? Command { get; set; }
}
