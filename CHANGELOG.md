# Changelog

All notable changes to Typee are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Phase 8: `ITextInserter` + `IFocusSnapshotService`; SendInput UNICODE batching (Arabic/emoji-safe); clipboard paste with full-format save/restore after 300 ms (clear Typee text if restore fails); grammar span select (UIA + Shift+arrow fallback) with pre-insert focus/span verification; Tab/Alt+n accept inserts via overlay → inserter; pure unit tests for batching, verification, clipboard restore policy, span mapping.
- Phase 7: suggestion pipeline (local phrase/cache → Harper L1 → optional LLM style JSON → L2 completion); `ISuggestionProvider` plugin hook; cancellation coordinator; `OpenAiCompatibleClient` SSE + retry + offline tray; prompt templates; first-run consent; Credential Manager API keys (Save/Clear/Test, last-4 UI); HTTPS non-localhost guard; per-profile rate/cost guard; overlay wired to pipeline (dummy remains debug-only).
- Phase 6: Harper grammar — `docs/HARPER_NOTES.md`; `tools/fetch-harper.ps1` (pin + SHA256); `HarperProcessSupervisor` (stdio + Job Object, restart budget 3/5 min); `HarperLspClient` (StreamJsonRpc, one virtual `plaintext` doc, diagnostics + code actions); LSP UTF-16 position conversion; `GrammarService` with nearest-before-caret / non-Latin skip / word-being-typed ignore / dictionary sync; fallback mini-checker; Diagnostics harper status; resume reconnect.
- Phase 5: SQLite `typee.db` migrations + repositories (phrases, completion_cache TTL, dictionary, snippets, custom commands, recent instructions, action profile memory, endpoint acknowledgments, caret method memory); `PhraseService` prefix trie + accept-count learning; Settings **My phrases** / **My dictionary**; Privacy Clear learned / Delete all wired to DB; caret method persistence (process name + method only).
- Phase 4: floating suggestion overlay (non-activating, DPI-aware, theme-aware, 120 ms fade); `SuggestionOverlayViewModel` Tab/Esc swallow; hide rules + prefix trim; tray dummy suggestion + Diagnostics window; EN/AR `.resx` scaffold; `uiLanguage` validation; OwnProcess skip.
- Phase 3: `ICaretLocator` / `ITextContextReader` with UIA TextPattern + Win32 fallbacks; `ProcessInfoService` skips (blocklist, elevated, password, fullscreen); tray debug **Show caret marker**; `TextSegmenter` + layout math unit tests.
- §9 hardening (now): crash marker + diagnostic clipboard offer; global exception handlers with tray notifications; `ServiceSupervisor` with 1s/5s/30s backoff for the keyboard hook.
- Settings `schemaVersion` 1, migrator/validator, `settings.backup.json`, import/export/reset; Privacy tab (clear learned / delete all including typee.db).
- `SetDefaultDllDirectories` at app startup; hook watchdog (Raw Input keyboard timestamps); system resume + `TaskbarCreated` handling.
- Hardening & Release Readiness spec (`docs/MASTER_PROMPT.md` §9): Credential Manager keys, Velopack installer plan, Arabic/a11y, test harness (deferred).
- Agent handoff section in `docs/PROGRESS.md`.

### Added
- Brand logo pack: app/tray icons (theme + paused), window icons, `docs/brand/` (SVG, GitHub social, README header).
- Phase 4: suggestion overlay (no-activate), Diagnostics window, EN/AR `.resx` scaffold.
- Phase 5: SQLite `typee.db` migrations, phrase trie, Privacy wipe, caret method memory.
- Phase 6: Harper LSP (`docs/HARPER_NOTES.md`, Job Object supervisor, fallback checker).
- Phase 7: suggestion pipeline, OpenAI-compatible client, consent, Credential Manager keys, HTTPS + rate guard.
- Phase 8: text insertion (SendInput UNICODE + clipboard restore), focus snapshot, pre-insert verify.
- Checkpoint B manual script: `docs/CHECKPOINT_B.md`.

### Fixed
- Phase 3 privacy: elevation unknown/missing PID, password property unknown/null, and mid-Invoke focus TOCTOU all fail closed via `TryGetSafeFocusedElement`.
- Hook watchdog: Raw Input keyboard sink (`RIDEV_INPUTSINK`) timestamps only — replaces `GetLastInputInfo` mouse false positives.
- Hook watchdog: skip reinstall when foreground is elevated, secure desktop is active, or the session is locked (LL hook cannot see those keys).

### Changed
- Phase workflow: work on `phase/<n>-<name>` branches; push after every phase commit; tag checkpoints.

## [0.2.0] — 2026-11-08

### Added
- Phase 2: global low-level keyboard hook on dedicated message-loop thread, hotkey registration (`RegisterHotKey` + passive push-to-talk), typing pause/resume activity, tray "Show key activity" debug.

### Fixed
- Hook channel payload includes flags and modifiers; debounce hot-reloads with settings.

## [0.1.0] — 2026-11-01

### Added
- Phase 1 skeleton: tray app, single-instance mutex, settings.json + hot-reload, Serilog logs, settings UI, docs, CI.
