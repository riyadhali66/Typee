# Typee — Compatibility test matrix

Rows = target apps. Columns = features. Update at each checkpoint.

Legend: ✅ pass · ⚠️ partial / flaky · ❌ fail · — not tested · n/a not applicable yet

| App | Caret | Overlay | Context | Grammar | Completion | Insert | Lists | Command bar | Send-to-AI | Snippets | Voice |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Notepad | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Word | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Outlook | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Chrome (textarea) | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Edge | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Firefox | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| VS Code | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| WhatsApp desktop | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Telegram desktop | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Teams | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| Slack | — | — | — | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| TestHost | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |

## Phase 4 manual checklist (overlay)

Run with tray **Show dummy suggestion** (caret in a text field) or **Auto dummy on pause**.

1. Overlay appears below caret (above if no room); never steals focus — host keeps typing focus.
2. Esc dismisses; Tab dismisses (accept path; no insert until Phase 8).
3. Outside click / focus change / caret move hides overlay.
4. `pauseInFullscreen=true` + fullscreen presentation/game → overlay hidden/skipped.
5. Password / elevated / blocklisted / Typee own windows → no overlay.
6. Logs + Diagnostics show counts/status only — never typed text or suggestion body.
7. Second monitor + 150% DPI: overlay clamps to work area and scales DIP correctly.

Status after Phase 4 code: matrix columns **Caret** / **Overlay** ready for Checkpoint B manual fill; automated TestHost still deferred.
