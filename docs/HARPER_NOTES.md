# Harper (`harper-ls`) integration notes

Sources (read before coding Phase 6; do not invent APIs):

- Official language-server docs: https://writewithharper.com/docs/integrations/language-server
- GitHub README: https://github.com/Automattic/harper (links to the same LS docs)
- Editor integrations confirming CLI/LSP shape: Neovim lspconfig (`harper-ls --stdio`), Helix discussion #135, Emacs/eglot discussion #956
- Release pin: `tools/harper.version` → Windows asset `harper-ls-x86_64-pc-windows-msvc.zip` from https://github.com/Automattic/harper/releases

License: Apache-2.0 (Automattic / Elijah Potter NOTICE). Bundle the upstream LICENSE next to `harper-ls.exe`.

---

## Process / transport

| Item | Official behavior |
|---|---|
| Stdio mode | Run `harper-ls --stdio` (Neovim/Helix/Emacs all use this). Default without the flag is TCP (`127.0.0.1:4000`) — Typee must pass `--stdio`. |
| Transport | LSP over stdio (stdin/stdout). Typee uses StreamJsonRpc with LSP header framing. |
| Text sync | Server advertises `textDocumentSync.change = 1` (**Full**): each `textDocument/didChange` sends the entire document text in `contentChanges[0].text`. |
| Capabilities of interest | `codeActionProvider`, `executeCommandProvider` (`HarperAddToUserDict`, `HarperAddToWSDict`, `HarperAddToFileDict`, `HarperIgnoreLint`, …), diagnostics via `textDocument/publishDiagnostics`. |

Typee lifecycle (product rules, not Harper): hidden child, Windows Job Object (`KILL_ON_JOB_CLOSE`), restart budget 3 / 5 min then disable + tray notice, restart check on `SystemResumed`.

---

## Document / languageId

From the supported-languages table in the official LS docs:

| Language | Language ID |
|---|---|
| Plain Text | `plaintext` **or** `text` |

Typee opens **one** virtual document (e.g. `untitled:typee-buffer`) with `languageId: "plaintext"`, then updates it with full-text `didChange` + incrementing `version`. Do not use invent language ids such as `english-prose` (seen in some editor experiments; not in the official table).

Initialize sequence used by editors:

1. `initialize` (client capabilities)
2. `initialized`
3. `textDocument/didOpen` for the virtual buffer
4. `workspace/didChangeConfiguration` with settings under the `harper-ls` key (required — server errors if the key is missing)

---

## Configuration keys

All settings are nested under a top-level `"harper-ls"` object (LSP `settings` / `workspace/didChangeConfiguration`):

```json
{
  "harper-ls": {
    "dialect": "American",
    "userDictPath": "C:/Users/.../Typee/harper-user-dictionary.txt",
    "linters": {
      "SpellCheck": true,
      "RepeatedWords": true,
      "Spaces": true
    },
    "diagnosticSeverity": "hint"
  }
}
```

### Keys Typee maps from `settings.json`

| Typee setting | Harper key | Notes from docs |
|---|---|---|
| `grammar.dialect` | `dialect` | `"American"` \| `"British"` \| `"Australian"` \| `"Canadian"` \| `"Indian"` (default American) |
| `grammar.disabledRules` | `linters.<RuleName>: false` | Linter names are PascalCase booleans (`SpellCheck`, `RepeatedWords`, `Spaces`, `SentenceCapitalization`, …). Full list: Harper rules page. Unknown names should be omitted, not invented. |
| User dictionary (SQLite → file) | `userDictPath` | Path to a **line-separated plaintext word list**. Docs also expose `workspaceDictPath`, `fileDictPath`, `ignoredLintsPath`. |

Other documented keys (available, not all wired in Phase 6): `diagnosticSeverity`, `isolateEnglish`, `maxFileLength`, `excludePatterns`, `codeActions.ForceStable`, `markdown.IgnoreLinkTitle`.

There is **no** documented inline word-array config — Typee must materialize SQLite dictionary words into the `userDictPath` file, then resend `didChangeConfiguration`.

---

## How fixes return (diagnostics + code actions)

1. **Diagnostics** — Harper pushes `textDocument/publishDiagnostics` after open/change/config. Each diagnostic has `range` (LSP positions), `message`, optional `code` / severity. Positions are LSP UTF-16 (line + character); Typee must convert ↔ .NET string offsets including surrogate pairs.
2. **Code actions** — Client requests `textDocument/codeAction` for a diagnostic’s range. Documented actions:
   - **Quick Fixes** — `Replace with: "…"` (WorkspaceEdit text edits)
   - `HarperIgnoreLint` — ignore for the session
   - `HarperAddToUserDict` / `HarperAddToWSDict` / `HarperAddToFileDict` — dictionary commands

Typee maps Quick Fix edits → `GrammarIssue.Replacements[]`. “Add to dictionary” is handled by Typee (SQLite + rewrite `userDictPath` + config resend), not by blindly executing Harper’s command alone.

Correlate diagnostics to the in-flight check by document URI + version; ignore stale versions; honor `grammar.requestTimeoutMs`.

---

## Pin / asset (fetch script)

| Item | Value |
|---|---|
| Version file | `tools/harper.version` (e.g. `v0.73.0`) |
| Windows x64 asset | `harper-ls-x86_64-pc-windows-msvc.zip` |
| URL pattern | `https://github.com/Automattic/harper/releases/download/<tag>/harper-ls-x86_64-pc-windows-msvc.zip` |
| Verify | SHA256 of the zip before extract |
| Output | `src/Typee.App/runtimes/harper/harper-ls.exe` + `LICENSE` (never commit binaries; `.gitignore` covers the folder) |

---

## Privacy / logging

Never log paragraph text, diagnostic message bodies that quote user text, or replacement strings. Log counts, rule names/IDs, versions, timeouts, and process exit codes only.
