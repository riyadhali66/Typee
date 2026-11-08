# Typee — Progress

Source of truth: `docs/MASTER_PROMPT.md` (build spec), `docs/FEATURES_V2.md` (Phase 8F + ideas). Status markers: `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/deferred.

## Handoff

> Agents: read this first, then `git pull`, before editing.

| Field | Value |
|---|---|
| Current branch | `phase/hardening-now` (raw-input watchdog next commit; then `phase/3-caret-uia`) |
| Last completed | Phase 2 soak: all good; Raw Input hook watchdog (no GetLastInputInfo) |
| Next task | Push watchdog → Phase 3 caret/UIA → Checkpoint A |
| Half-done | None |
| Uncommitted risks | None |
| Updated | 2026-11-08 (Cursor) |

## Phase status

| Phase | Status | Commit |
|---|---|---|
| 1 — Skeleton, tray, settings, logging | [x] done (audited + completed) | cfe787e → fix(phase-1) |
| 1+ — Hardening now (crash/settings/DLL) | [x] | §9 now |
| 2 — Keyboard hook + hotkeys | [x] code + §9 P2 watchdog/events; soak [ ] | 956ce66 |
| 3 — Caret tracking + UIA text context | [ ] | |
| 4 — Suggestion overlay | [ ] | |
| 5 — SQLite storage (all tables incl. 8E/8F) | [ ] | |
| 6 — Harper grammar | [ ] | |
| 7 — Pipeline + LLM + consent | [ ] | |
| 8 — Text insertion + focus snapshot | [ ] | |
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
- [x] §9.8 Privacy tab: Clear learned data / Delete all Typee data + storage summary (DB wipe deferred Phase 5)

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
- [~] §9.4 `pauseInFullscreen` setting only; `SHQueryUserNotificationState` deferred (overlays later)

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
- [ ] pauseInFullscreen setting + SHQueryUserNotificationState (full overlay pause in Phase 4)

### Phase 3
- [ ] Display-change → refresh monitor/DPI cache (overlays later)
- [ ] `tests/Typee.TestHost` + `docs/TEST_MATRIX.md`
- [ ] Per-process caret method memory in `typee.db` (process names only)
- [ ] Clipboard full-format preserve/restore (with Phase 8 inserter)

### Phase 4
- [ ] Diagnostics window (tray → Diagnostics); never show user text
- [ ] Performance budgets checked at checkpoint
- [ ] Arabic/EN `.resx` + `uiLanguage`; a11y names; High Contrast; exclude Typee windows from caret/hook

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

## Phase 3 — Caret tracking + UIA text context — TODO

Plan (files):
- Core: `ICaretLocator`, `ITextContextReader`, models (`CaretRect`, `TextContext`, `TextSpan`), `Text/TextSegmenter.cs` (sentence/paragraph/line split, pure).
- Platform: COM reference `UIAutomationClient`; `Uia/UiaThread` (dedicated STA thread, 150 ms per call); `Caret/CaretLocator.cs` (chain: GetGUIThreadInfo → TextPattern2.GetCaretRange → GetSelection → element rect; Chromium empty-rect fix: expand 1 char back, right edge); `Uia/UiaTextContextReader.cs`; `Uia/UiaElementCache` + `FocusMonitor` (SetWinEventHook foreground/focus); `ProcessInfoService` (process name, elevated check via token elevation); password/blocklist/elevated skips.
- App: debug tray option "Show caret marker" (polling non-activating marker window).
- Tests: TextSegmenter, rect math (DPI, clamp, flip above/below).

Tasks:
- [ ] Core: interfaces + models + TextSegmenter + tests
- [ ] Platform: UIA COM reference + UiaThread executor
- [ ] Platform: CaretLocator chain + tests of pure parts
- [ ] Platform: UiaTextContextReader (read + select + range rects)
- [ ] Platform: FocusMonitor + element cache refresh
- [ ] Platform: ProcessInfoService (blocklist, password skip, elevation)
- [ ] App: caret marker debug window + wiring
- [ ] Gate: build/test/soak; manual matrix (Notepad, Word, Chrome, Edge, VS Code, WPF app, WhatsApp; 150% DPI; second monitor)

## Phase 4 — Suggestion overlay — TODO

- [ ] Core: Suggestion model + router interfaces
- [ ] App: overlay window (no-activate styles after HWND creation), positioning (below caret, above if no room, work-area clamp, physical px → DIP via target monitor DPI), fade
- [ ] App: SuggestionOverlayViewModel (Show/Hide/IsVisible → drives hook swallow flag)
- [ ] Hide rules: Esc, outside click, focus change, caret moved, typing resumed (+partial-match trim rule)
- [ ] Dummy suggestion pipeline for manual testing; gate

## Phase 5 — SQLite storage — TODO

- [ ] Migrations table + runner; ALL tables now: phrases, completion_cache, user_dictionary, snippets, custom_commands, recent_instructions, action_profile_memory, endpoint_acknowledgments
- [ ] Repositories + settings-backed TTL; phrase trie (prefix, 1–5 words, rank by use+recency)
- [ ] Settings tabs "My phrases", "My dictionary" (CRUD + import/export JSON)
- [ ] Accept-count logic (increment; insert as accepted after 3 accepts); tests

## Phase 6 — Harper grammar — TODO

- [ ] Read harper-ls docs; summarize findings before coding (stdio flag, plain-text languageId, config keys, diagnostics + code actions)
- [ ] `tools/fetch-harper.ps1` full (download pinned win-x64 asset, checksum, copy binary + LICENSE)
- [ ] HarperProcessSupervisor (hidden stdio child, Job Object, restart max 3/5 min → disable + tray notice)
- [ ] HarperLspClient (StreamJsonRpc): initialize, didChangeConfiguration, one virtual doc, full-text didChange, await matching diagnostics (ignore stale), codeAction per diagnostic
- [ ] LSP UTF-16 position ↔ string offset conversion (surrogates) + tests
- [ ] GrammarService → GrammarIssue; nearest-before-caret priority; non-Latin skip; word-being-typed ignore; add-to-dictionary (SQLite + config resend); fallback mini-checker
- [ ] Gate incl. kill harper-ls.exe → auto-restart

## Phase 7 — Suggestion pipeline + LLM + consent — TODO

- [ ] Pipeline on TypingPaused: context read → bail rules → local (trie, cache) → L1 grammar → (optional LLM deep check) → L2 completion; L1 > L2
- [ ] Cancellation (TypingResumed/focus/newer request); only latest updates UI
- [ ] OpenAiCompatibleClient (SSE streaming, timeout, 1 retry, offline → tray tooltip); prompt templates in `Typee.Llm/Prompts/*.txt`; ISuggestionProvider
- [ ] First-run consent screen; router/debouncer/cancel/JSON-parse tests (incl. malformed)

## Phase 8 — Text insertion + focus snapshot — TODO

- [ ] ITextInserter: SendInput KEYEVENTF_UNICODE batched; clipboard paste w/ save-restore after 300 ms
- [ ] Grammar fix: select span (UIA Select preferred, Shift+arrows fallback) → insert → restore caret
- [ ] Pre-insert verification (foreground + element + span text unchanged, else abort silently)
- [ ] IFocusSnapshotService (window/element/selection capture + restore); tests for pure parts

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
