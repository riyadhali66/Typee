# Typee — Progress

Source of truth: `docs/MASTER_PROMPT.md` (build spec), `docs/FEATURES_V2.md` (Phase 8F + ideas). Status markers: `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/deferred.

## Handoff

> Agents: read this first, then `git pull`, before editing.

| Field | Value |
|---|---|
| Current branch | `main` (tag `checkpoint-b`) |
| Last completed | Phases 4–8 merged; Checkpoint B docs + perf snapshot |
| Next task | User runs `docs/CHECKPOINT_B.md`; then Phase 8A |
| Half-done | None |
| Uncommitted risks | `harper-ls.exe` gitignored — run `tools/fetch-harper.ps1`; live insert/grammar soak not verified |
| Updated | 2026-11-09 (Cursor) |

## Phase status

| Phase | Status | Commit |
|---|---|---|
| 1 — Skeleton, tray, settings, logging | [x] done (audited + completed) | cfe787e → fix(phase-1) |
| 1+ — Hardening now (crash/settings/DLL) | [x] merged to main | §9 now |
| 2 — Keyboard hook + hotkeys | [x] merged; raw-input watchdog on main; soak [x] | d3251d8 |
| 3 — Caret tracking + UIA text context | [x] merged; Checkpoint A partial | fabc808 / tag `checkpoint-a` |
| 4 — Suggestion overlay | [x] merged to main | 1e6e4cc |
| 5 — SQLite storage (all tables incl. 8E/8F) | [x] merged to main | 25b01d0 |
| 6 — Harper grammar | [x] merged to main | b0929fc |
| 7 — Pipeline + LLM + consent | [x] merged to main | 3879fa5 / bd8bf98 |
| 8 — Text insertion + focus snapshot | [x] merged; Checkpoint B pending manual | 6030444 / tag `checkpoint-b` |
| 8A — Typee button + Action Menu | [ ] | |
| 8B — Smart Lists | [ ] | |
| 8C — Command Bar | [ ] | |
| 8F — Translate preview, LLM profiles, Summarize | [ ] (approved) | |
| 8D — Prompt detection + Send to AI | [ ] | |
| 8E — Snippets + custom commands | [ ] | |
| 9 — Push-to-talk voice | [ ] | |
| 10 — Polish + release | [ ] | |

Checkpoints: **A** after Phase 3 · **B** after Phase 8 · **C** after 8F · **D** final.

---

## Phase 1 — Skeleton, tray, settings, logging — DONE

Built (pre-existing, commit cfe787e): solution with 7 projects + 3 test projects, correct references and dependency rule; tray icon (H.NotifyIcon.Wpf) with Enable/Pause suggestions, Settings, Open logs, Exit; single-instance mutex `Local\Typee.SingleInstance` (second launch opens Settings on first instance); Serilog rolling logs in `%LOCALAPPDATA%\Typee\logs`; settings.json with hot-reload (FileSystemWatcher + atomic save); settings window with hotkey capture; PerMonitorV2 manifest; CI workflow; README/CONTRIBUTING/THIRD_PARTY_NOTICES/LICENSE/.env.example; `tools/fetch-harper.ps1` stub (full download lands in Phase 6 per spec).

### Audit vs spec (fixed in `fix(phase-1)`)

| Gap | Resolution |
|---|---|
| `AGENTS.md` missing | Created (rules for agents: stack, layout, privacy, build/test). |
| `docs/PROGRESS.md` missing | This file. |
| `docs/RELEASING.md` missing (layout §3) | Stub created; populated in Phase 10. |
| Settings schema incomplete vs §5: only 4 hotkeys, wrong key name (`toggleWidget`), missing `button`, `smartLists`, `promptAssist`, `snippets`, `translateTargets` | Full §5 schema in `AppSettings` + `settings.example.json` (7 hotkeys: commandBar, sendToLastAi, listStylePicker, toggleChecklist, pushToTalk, acceptSuggestion, dismiss). |
| Settings window not tabbed (spec: "tabbed; start with General + Hotkeys + LLM") | Rebuilt as TabControl (General / Hotkeys / LLM); hotkey capture for all 7 hotkeys; Alt+Space rejected (§5 rule). |
| Save from the settings window would drop sections without UI | Save now clones store's current settings and applies only edited sections. |

### Decisions

- **D1 (Phase 1 audit):** completed the §5 settings schema now rather than per-phase, because Phase 2 registers hotkeys by name (`commandBar`, …) and later phases read `smartLists`/`promptAssist` defaults. Old dev `settings.json` files with `hotkeys.toggleWidget` keep working: unknown keys are ignored, missing keys fall back to defaults.
- **D2:** `HotkeySettings.ToggleWidget` (pre-existing deviation) renamed to `CommandBar` to match §5 exactly.
- Test added: `settings.example.json` must parse into `AppSettings` (guards example/schema drift; file copied to test output).

Verified: `dotnet build -c Release` 0 warnings/0 errors; `dotnet test` green. Not verified (manual): tray icon rendering, settings window visuals, second-instance activation — needs an interactive session.

### §9 Hardening — Phase 1 scope (implement now)

- [x] §9.1 Handoff section (this file) + `CHANGELOG.md` + phase-branch workflow
- [x] §9.2 Global exception handlers → log (no user text) + tray notice + keep running when recoverable
- [x] §9.2 Background service supervisor (backoff 1s/5s/30s; give up after 5 failures / 10 min; tray warning)
- [x] §9.2 Crash marker on unclean exit; next start offer diagnostic report copy
- [x] §9.7 `schemaVersion: 1` + migration runner + unit tests
- [x] §9.7 Validate on load (hotkeys/URLs/ranges); per-field fallback + notify which fields reset
- [x] §9.7 `settings.backup.json`; restore if corrupt + notify
- [x] §9.7 Settings Export / Import (keys excluded) / Reset to defaults
- [x] §9.8 `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS)` at startup
- [x] §9.8 `%LOCALAPPDATA%\Typee` created with default per-user permissions only
- [x] §9.8 Privacy tab: Clear learned data / Delete all Typee data + storage summary (DB wipe completed in Phase 5)

---

## Phase 2 — Keyboard hook + hotkeys — DONE (pending manual soak)

Plan (files):
- `src/Typee.Core/Input/HotkeyGesture.cs` — pure parser "Ctrl+Shift+Space" → modifiers + VK; validation (no Alt+Space, no bare modifiers); normalization. Unit-tested.
- `src/Typee.Core/Input/KeyboardEvent.cs` — immutable event (vk, key-down/up, injected, timestamp).
- `src/Typee.Core/Abstractions/IKeyboardEvents.cs`, `IHotkeyService.cs` (+ `HotkeyRegistrationResult`).
- `src/Typee.Core/Typing/TypingActivityService.cs` — debounce → `TypingPaused`/`TypingResumed`; injectable `TimeProvider`. Unit-tested.
- `src/Typee.Platform.Windows/Interop/NativeMethods.cs` — LibraryImport: hook + window + hotkey APIs.
- `src/Typee.Platform.Windows/Keyboard/KeyboardHookService.cs` — WH_KEYBOARD_LL on dedicated thread with message loop; callback enqueues to bounded channel + CallNextHookEx; ignores LLKHF_INJECTED; swallow only via volatile flags.
- `src/Typee.Platform.Windows/Keyboard/HotkeyService.cs` — RegisterHotKey on a message-only window (HWND_MESSAGE); bare keys + push-to-talk tracked via LL hook (passive, no swallow, key-up for push-to-talk); conflicts surfaced.
- `src/Typee.App` — wire services; tray debug option "Show key activity" (typing/paused status only, never keys); re-register hotkeys on settings hot-reload.

Tasks:
- [x] Core: HotkeyGesture parser + tests
- [x] Core: KeyboardEvent model, IKeyboardEvents, IHotkeyService
- [x] Core: TypingActivityService + tests
- [x] Platform: NativeMethods (hook/window/hotkey P/Invoke)
- [x] Platform: KeyboardHookService (thread + loop + channel + swallow flags)
- [x] Platform: HotkeyService (RegisterHotKey + hook-observed gestures)
- [x] App: DI wiring + hot-reload re-registration
- [x] App: tray "Show key activity" debug option (status only: typing/paused)
- [x] KeyboardEvent carries flags + modifiers (+ injected=false after LL filter)
- [x] Typing debounce hot-reloads with settings
- [ ] Gate: manual soak on a real desktop (10 min fast typing, hotkeys, pause events in logs)
- [x] §9.3 Hook watchdog (`GetLastInputInfo` vs last hook event every 5 s → reinstall)
- [x] §9.3 Debug callback duration > 1 ms logging
- [x] §9.4 Sleep/resume + WTS session lock/unlock → reinstall hook + re-register hotkeys
- [x] §9.4 Explorer restart (`TaskbarCreated`) → re-add tray icon
- [x] §9.4 `pauseInFullscreen` + `SHQueryUserNotificationState` (overlays respect in Phase 4)

### Phase 2 review notes (Z.ai draft → fixes)

- **D3:** `SetWindowsHookExW` takes the hook as `nint` (via `Marshal.GetFunctionPointerForDelegate`) — `LibraryImport` cannot marshal delegate parameters.
- **D4:** Passive gestures accept side-specific modifier codes (`VK_LCONTROL`/`VK_RSHIFT`/…) — the LL hook never reports generic `VK_CONTROL`/`VK_SHIFT`/`VK_MENU`, so push-to-talk with a modifier could never fire.
- **D5:** Hook consumer invokes each `KeyEvent` subscriber in its own try/catch (one failing consumer no longer stops key events — rule 5).
- **D6:** `MessageWindow` captures *all* exceptions from queued actions so none unwinds into the native WndProc.
- **D7:** `TypingActivityService` raises events outside its lock (cross-thread re-entry deadlock) and unsubscribes on dispose.
- **D8 (§9 hook watchdog):** `GetLastInputInfo` includes mouse movement — watchdog may false-positive reinstall when the user moves the mouse without keyboard input; accepted for now; tune lag threshold or keyboard-only signal in a follow-up if soak shows noise.
- Privacy check passed: only virtual-key codes and timestamps cross the channel; logs contain hotkey ids, counts and error codes, never keys or text.
- Hotkey actions are placeholders that log "fired" until their feature phases (Command Bar, Smart Lists, Speech) land.

## §9 Hardening — tasks by phase (from MASTER_PROMPT §9)

### Now / Phase 1
- [x] Git handoff + CHANGELOG + phase branches
- [x] Crash safety (handlers, supervisor, crash marker)
- [x] Settings robustness (schemaVersion, migrate, validate, backup, import/export/reset)
- [x] Security now: SetDefaultDllDirectories; LocalAppData per-user ACLs; Privacy tab (settings/logs/marker only until Phase 5 DB)

### Phase 2
- [ ] Hook watchdog + Debug >1 ms callback log
- [ ] Sleep/resume + WTS session notifications → reinstall hook/hotkeys
- [ ] TaskbarCreated → re-add tray
- [x] pauseInFullscreen setting + SHQueryUserNotificationState (full overlay pause in Phase 4)

### Phase 3
- [ ] Display-change → refresh monitor/DPI cache (overlays later)
- [ ] `tests/Typee.TestHost` + `docs/TEST_MATRIX.md`
- [x] Per-process caret method memory in `typee.db` (process names only) — Phase 5
- [x] Clipboard full-format preserve/restore (with Phase 8 inserter)

### Phase 4
- [x] Diagnostics window (tray → Diagnostics); never show user text
- [ ] Performance budgets checked at checkpoint
- [x] Arabic/EN `.resx` + `uiLanguage`; a11y names; High Contrast; exclude Typee windows from caret/hook

### Phase 6
- [ ] On resume/unlock: verify harper-ls alive

### Phase 7 / 8F
- [ ] Credential Manager API keys; HTTPS remote baseUrl; rate/cost guard; Test connection

### Phase 8A / 8C
- [ ] Arabic RTL + keyboard-nav + AutomationProperties on Action Menu / Command Bar

### Phase 9
- [ ] On resume/unlock: re-open audio device

### Phase 10
- [ ] SHA256 + HTTPS for all downloads; Privacy Clear/Delete full
- [ ] Antivirus readiness (metadata, signing docs, VirusTotal checklist, README hook FAQ)
- [ ] Velopack installer + auto-update + GA release on `v*` tags
- [ ] Assembly metadata complete; Authenticode steps in RELEASING.md

---

## Phase 3 — Caret tracking + UIA text context — DONE (code)

Built: Core `ICaretLocator`, `ITextContextReader`, `ITargetContextGuard`, models (`CaretLocation`, `ScreenRect`, `MonitorInfo`, `TextContext`, `TextSpan`, …), `Text/TextSegmenter`, `Text/OverlayLayoutMath` + 8 unit tests. Platform: `Interop.UIAutomationClient` (namespace `Interop.UIAutomationClient`, embed types false), `UiaThread` (STA + 150 ms timeout), `CaretLocator` (GetGUIThreadInfo → TextPattern2 → selection → bounds), `UiaTextContextReader`, `UiaElementCache`, `FocusMonitor` (SetWinEventHook), `ProcessInfoService` (blocklist, elevation, password, `pauseInFullscreen` via `SHQueryUserNotificationState`). App: tray **Show caret marker** (`CaretMarkerWindow` + `CaretMarkerService`, `WS_EX_NOACTIVATE`).

Tasks:
- [x] Core: interfaces + models + TextSegmenter + tests
- [x] Platform: UIA COM reference + UiaThread executor
- [x] Platform: CaretLocator chain + tests of pure parts
- [x] Platform: UiaTextContextReader (read + select + range rects)
- [x] Platform: FocusMonitor + element cache refresh
- [x] Platform: ProcessInfoService (blocklist, password skip, elevation)
- [x] Privacy: fail-closed elevation/password; `TryGetSafeFocusedElement` same-instance re-check
- [x] App: caret marker debug window + wiring
- [x] Gate: `dotnet build` / `dotnet test` — 0 warnings, 0 errors
- [x] Checkpoint A — **partially verified** — logs clean confirmed by agent; manual app tests deferred to Checkpoint B (tag `checkpoint-a`)

### Checkpoint A — manual tests

Status: **partially verified — logs clean confirmed by agent, manual app tests deferred to Checkpoint B**.

1. Enable tray **Show caret marker**; red bar tracks caret in Notepad while typing (no focus steal). — deferred
2. Repeat in Word, Chrome textarea, Edge, VS Code editor, any simple WPF text box, WhatsApp desktop chat. — deferred
3. Move window to second monitor; marker stays aligned; test 150% display scaling. — deferred
4. Focus KeePass/elevated admin Notepad: marker hidden (skip rules). — deferred
5. Password field in browser: no marker / no context read. — deferred
6. Full-screen game or presentation with `pauseInFullscreen=true`: marker pauses. — deferred
7. Logs: no user keystrokes or document text (counts/method names only). — **confirmed** (agent)

### Phase 3 deferred (still on cross-phase list)

- Display-change → refresh monitor/DPI cache (overlays in Phase 4)
- `tests/Typee.TestHost` + `docs/TEST_MATRIX.md`
- [x] Per-process caret method memory in `typee.db` — Phase 5

## Phase 4 — Suggestion overlay — DONE (code)

Built: Core `Suggestion` / `SuggestionKind` / `SuggestionLevel`, `ISuggestionOverlay`, `SuggestionTrim` (+ tests), `DiagnosticSnapshot` / `IDiagnosticsCollector`. App: non-activating `SuggestionOverlayWindow` (`WS_EX_NOACTIVATE|TOOLWINDOW|TOPMOST`), DPI-aware placement via `OverlayLayoutMath`, ~120 ms fade, light/dark/high-contrast card, `SuggestionOverlayViewModel` (Show/Hide/IsVisible → Tab/Esc swallow), hide on Esc/outside click/focus/caret move/skip/fullscreen, trim-on-matching-prefix (display-only; host still receives keys — intentional until Phase 8 insert), Alt+1/2/3 accept/dismiss, tray **Show dummy suggestion** + **Auto dummy on pause** (`suggestions.debugDummyOnPause`), Diagnostics window (CPU/RAM/hook/caret/harper+LLM placeholders/errors — no user text), `Strings.resx` + `Strings.ar.resx` scaffold + `uiLanguage`, OwnProcess skip for Typee windows, `pauseInFullscreen` hides overlay.

Tasks:
- [x] Core: Suggestion model + overlay interface + trim helper + tests
- [x] App: overlay window (no-activate styles after HWND creation), positioning, fade, theme
- [x] App: SuggestionOverlayViewModel (Show/Hide/IsVisible → drives hook swallow flag)
- [x] Hide rules: Esc, outside click, focus change, caret moved, typing resumed (+partial-match trim)
- [x] Dummy suggestion pipeline (tray + debug setting); pause suggestions hides overlay
- [x] Diagnostics window (tray → Diagnostics); never user text
- [x] EN+AR `.resx` scaffold; AutomationProperties.Name; uiLanguage validation
- [x] Fullscreen pause for overlays; exclude OwnProcess
- [x] Gate: `dotnet build` / `dotnet test` Release — 0 warnings, 0 errors
- [ ] Gate: manual overlay beside caret in Notepad (Esc/Tab, no focus steal) — deferred to Checkpoint B

### Phase 4 deferred / notes

- [!] Display-change → refresh monitor/DPI cache + reposition overlays (still deferred; poll reposition not yet wired to WM_DISPLAYCHANGE)
- [!] Performance budgets measured at Checkpoint B (Diagnostics window is the live surface)
- [!] Trim does **not** swallow printable keys (only Tab/Esc): typed chars go to the host while the card shortens — correct for completion UX without Phase 8 insertion
- [!] `tests/Typee.TestHost` still deferred (Phase 3 list)

## Phase 5 — SQLite storage — DONE (code)

Built: `%LOCALAPPDATA%\Typee\typee.db` via `MigrationRunner` + `schema_migrations`; tables `phrases`, `completion_cache`, `user_dictionary`, `snippets`, `custom_commands`, `recent_instructions`, `action_profile_memory`, `endpoint_acknowledgments`, `caret_method_memory`. Repositories (Dapper) + `suggestions.completionCacheTtlHours` (default 168). `PhraseTrie` + `PhraseService` (match last 1–5 words; rank use_count + recency; accept-count → insert after 3). Settings tabs **My phrases** / **My dictionary** (CRUD + JSON import/export). Privacy: Clear learned data / Delete all Typee data with plain-language storage summary. `CaretLocator` persists process name + method enum only on success (and prefers remembered method).

Tasks:
- [x] Migrations table + runner; ALL tables now (incl. 8E/8F + caret_method_memory)
- [x] Repositories + settings-backed TTL; phrase trie (prefix, 1–5 words, rank by use+recency)
- [x] Settings tabs "My phrases", "My dictionary" (CRUD + import/export JSON)
- [x] Accept-count logic (increment; insert as accepted after 3 accepts)
- [x] Privacy Clear learned / Delete all wired to SQLite
- [x] Persist caret method memory from CaretLocator success path
- [x] Unit tests: migrations, phrase trie, accept-count, repositories
- [x] Gate: `dotnet build` / `dotnet test` Release — 0 warnings, 0 errors

### Phase 5 deferred / notes

- [!] Phrase learning from live Tab-accept is not wired to the overlay yet (needs Phase 7/8 suggestion accept path); `IPhraseService.RecordAcceptAsync` is ready
- [!] Completion-cache TTL purge is available via repository; periodic sweep lands with Phase 7 pipeline
- [!] Snippets / custom commands Settings UI deferred to Phase 8E (tables + repos ready)
- [!] `tests/Typee.TestHost` still deferred

## Phase 6 — Harper grammar — DONE

Built: `docs/HARPER_NOTES.md` from official LS docs + README; `tools/fetch-harper.ps1` downloads pinned `harper-ls-x86_64-pc-windows-msvc.zip`, SHA256 (`tools/harper.sha256`), copies exe + LICENSE to `runtimes/harper/` + build output; `HarperProcessSupervisor` (`--stdio`, Job Object kill-on-close, restart max 3/5 min → disable + tray); `HarperLspClient` (StreamJsonRpc initialize/initialized, didChangeConfiguration, one `untitled:typee-buffer` plaintext doc, full didChange, await matching publishDiagnostics, codeAction); `LspPositionConverter` + tests (surrogates); `GrammarService : IGrammarChecker` → `GrammarIssue` (nearest-before-caret, non-Latin skip, ignore word being typed, dictionary → SQLite file + config resend); fallback mini-checker; DI + Diagnostics harper status; SystemResumed ensure-alive; THIRD_PARTY_NOTICES synced.

### Decisions

| Topic | Choice |
|---|---|
| languageId | `plaintext` (official table; also accepts `text`) |
| User dictionary | SQLite → `%LOCALAPPDATA%\Typee\harper-user-dictionary.txt` via `userDictPath` (no inline word array in Harper config) |
| disabledRules | Mapped to `harper-ls.linters.<Name>: false` |
| Missing binary | Fallback checker; status shown in Diagnostics |

### Phase 6 deferred / notes

- [!] Live Notepad gate + kill-restart soak left for manual / Checkpoint B (unit + fallback covered)
- [!] Grammar issues not yet surfaced in overlay (needs Phase 7 pipeline)
- [!] `tests/Typee.TestHost` still deferred

## Phase 7 — Suggestion pipeline + LLM + consent — DONE

Built: `SuggestionPipelineService` + `SuggestionRouter` + `ISuggestionProvider` plugins (local phrase → completion cache → Harper grammar → optional LLM style JSON → LLM completion). `SuggestionRequestCoordinator` cancels on TypingResumed / focus / newer request; overlay only applies the latest generation. `OpenAiCompatibleClient` (SSE `/v1/chat/completions`, timeout, one retry, offline → tray tooltip; local features keep working). Prompt templates embedded from `src/Typee.Llm/Prompts/*.txt`. First-run `ConsentWindow` (local vs LLM). Credential Manager API keys (Save/Clear/Test; last 4 only; CredMan → env → none). HTTPS required for non-localhost `baseUrl`. Rate/cost guard (`maxRequestsPerMinute` default 20, optional daily cap) with tray pause notice.

### Automated coverage

| Area | Status |
|---|---|
| Router priority (L1 > L2) + failing provider skip | ✅ unit |
| Request coordinator cancellation / generation | ✅ unit |
| Typing debounce gate (pause/resume) | ✅ unit |
| Style-check JSON parse incl. malformed / fenced | ✅ unit |
| HTTPS endpoint policy | ✅ unit |
| Rate guard per-minute + daily cap | ✅ unit |
| API key resolve order + mask | ✅ unit |
| SSE client parse + one retry + embedded prompts | ✅ unit |
| Ollama live completion ~1–2 s | — manual |
| CredMan Save/Clear round-trip on a real Windows profile | — manual |

### Phase 7 deferred / notes

- [!] Live Ollama / remote HTTPS soak left for manual Checkpoint B
- [!] CredMan interop compiled + unit-covered via `MemoryApiKeyStore` + resolver; interactive Save/Clear/Test on a developer machine still recommended
- [x] Text insert on Tab — completed in Phase 8

## Phase 8 — Text insertion + focus snapshot — DONE

Built: `ITextInserter` / `IFocusSnapshotService` in Core; Platform `TextInserter` (SendInput `KEYEVENTF_UNICODE` batched via `UnicodeInputBatcher`, clipboard paste with full-format `ClipboardGuardian` save + restore after 300 ms — GDI formats skipped; on restore failure empty clipboard + warn, never leave Typee text silently); grammar replace = UIA `TrySelect` preferred + Shift+arrows fallback, then insert + caret restore; pre-insert verification (foreground hwnd + UIA RuntimeId + span/context text) via pure `InsertVerification` — abort silently on mismatch; `Suggestion.Insert` (`TextInsertPlan`) attached by providers; Tab/Alt+1/2/3 on overlay → `ApplyPlanAsync` off hook/UI thread; `Suggestions.InsertMode` (`SendInput`|`Clipboard`); unit tests for batching, verification, clipboard restore decisions, document span mapping.

### Phase 8 deferred / notes

- [!] Live insert gate (Notepad, Chrome, Word, VS Code, Arabic/emoji) deferred to Checkpoint B
- [!] GDI clipboard formats (bitmap/metafile/palette) not duplicated — text/HGLOBAL formats restored; counted in restore decision logs (no contents)
- [!] Clipboard restore always runs after 300 ms under insert lock — a user Copy during that window can be overwritten (documented tradeoff vs leaving Typee text)
- [!] Dummy tray suggestion has no `Insert` plan (accept dismisses only) — intentional
- [!] Phrase `RecordAcceptAsync` still not wired from accept (Phase 5 learning note)
- [!] `IFocusSnapshotService.TryRestore` may return true after window-only restore when caret/selection UIA fails (8C should treat carefully)

- [x] ITextInserter: SendInput KEYEVENTF_UNICODE batched; clipboard paste w/ save-restore after 300 ms
- [x] Grammar fix: select span (UIA Select preferred, Shift+arrows fallback) → insert → restore caret
- [x] Pre-insert verification (foreground + element + span text unchanged, else abort silently)
- [x] IFocusSnapshotService (window/element/selection capture + restore); tests for pure parts

## Checkpoint B — after Phase 8

Manual script: [`docs/CHECKPOINT_B.md`](CHECKPOINT_B.md). Tag: `checkpoint-b`.

### §9.5 performance budgets (agent snapshot 2026-11-09)

| Metric | Budget | Measured | Notes |
|---|---|---|---|
| Idle CPU | < 1% | not verified — manual test needed | ~0.23 s CPU in first ~8 s idle; % not sampled |
| RAM without Whisper | < 150 MB (incl. harper-ls) | **~43 MB** Typee alone | harper-ls **not attached** in this snapshot — combined RAM not verified |
| Hook callback | < 1 ms p99 | not verified — manual test needed | DEBUG warning path exists; no p99 harness |
| Overlay after pause (local) | < 50 ms | not verified — manual test needed | |
| Harper result | < 150 ms / paragraph | not verified — manual test needed | |
| Startup to tray | < 2 s | **~680 ms** | Release `Typee.exe` cold-ish start to process alive |

Automated gate at Checkpoint B: **152** tests, 0 warnings / 0 errors (Release).

### Known issues / not verified

- Checkpoint A manual matrix deferred here — see `CHECKPOINT_B.md` §A
- Live grammar overlay + Tab insert in Notepad/Word/Chrome — not verified
- Live Ollama completion soak — not verified
- CredMan Save/Clear/Test on a real profile — not verified
- Kill harper-ls → auto-restart soak — not verified
- Phrase `RecordAcceptAsync` not wired from Tab accept
- Dummy suggestion has no insert plan
- Clipboard 300 ms restore can overwrite a user Copy in that window
- `tests/Typee.TestHost` still deferred
- Compare links (gh unauthenticated): merge was direct to `main`

## Phase 8A — Typee button + Action Menu — TODO

- [ ] 24×24 non-activating button bottom-right of focused element; status dot; hide while typing fast; draggable, per-app position memory
- [ ] Action Menu (fix/improve/shorten/expand/formal/friendly/translate→/lists/prompt/send-to-AI→/snippets/my commands/pause here); applies to selection else paragraph
- [ ] Action registry wired to prompts; gate

## Phase 8B — Smart Lists — TODO

- [ ] Marker parsing (numbered/lettered/roman/bullets/checklists, keep indentation) + next-marker (numbers, letters, roman→50, nesting) + renumber + style conversion — all pure + tests
- [ ] Auto-continue on Enter (no swallow; 40 ms; UIA read prev/current line); empty item ends list; Tab/Shift+Tab indent styles
- [ ] Double-detection guard; default off in disabledProcesses + Google Docs; style picker chip + Ctrl+Alt+L; Ctrl+Alt+X toggle checklist

## Phase 8C — Command Bar — TODO

- [ ] Focus-taking bar near caret (snapshot/restore); instruction box, chips 1–9, recents, My commands
- [ ] Preview card (word diff, streaming): Enter replace / Ctrl+Enter insert / R retry / C copy / Esc cancel; Undo chip 10 s (memory only)
- [ ] actions.json + per-action template files; Fix grammar = Harper; LLM-off tooltips; recent_instructions store

## Phase 8F — Translate preview · LLM profiles · Summarize — APPROVED (after 8C)

- [ ] `llmProfiles[]` settings + Settings→LLM management; Default profile = legacy llm block
- [ ] Model dropdown in Command Bar; per-action memory in `action_profile_memory` (SQLite)
- [ ] Remote-endpoint one-time notice per profile (`endpoint_acknowledgments`), localhost exempt
- [ ] Translate side-by-side preview (RTL panes per FEATURES_V2; Enter replace / C copy / Esc)
- [ ] Summarize (any selection; copy-only default; Ctrl+Enter insert below; per-action hint row)
- [ ] Shared `maxActionInputChars` (12000): chunk+merge, partial header notice; sentence-break + surrogate-safe chunker + tests

## Phase 8D — Prompt detection + Send to AI — TODO

- [ ] Local heuristic scorer (verbs, AI addressee, spec structure, ≥15 words) + tests; chip "Looks like a prompt → Improve · Send to ▾"; suppressed inside AI targets
- [ ] Improve-as-prompt action (role/goal/context/requirements/output; preview + accept)
- [ ] Send to AI: clipboard + open/focus + paste (8 s foreground wait), Cursor openChatKeys; clipboard restore 2 s; URL prefill only where documented (>1500 chars fallback); autoSubmit off + warning; last target + Ctrl+Alt+Enter; Settings tab AI targets

## Phase 8E — Snippets & custom commands — TODO

- [ ] Snippet triggers (prefix `;`, expand on space/Enter/Tab: backspace trigger, insert body; `{date}{time}{clipboard}{cursor}`); Settings tab CRUD + import/export; Action Menu listing
- [ ] Custom commands (name + instruction + optional hotkey) in Command Bar + Action Menu; runs on selection

## Phase 9 — Push-to-talk voice — TODO

- [ ] NAudio capture (16 kHz mono 16-bit, memory only, ≤60 s, ignore <0.3 s); mic indicator overlay; Ctrl+Alt+M hold via hook key-up
- [ ] WhisperModelManager (download + SHA256 + progress window; base.en/small/multilingual; keep warm); Whisper.net transcription → ITextInserter
- [ ] Optional Harper pass (auto-apply unambiguous single-replacement fixes); gate (offline end-to-end)

## Phase 10 — Polish & release — TODO

- [ ] Start with Windows (HKCU Run `Typee`); per-app enable/disable from tray; graceful shutdown (unhook, unregister, harper shutdown/exit, dispose Whisper + DB)
- [ ] Self-contained win-x64 publish (+ harper-ls.exe + notices); `docs/RELEASING.md`; README final (features, setup, hotkey table, privacy, limitations, Harper credit); CHANGELOG v1.0.0; zip + SHA256; fresh-account test

---

## Ideas (not approved)

(extras found while building get logged here — see also FEATURES_V2 §Ideas; nothing goes into code without approval)
