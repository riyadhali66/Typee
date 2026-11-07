# AGENTS.md — rules for AI agents working in this repo

Typee is an open-source Windows typing assistant (C# / .NET 8 / WPF, tray app). The full build spec is `docs/MASTER_PROMPT.md`; V2 feature specs live in `docs/FEATURES_V2.md`; progress in `docs/PROGRESS.md`. If docs and code disagree: privacy rules win, then FEATURES_V2 (newer), then MASTER_PROMPT.

## Stack & layout

- `net8.0-windows`, x64, WPF + MVVM (CommunityToolkit.Mvvm), nullable enabled, **warnings as errors**.
- `src/Typee.App` (WPF entry, tray, overlays) · `Typee.Core` (interfaces, models, pipeline) · `Typee.Platform.Windows` (Win32/UIA) · `Typee.Grammar` (Harper LSP) · `Typee.Llm` (OpenAI-compatible client) · `Typee.Speech` (Whisper.net) · `Typee.Storage` (SQLite).
- **Dependency rule:** Core references nothing else in the solution. App references everything; every other project references Core only.

## Non-negotiables

1. **NEVER log, store or transmit raw keystrokes or typed text.** The keyboard hook tracks only events in memory (typing pauses, hotkeys, accept/dismiss, list keys, snippet triggers). Log files must never contain user text — when in doubt, log counts and IDs, not content.
2. Never read or act on password fields (UIA `IsPassword`), blocklisted processes, or elevated windows.
3. Never block typing: hook callback < 1 ms (enqueue to a channel, nothing else), slow work off the UI thread, UIA calls with timeouts on a dedicated STA thread, LLM calls cancellable.
4. Overlays never steal focus (`WS_EX_NOACTIVATE`); the Command Bar is the only focus-taking window and must snapshot/restore the target.
5. Fail safely: any subsystem failure → log + keep running; tray tooltip shows degraded state. Never crash the host app the user is typing in.
6. API keys only from environment variables named in settings — never in `settings.json`, DB, or logs.
7. Clean-room: no proprietary code, names or assets. Harper is consumed as an official prebuilt binary (Apache-2.0; keep LICENSE + THIRD_PARTY_NOTICES in sync).

## Conventions

- Win32 interop: source-generated `LibraryImport` in one `NativeMethods` class per project, `SetLastError = true`, check every return value.
- UIA via COM `Interop.UIAutomationClient` (not System.Windows.Automation), on a dedicated STA thread with timeouts.
- Every phase: build + test green, then update `docs/PROGRESS.md`. Commits: `feat(phase-X): summary`, `fix(phase-X): summary`.
- No TODO stubs in code called "done". Deferrals are recorded in PROGRESS.md.
- Verify third-party APIs against official docs before writing integration code; if unreachable, isolate behind a Core interface and note the risk.

## Build & test

```powershell
dotnet build Typee.sln -c Release   # must be 0 warnings, 0 errors
dotnet test Typee.sln -c Release
```

App data at runtime: `%LOCALAPPDATA%\Typee\` (settings.json, typee.db, logs\, models\). `harper-ls.exe` is fetched by `tools/fetch-harper.ps1` (pinned in `tools/harper.version`) — never commit binaries or models.
