# Typee — Features V2 (backlog)

Planned features beyond the master build prompt. **Nothing in this file is implemented yet** — these are spec notes for later phases. Implementation follows the same rules as the master prompt: privacy (§4), settings (§5), fail-safely, phase gating.

---

## PHASE 8F — Translate preview · LLM profiles · Summarize selection

Status: **planned — do not implement yet.**
Depends on: Phase 5 (SQLite storage + migrations), 7 (LLM client, consent screen), 8A (Action Menu, Translate), 8C (Command Bar, preview card, actions.json).

### 1. Translate — side-by-side preview before accepting

- The Translate action (Action Menu → Translate → target language; Command Bar → Translate) never inserts immediately. It first shows a **side-by-side preview**: original text on the left, translation on the right, target language in the card header (e.g. `Translate → Arabic`).
- Key model matches the 8C preview card with both panes visible: **Enter = replace** the selection/paragraph with the translation, **Ctrl+Enter = insert below**, **C = copy** the translation, **Esc = cancel** (original untouched).
- Targets come from `translateTargets` as in 8A; source language auto-detected. Both panes scroll independently; card clamped to the monitor work area like other overlays.
- **RTL panes:** set `FlowDirection=RightToLeft` on a pane when its language is RTL (Arabic, Urdu, Hebrew, Persian) — the translation pane when the *target* is RTL, the original pane when the detected *source* is RTL. Per-pane only, never the whole card, so mixed LTR/RTL pairs render correctly.
- Inputs over `maxActionInputChars` follow the shared chunking rule below (header notice `Text too long — processing the first part` when only part is processed).

**Acceptance:** select a paragraph in Chrome → Translate → Arabic → side-by-side card appears; Enter replaces, C copies, Esc leaves the original byte-identical; the Arabic pane lays out RTL with punctuation in the right places; a >12000-char input is chunk-merged or shows the partial-processing header.

### 2. Command bar — model dropdown over LLM profiles

- New settings array `llmProfiles[]` alongside the existing `llm` block (mirror in `settings.example.json`):

  ```json
  "llmProfiles": [
    { "name": "Ollama local", "baseUrl": "http://localhost:11434/v1", "model": "llama3.1:8b", "apiKeyEnvVar": "" },
    { "name": "Remote", "baseUrl": "https://api.openai.com/v1", "model": "gpt-4o-mini", "apiKeyEnvVar": "TYPEE_REMOTE_KEY" }
  ]
  ```

- The Command Bar gets a **model dropdown** listing configured profiles by `name`; the selected profile's `baseUrl` / `model` / `apiKeyEnvVar` routes that one request.
- **Remember the last choice per action**: Translate reopens with the profile last used for Translate, Summarize with its own, etc. Persisted in SQLite — table `action_profile_memory(action_id, profile_name, updated_utc)` in `typee.db` (Storage project, Phase 5 migrations) — **not** in settings.json, so memory writes never race the settings hot-reload. API keys still come only from env vars, never from file or DB.
- **Remote-endpoint notice:** when a profile with a non-`localhost` `baseUrl` is added in Settings or first used, show a one-time notice naming the exact endpoint host that text will be sent to (e.g. "Command bar text will be sent to `api.openai.com`"). Record the acknowledgment per profile (in `typee.db`); re-show only if that profile's `baseUrl` host later changes. `localhost`/`127.0.0.1` profiles are exempt.
- The existing single `llm` block stays as the implicit **Default** profile (first item in the dropdown) and keeps powering Level 2 completions outside the Command Bar. Empty/absent `llmProfiles[]` hides the dropdown — 8C behavior unchanged.
- Profiles managed in Settings → LLM (add/edit/remove). Unreachable profile → inline tooltip + tray offline state as in Phase 7; local features unaffected.

**Acceptance:** two profiles configured → run Translate with A, Summarize with B → reopen the bar: the dropdown pre-selects A for Translate and B for Summarize; Default still drives inline completions; memory survives a settings.json hot-reload; adding a remote profile shows the endpoint notice exactly once (never for localhost).

### 3. "Summarize selection" — copy-only by default

- New **Summarize** action: Command Bar chips + Action Menu (shown when a selection exists). Works on **any selected text** in any app that exposes a selection via UIA — no language or minimum-length requirement beyond non-empty.
- The result is **copy-only by default** — this action overrides the 8C default of Enter = replace. The summary appears in a preview card: **Enter = copy**, **Ctrl+Enter = insert below** the selection (via `ITextInserter` + `IFocusSnapshotService`, Phase 8), **Esc = cancel**. It never replaces the original text.
- Registered in `src/Typee.Llm/Prompts/actions/actions.json` like other actions (`needsSelection: true`); the template returns only the summary, in the language of the input by default.
- **Hint row is per action, always accurate:** generated from the action's actual key map, never a fixed string. Since Enter behaves differently here than in 8C, Summarize shows `Enter copy · Ctrl+Enter insert below · Esc cancel`; Translate shows `Enter replace · Ctrl+Enter insert below · C copy · Esc cancel`; standard 8C actions keep the 8C hint. No card may display keys it does not honor.
- Inputs over `maxActionInputChars` follow the shared chunking rule below (chunk + merge; header notice when partial).
- LLM-only (Harper not involved); disabled with tooltip when the LLM is offline. Summaries are never written to phrase learning or history.

**Acceptance:** select text in Word → Summarize → card shows the summary; Enter copies and the document is untouched; Ctrl+Enter inserts the summary below the selection and focus/selection are restored; the hint row reads "Enter copy · Ctrl+Enter insert below · Esc cancel"; a 20000-char selection is chunk-merged or shows the partial-processing header.

### Shared (Translate + Summarize) — max input size

- New setting `maxActionInputChars` (int, default `12000`), mirrored in `settings.example.json`, hot-reloaded like the rest of settings.
- Inputs above the limit are **chunked and the results merged** into a single preview — chunks processed sequentially in order, streaming into the card as they arrive.
- When only part of the input can be processed (merge not feasible or the user interrupts), process from the start up to the limit and show `Text too long — processing the first part` in the card header, so a partial result is never mistaken for a complete one.
- Chunk boundaries prefer paragraph/sentence breaks; never split surrogate pairs.

---

*Cross-cutting: all three features respect the privacy rules — text leaves the machine only via the selected LLM profile, after the per-profile endpoint acknowledgment for non-localhost endpoints, and never for blocklisted processes; nothing new is logged; new persisted state is limited to the `action_profile_memory` table and per-profile endpoint acknowledgments in `typee.db`.*
