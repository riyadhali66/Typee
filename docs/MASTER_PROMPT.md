# TYPEE — MASTER BUILD PROMPT (for ZCode agent)

You are a senior Windows systems engineer (Win32, UI Automation, WPF, .NET 8) and a careful open-source maintainer, working as an autonomous coding agent inside this repository. You can read files, edit files and run terminal commands. Build **Typee**: an open-source Windows typing assistant. It runs in the system tray and shows a floating widget beside the text caret in ANY application, offering offline grammar fixes, smart next-thought completions, auto-numbered lists, a Cursor-style AI command bar, "send this prompt to Claude/ChatGPT/Cursor", snippets, and offline voice typing.

---

## 0. HOW YOU MUST WORK (read first, follow always)

1. **Start by inspecting the repo.**
   - If the repo is empty → start at Phase 1.
   - If a Typee solution already exists (it may have been started in another tool) → audit it against this spec, write the audit to `docs/PROGRESS.md` (what is done, what is missing, what deviates), fix gaps in completed phases first, then continue with the next unfinished phase.
2. **Create and maintain these files:**
   - `AGENTS.md` — short rules for any AI agent working in this repo (stack, layout, conventions, "never log keystrokes", build/test commands).
   - `docs/PROGRESS.md` — checklist of all phases, current status, decisions made, known issues. Update it at the end of every phase.
   - `docs/ARCHITECTURE.md` — module diagram (text), dependency rules, data flow.
3. **Phase gating:** build ONE phase at a time. At the end of each phase:
   - run `dotnet build Typee.sln -c Release` (must be 0 warnings, 0 errors) and `dotnet test`;
   - update `docs/PROGRESS.md`;
   - print a short report: what was built, what was deferred (explicitly), and a numbered list of manual tests for me;
   - then **STOP and wait** for me to reply "next phase". Do not start the next phase on your own.
4. **Plan before coding** each phase: list the files you will create/change and the interfaces involved, then implement.
5. **No fake completeness:** never leave TODO stubs in code you call done. If something is deferred, say so in the report and in PROGRESS.md.
6. **Verify third-party APIs:** for Harper, Whisper.net, H.NotifyIcon, StreamJsonRpc and UIA interop, read the official docs/README before writing integration code. Do not guess flags, config keys or method names. If you cannot access docs, say so and write the code defensively behind an interface.
7. **Fail safely everywhere:** if caret detection, Harper, the LLM, Whisper, or text insertion fails, log it and keep the app running. Never crash the host app the user is typing in, never block their typing.
8. **Small commits:** after each phase, suggest a git commit message (e.g. `feat(phase-3): caret tracking with UIA fallbacks`).
9. **Clean-room:** do not copy or imitate any proprietary product's code, names, assets or UI. Only public Windows APIs and open-source libraries, respecting their licenses.

---

## 1. BRANDING

- Name: **Typee**. Tagline: "Type smarter, anywhere."
- Tray tooltip: `Typee – typing assistant`. Offline/error variants: `Typee – LLM offline`, `Typee – grammar engine stopped`, `Typee – paused`.
- All namespaces start with `Typee.`
- Single-instance mutex: `Local\Typee.SingleInstance`
- App data: `%LOCALAPPDATA%\Typee\` (settings.json, typee.db, logs\, models\)
- Registry Run value name: `Typee`

---

## 2. TECH STACK (fixed — do not substitute)

| Area | Choice |
|---|---|
| Language / runtime | C#, .NET 8, `net8.0-windows`, x64, nullable enabled, warnings as errors |
| UI | WPF, MVVM (CommunityToolkit.Mvvm) |
| Hosting / DI | `Microsoft.Extensions.Hosting` (background services, options, logging) |
| Tray | `H.NotifyIcon.Wpf` |
| Win32 interop | `LibraryImport` source-generated P/Invoke, one `NativeMethods` class per project, `SetLastError = true`, error checks |
| UI Automation | COM `UIAutomationClient` via `Interop.UIAutomationClient` (NOT System.Windows.Automation) |
| Grammar | **Harper** (https://github.com/Automattic/harper, Apache-2.0): bundle official prebuilt `harper-ls` Windows x64 binary from Harper GitHub Releases; talk LSP over stdio with `StreamJsonRpc`. Never vendor/modify Harper source |
| LLM | Any OpenAI-compatible `/v1/chat/completions` (Ollama, vLLM, DeepSeek, OpenAI, Z.ai GLM), streaming SSE, `IHttpClientFactory` |
| Storage | SQLite via `Microsoft.Data.Sqlite` + Dapper, simple migrations table |
| Audio | `NAudio`, 16 kHz mono 16-bit PCM |
| Speech-to-text | `Whisper.net` (whisper.cpp), default `ggml-base.en.bin`, optional small/multilingual |
| Logging | `Serilog`, rolling files in `%LOCALAPPDATA%\Typee\logs` |
| Tests | xUnit (+ FluentAssertions optional) |

---

## 3. SOLUTION LAYOUT

```
Typee/
├─ Typee.sln
├─ AGENTS.md
├─ src/
│  ├─ Typee.App/                # WPF entry, tray, overlays (suggestion card, Typee button, action menu, command bar), settings window, DI root
│  ├─ Typee.Core/               # interfaces, models, suggestion pipeline, router, debouncer, action registry, list logic, prompt detection
│  ├─ Typee.Platform.Windows/   # keyboard hook, hotkeys, caret tracking, UIA text read/select, SendInput, clipboard, focus save/restore, window/process info
│  ├─ Typee.Grammar/            # Harper LSP client, process supervisor, diagnostics → GrammarIssue mapping
│  ├─ Typee.Llm/                # OpenAI-compatible client, SSE parser, prompt templates, actions/*.txt
│  ├─ Typee.Speech/             # audio capture, Whisper.net transcription, model manager
│  └─ Typee.Storage/            # SQLite schema + migrations, repositories (phrases, cache, dictionary, snippets, custom commands, history), settings store
├─ tests/
│  ├─ Typee.Core.Tests/
│  ├─ Typee.Grammar.Tests/
│  └─ Typee.Storage.Tests/
├─ tools/
│  ├─ harper.version            # pinned Harper release tag
│  └─ fetch-harper.ps1          # download pinned harper-ls win x64 asset, verify checksum, copy to build output + Harper LICENSE
├─ docs/
│  ├─ ARCHITECTURE.md
│  ├─ PROGRESS.md
│  └─ RELEASING.md
├─ settings.example.json
├─ .env.example
├─ THIRD_PARTY_NOTICES.md
├─ CONTRIBUTING.md
├─ README.md
├─ LICENSE (MIT)
└─ .github/workflows/build.yml  # windows-latest: fetch-harper.ps1, dotnet build, dotnet test
```

**Dependency rule:** `Typee.Core` references nothing else in the solution (interfaces only). App → everything; every other project → Core only.

**Interfaces to define early in Core** (even if implemented later): `IKeyboardEvents`, `IHotkeyService`, `ICaretLocator`, `ITextContextReader`, `ITextInserter`, `IFocusSnapshotService` (save/restore target window + element + selection), `IGrammarChecker`, `ILlmClient`, `ISuggestionProvider`, `IActionRegistry`, `IPhraseService`, `ISnippetService`, `ISendToAiService`, `ISpeechToText`, `ISettingsStore`.

---

## 4. PRIVACY & SAFETY (non-negotiable)

- Never log, store or transmit raw keystrokes. The keyboard hook only tracks events in memory for typing pauses, hotkeys, accept/dismiss keys, Enter/Tab for lists, and snippet triggers.
- Never read or act on password fields (UIA `IsPassword` = true).
- Per-app blocklist (process names), prefilled: `KeePassXC.exe`, `1Password.exe`, `Bitwarden.exe`, `mstsc.exe`, plus common banking apps.
- Grammar (Harper), phrases, snippets, lists and voice are 100% local. Text goes to the LLM only if an LLM is configured AND the app is not blocklisted.
- First-run **consent screen** (built by Phase 7 at the latest): explains what is local and what is sent to the LLM endpoint.
- Phrase learning stores only accepted/saved text. Settings has "Clear all learned data".
- Audio is never written to disk. Original text for Undo is kept in memory only.
- Typee never runs elevated. If the target window belongs to an elevated process (UIPI), do nothing there.
- Send-to-AI never auto-submits unless the user enables it per target.

---

## 5. SETTINGS (`%LOCALAPPDATA%\Typee\settings.json`, hot-reloaded; copy in repo as `settings.example.json`)

```json
{
  "hotkeys": {
    "commandBar": "Ctrl+Shift+Space",
    "sendToLastAi": "Ctrl+Alt+Enter",
    "listStylePicker": "Ctrl+Alt+L",
    "toggleChecklist": "Ctrl+Alt+X",
    "pushToTalk": "Ctrl+Alt+M",
    "acceptSuggestion": "Tab",
    "dismiss": "Escape"
  },
  "suggestions": {
    "enabled": true,
    "debounceMs": 500,
    "minCharsBeforeSuggest": 12,
    "maxContextChars": 1500,
    "level1GrammarEnabled": true,
    "level2CompletionEnabled": true
  },
  "grammar": {
    "engine": "harper",
    "dialect": "American",
    "llmDeepCheck": false,
    "disabledRules": [],
    "requestTimeoutMs": 400
  },
  "llm": {
    "baseUrl": "http://localhost:11434/v1",
    "model": "llama3.1:8b",
    "apiKeyEnvVar": "TYPEE_API_KEY",
    "timeoutMs": 8000,
    "maxTokens": 60,
    "temperature": 0.3
  },
  "button": { "enabled": true, "hideWhileTyping": true },
  "smartLists": {
    "enabled": true,
    "autoContinue": true,
    "tabIndent": true,
    "defaultNumberStyle": "1.",
    "defaultBullet": "•",
    "disabledProcesses": ["WINWORD.EXE", "ONENOTE.EXE", "OUTLOOK.EXE", "Notion.exe"]
  },
  "promptAssist": {
    "detectPrompts": true,
    "detectionThreshold": 0.6,
    "lastTarget": "claude-web",
    "targets": [
      { "id": "claude-web", "label": "Claude", "method": "clipboard-open", "url": "https://claude.ai/new", "autoSubmit": false },
      { "id": "chatgpt-web", "label": "ChatGPT", "method": "clipboard-open", "url": "https://chatgpt.com/", "autoSubmit": false },
      { "id": "gemini-web", "label": "Gemini", "method": "clipboard-open", "url": "https://gemini.google.com/app", "autoSubmit": false },
      { "id": "cursor", "label": "Cursor", "method": "clipboard-focus", "process": "Cursor.exe", "openChatKeys": "Ctrl+L", "autoSubmit": false }
    ]
  },
  "snippets": { "enabled": true, "triggerPrefix": ";" },
  "translateTargets": ["English", "Arabic"],
  "speech": { "model": "base.en", "language": "en", "insertMode": "SendInput" },
  "blockedProcesses": ["KeePassXC.exe", "1Password.exe", "Bitwarden.exe", "mstsc.exe"],
  "startWithWindows": false
}
```

Rules: never use Alt+Space (Windows system menu). API keys come only from the env var named in settings, never stored in the file. Map `grammar.dialect` / `disabledRules` to the exact harper-ls config keys from Harper docs. Every hotkey is conflict-checked at registration with a clear UI error.

---

## 6. PHASES

### PHASE 1 — Skeleton, tray, settings, logging
- Full solution and projects with correct references; `AGENTS.md`, `docs/*`, README, CONTRIBUTING (branching, style, how to add an LLM provider / suggestion level / quick action, how to bump Harper), THIRD_PARTY_NOTICES, MIT LICENSE, .env.example, CI workflow.
- Tray-only start; single instance (second launch opens Settings on the first instance).
- Tray menu: Enable/Pause suggestions, Settings, Open logs folder, Exit.
- Per-Monitor V2 DPI awareness (app.manifest).
- Settings window (tabbed; start with General + Hotkeys + LLM) with hotkey capture box; save + hot-reload.
**Acceptance:** tray icon + tooltip; settings save/reload/hot-reload; logs written; second instance opens Settings; Exit leaves no process.

### PHASE 2 — Global keyboard hook + hotkeys (Platform.Windows)
- `KeyboardHookService`: `SetWindowsHookEx(WH_KEYBOARD_LL)` on a dedicated thread with its own Win32 message loop; delegate held in a field; clean unhook.
- Callback < 1 ms: copy (vk, flags, modifiers, timestamp) into a `Channel<T>`, call `CallNextHookEx`. Ignore `LLKHF_INJECTED`.
- Swallowing allowed ONLY via volatile flags: Tab/Esc while a suggestion is visible; Tab for list indent (Phase 8B) when the list controller says so.
- `HotkeyService`: `RegisterHotKey` on a message-only window (`HWND_MESSAGE`). Push-to-talk (needs key-up) via the LL hook.
- Hotkey string parser ("Ctrl+Shift+Space"); registration failure shown in UI.
- `TypingActivityService` (Core): `TypingPaused` after debounce, `TypingResumed` on any key.
- Debug tray option "Show key activity" (shows only typing/paused status, never keys).
**Acceptance:** pause events logged in Notepad; hotkeys fire; 10 minutes fast typing with zero lag in other apps.

### PHASE 3 — Caret tracking + text context
- `ICaretLocator` (physical screen pixels + monitor), fallback chain:
  1. `GetGUIThreadInfo` → `rcCaret` + `ClientToScreen`
  2. UIA `TextPattern2.GetCaretRange` → `GetBoundingRectangles` (if empty, expand 1 char backward, use right edge)
  3. UIA `TextPattern.GetSelection()` first range rect
  4. Focused element `BoundingRectangle` bottom-left
- All UIA calls on a dedicated STA thread, 150 ms timeout each; cache element per window; refresh via `SetWinEventHook` (foreground + focus events).
- `ITextContextReader`: text before caret (≤ maxContextChars), current sentence, current paragraph, current line + previous line, caret offset, current selection; method to get a screen rect for any character range; method to select a character range (`TextRange.Select()`). If no TextPattern → return null (never rebuild text from keystrokes).
- Skip password fields, blocklisted processes, elevated windows.
- Debug tray option "Show caret marker".
**Acceptance:** marker correct in Notepad, Word, Chrome, Edge, VS Code, a WPF app, WhatsApp desktop; at 150% DPI and on a second monitor.

### PHASE 4 — Floating suggestion overlay (App)
- Overlay window: `WindowStyle=None`, `AllowsTransparency=True`, `ShowInTaskbar=False`, `Topmost=True`, `ShowActivated=False`; after HWND creation add `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`. Never steals focus.
- Position below caret (above if no room), clamped to work area; physical px → DIP with the target monitor DPI.
- Card: header label ("Grammar fix" / "Are you thinking this?" / "List" / "Looks like a prompt"), body with changed/new part highlighted, up to 3 replacement options for grammar (Tab = first, Alt+1/2/3), hint row "Tab accept · Esc dismiss", small "Typee" wordmark. Light/dark from Windows theme. 120 ms fade.
- `SuggestionOverlayViewModel`: `Show(Suggestion)`, `Hide()`, `IsVisible` (drives the hook swallow flag).
- Hide on Esc, outside click, focus change, caret moved away, typing resumed (unless typed chars match the suggestion start → trim instead).
**Acceptance:** dummy suggestion beside caret; typing never interrupted; Esc dismisses.

### PHASE 5 — Local storage, phrase cache, fast completions (Storage + Core)
- `%LOCALAPPDATA%\Typee\typee.db` with migrations. Tables:
  - `phrases(id, text, trigger_prefix, use_count, last_used_utc, source)` — source `accepted|manual`
  - `completion_cache(context_hash, suggestion, level, created_utc)` with TTL
  - `user_dictionary(word, added_utc)`
  - `snippets(id, trigger, body, use_count, updated_utc)` (used in 8E)
  - `custom_commands(id, name, instruction, hotkey, use_count)` (used in 8E)
  - `recent_instructions(id, text, used_utc)` (used in 8C, keep last 10)
- `PhraseService`: in-memory prefix trie; match last 1–5 words before caret; rank by use_count + recency.
- Settings tabs: "My phrases", "My dictionary" (add/edit/delete/import/export JSON).
- Accepted suggestions: increment use_count, or insert as `accepted` after 3 accepts.
**Acceptance:** saved phrase shows in < 30 ms, no network.

### PHASE 6 — Offline grammar with Harper (Grammar)
- First read the harper-ls docs (https://writewithharper.com/docs/integrations/language-server) + README; confirm stdio flag, plain-text languageId, config keys (dialect, linters, user dictionary), and how fixes come back (diagnostics + code actions). Summarize findings before coding.
- `tools/fetch-harper.ps1`: read `tools/harper.version`, download the Windows x64 harper-ls asset from https://github.com/Automattic/harper/releases, verify checksum, copy `harper-ls.exe` + Harper LICENSE to `src/Typee.App/runtimes/harper/` and build output. Used locally and in CI.
- `HarperProcessSupervisor`: hidden child process in stdio mode, bound to Typee with a Windows Job Object (dies with Typee); auto-restart max 3 times / 5 min, then disable grammar + tray notice.
- `HarperLspClient` (StreamJsonRpc): `initialize`/`initialized`; `workspace/didChangeConfiguration` (dialect, disabled rules, user dictionary); ONE virtual doc (e.g. `untitled:typee-buffer`); full-text `didChange` of current paragraph with incrementing version; await matching `publishDiagnostics` (timeout `grammar.requestTimeoutMs`, ignore stale versions); `textDocument/codeAction` per diagnostic for replacements + "add to dictionary"/"ignore" actions.
- Correct LSP position (line + UTF-16 char) ↔ string offset conversion incl. emoji/surrogates — unit tested.
- `GrammarService : IGrammarChecker` → `GrammarIssue { Start, Length, Message, RuleName, Replacements[], Kind }`, prioritizing the issue nearest before the caret.
- Harper is English-only: skip paragraphs that are mostly non-Latin script (e.g. Arabic).
- Ignore issues on the word currently being typed.
- "Add to dictionary" → SQLite + resend config.
- Fallback if harper-ls missing: tiny built-in checker (repeated words, double spaces, capital after period).
**Acceptance:** "this is a  test test of the the app" in Notepad → fixes in ~100 ms offline; killing harper-ls.exe → auto restart.

### PHASE 7 — Suggestion pipeline + LLM + consent (Core + Llm)
- On `TypingPaused`:
  1. Read context; bail if null/short/blocklisted.
  2. Local first: phrase trie → completion cache.
  3. Level 1 grammar: Harper. Only if `llmDeepCheck` and Harper found nothing → LLM style check returning strict JSON `{ "hasIssue": bool, "corrected": "...", "explanation": "..." }`.
  4. Level 2 completion: caret at end of text and no grammar issue → LLM continues with ≤ ~15 words, same tone and language, no quotes/explanations.
  5. Level 1 has priority over Level 2.
- Cancellation on `TypingResumed`, focus change, newer request; only the latest request updates UI.
- `OpenAiCompatibleClient`: SSE streaming, timeout, one retry, offline state → tray tooltip; local features keep working.
- Prompt templates in `src/Typee.Llm/Prompts/*.txt`.
- `ISuggestionProvider` plugin interface.
- First-run consent screen.
- Unit tests: router, debouncer, cancellation, priority, JSON parsing incl. malformed output.
**Acceptance:** Ollama on → completion in ~1–2 s at end of sentence; typing cancels instantly; Ollama off → grammar + phrases still work.

### PHASE 8 — Inserting text (Platform.Windows)
- `ITextInserter`: (a) `SendInput` `KEYEVENTF_UNICODE` batched (Arabic + emoji safe); (b) clipboard paste with save/restore after 300 ms.
- Grammar fixes: select exact span (UIA `Select()` preferred, Shift+arrows fallback), insert replacement, restore caret.
- Before inserting, verify foreground window, focused element, and span text are unchanged; otherwise abort silently.
- `IFocusSnapshotService`: capture target window/element/selection; restore focus + selection (used by 8C and 8D).
**Acceptance:** Tab applies fixes/completions in Notepad, Chrome, Word, VS Code incl. non-English.

### PHASE 8A — Typee Button + Action Menu
- Small round Typee button (24×24 DIP) at the bottom-right inside corner of the focused text element (supported fields only). Non-activating like the overlay.
- Status dot: grey idle, blue suggestion ready, orange = grammar issue count.
- Click → **Action Menu**: Fix grammar · Improve writing · Shorten · Expand · Make formal · Make friendly · Translate → (translateTargets) · Make numbered list · Make bullet list · Make checklist · Improve as AI prompt · Send to AI → (targets) · Insert snippet → · My commands → · Pause Typee in this app.
- Applies to selection if any, else current paragraph.
- Hides while typing fast (reappears after debounce); draggable along the edge; position remembered per app.
**Acceptance:** works in Notepad, Chrome textarea, VS Code, WhatsApp/Telegram desktop; clicking never steals focus from the text box.

### PHASE 8B — Smart Lists (auto numbering + style picker)
- Marker patterns at line start (keep leading indentation): numbered `1.` `1)` `(1)`; lettered `a.` `a)` `A.`; roman `i.` `ii.` (both cases); bullets `-` `*` `•` `–`; checklist `[ ]` `[x]` `☐` `☑`.
- **Auto-continue:** on Enter at end of a list item (do NOT swallow Enter), wait ~40 ms, read previous + current line via UIA; if new line empty → insert next marker (`1.`→`2.`, `a)`→`b)`, `iv.`→`v.`, same bullet, checklist → unchecked).
- Enter on an empty item (marker only) → remove marker, end list.
- Tab right after a marker → indent + sub-level style (`1.` → `a.` → `i.`); Shift+Tab → outdent. Only when no suggestion is visible.
- **Double-detection guard:** if the app already inserted a marker, do nothing. Default OFF in `smartLists.disabledProcesses` and in browser windows titled "Google Docs".
- **Style picker** ("List: 1. ▾" chip in overlay, or `Ctrl+Alt+L`): `1.` `1)` `a.` `A.` `i.` `•` `–` `☐` — converts the whole contiguous list and renumbers.
- Actions: Renumber list; Make numbered/bullet list/checklist from selected lines (skip blanks); `Ctrl+Alt+X` toggles checklist item.
- Unit tests: marker parsing, next-marker (numbers, letters, roman to 50, nesting, indentation), renumber, style conversion.
**Acceptance:** Notepad, WhatsApp desktop, Chrome textarea, VS Code: `1. milk`⏎ → `2. `; empty item ⏎ ends list; Tab → `a.`; picker converts; nothing doubled in Word.

### PHASE 8C — Command Bar (Cursor-style inline AI)
- `Ctrl+Shift+Space` opens a command bar near caret/selection. It TAKES focus (so the user can type), so first snapshot the target (8, `IFocusSnapshotService`) and restore before inserting.
- Contents: instruction box ("Tell Typee what to do…"); quick action chips with 1–9 shortcuts (Improve · Fix grammar · Shorten · Expand · Formal · Friendly · Translate · Bullet list · Numbered list · Summarize · Write reply · Improve as prompt); recent instructions; My commands.
- Input = selection, else current paragraph; "Write reply" uses text above caret as context.
- **Preview card** with word-level diff, streaming: Enter = replace · Ctrl+Enter = insert below · R = retry · C = copy only · Esc = cancel.
- Actions defined in `src/Typee.Llm/Prompts/actions/actions.json` (id, label, icon, templateFile, needsSelection) + one `.txt` template per action — contributors add actions without code.
- Fix grammar = Harper only; other actions = LLM (disabled with tooltip when LLM offline).
- Undo chip for 10 s after accept (original kept in memory).
**Acceptance:** select text in Chrome → Formal → preview → Enter replaces; Esc changes nothing; focus always returns; Undo restores.

### PHASE 8D — Prompt Detection + Send to AI
- Local heuristic score on paragraph/selection at typing pause: instruction verbs (write, create, build, generate, explain, act as, you are, give me, make a, help me), addressed to an AI (Claude, ChatGPT, Cursor, AI), spec structure (numbered requirements, "Requirements:", "Output:", "Deliverables"), ≥ 15 words. Unit-tested.
- Score ≥ threshold → chip "Looks like a prompt → Improve · Send to ▾". Not shown when the focused app is itself an AI target (unless the Typee button is used).
- **Improve as prompt:** LLM restructures into role, goal, context, requirements, output format — no invented facts; preview + accept like 8C.
- **Send to AI** targets from settings (Claude web/desktop, ChatGPT, Gemini, Perplexity, Cursor, custom).
  - Method 1 (default) clipboard + open/focus + paste: copy text, open URL in default browser or focus app window, wait until foreground (8 s timeout), `Ctrl+V`. Cursor: focus, send `openChatKeys`, paste. Restore original clipboard after 2 s.
  - Method 2 (optional) URL prefill template `...?q={text}` only for targets where you verified a documented prefill parameter; fall back to method 1 above 1,500 chars.
  - `autoSubmit` false by default; enabling shows a one-time warning.
  - Remember last target; `Ctrl+Alt+Enter` sends to last target. Also available from Action Menu and Command Bar.
- Settings tab "AI targets" (add/edit/remove/reorder).
**Acceptance:** prompt in Notepad → chip → Send to Claude → browser opens with text pasted, not submitted; Send to Cursor opens chat + pastes; clipboard restored.

### PHASE 8E — Snippets & Custom Commands
- Snippets: triggers with prefix (default `;`), e.g. `;sig`, `;addr`, `;ty`. Expand when followed by space/Enter/Tab: Backspace the trigger via SendInput, insert body. Variables `{date}`, `{time}`, `{clipboard}`, `{cursor}`. Settings tab "Snippets" (CRUD + import/export JSON). Listed in Action Menu.
- Custom commands: name + instruction (e.g. "Reply in Arabic and English", "Turn into WhatsApp message"), optional hotkey; shown in Command Bar + Action Menu under "My commands".
**Acceptance:** `;sig ` expands in any app; a custom command runs on the selection.

### PHASE 9 — Push-to-talk voice typing (Speech)
- Hold `Ctrl+Alt+M` → NAudio capture to memory, mic indicator near caret; release → Whisper.net on background thread → insert via `ITextInserter`.
- Max 60 s; ignore clips < 0.3 s.
- `WhisperModelManager`: first-use download with progress window, SHA256 check, model choice (base.en / small / multilingual small), keep warm.
- Optional: run transcript through Harper, auto-apply only unambiguous single-replacement fixes.
**Acceptance:** speak a sentence → text appears in focused app within seconds, fully offline.

### PHASE 10 — Polish & release
- Start with Windows (HKCU Run, value `Typee`).
- Per-app enable/disable from tray ("Disable Typee in Chrome").
- Graceful shutdown: unhook, unregister hotkeys, harper-ls `shutdown`/`exit`, dispose Whisper + DB.
- Self-contained `win-x64` publish → `Typee.exe` + `harper-ls.exe` + notices; `docs/RELEASING.md` (incl. bumping `tools/harper.version`).
- README: name + tagline, features, Ollama/Z.ai/OpenAI setup, hotkey table, Harper credit (Automattic, Apache-2.0), screenshot placeholders, known limitations (English-only grammar, elevated apps, games, apps without UIA text support).
**Acceptance:** clean install on a fresh Windows user account works end to end.

---

## 7. HOTKEY TABLE (defaults, all configurable)

| Action | Default |
|---|---|
| Accept suggestion | Tab (only while visible) |
| Dismiss | Esc |
| Command Bar | Ctrl+Shift+Space |
| Send to last AI target | Ctrl+Alt+Enter |
| List style picker | Ctrl+Alt+L |
| Toggle checklist item | Ctrl+Alt+X |
| Push-to-talk | Ctrl+Alt+M (hold) |

---

## 8. START NOW

1. Inspect the repository and tell me whether it is empty or already contains Typee work.
2. If existing work is found, write the audit to `docs/PROGRESS.md` and show me a summary.
3. Show the final solution tree and the NuGet packages per project.
4. Then implement the first unfinished phase only, run build + tests, report, and STOP.