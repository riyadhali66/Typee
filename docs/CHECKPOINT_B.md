# Checkpoint B — Manual test script

Run against `main` tagged `checkpoint-b` (Release build). Tray icon: notification area (often under **^** hidden icons). Look for a teal **T**. Right-click → **Show caret marker** (Checkpoint A), **Diagnostics…**, Settings, etc.

Consent: on first run accept the consent screen (local vs LLM).

---

## A. Deferred Checkpoint A (red caret marker)

1. Tray → check **Show caret marker**.
2. Open **Notepad**, type. A thin red bar should track the caret; Notepad keeps focus.
3. Repeat in **Word** and **Chrome** (a `<textarea>`, e.g. a form or `data:text/html,<textarea rows=10 cols=60></textarea>`).
4. Optional: Edge, VS Code, WhatsApp Desktop, 150% scaling, second monitor.
5. Admin Notepad / KeePass / browser password field → **no** red bar.
6. Full-screen YouTube (F) with `pauseInFullscreen=true` → marker pauses.
7. Logs under `%LOCALAPPDATA%\Typee\logs\` → search for a unique phrase you typed → **must not** appear.

---

## B. Grammar + overlay (Notepad)

1. Ensure suggestions are enabled (tray not “Pause”).
2. Open Notepad. Type slowly, pause ~1s after:

   `this is a  test test of the the app`

3. You should see a Typee overlay near the caret (grammar fix). It must **not** steal focus.
4. Press **Tab** → text should correct in Notepad.
5. Type another bad sentence:

   `She go to the store yesterday and buy milk.`

   Pause → overlay → **Esc** dismisses with no change.
6. Kill `harper-ls.exe` in Task Manager → Typee should restart it (or fall back); tray/Diagnostics may show status. Grammar should recover within a few pauses.

---

## C. Word

1. Open Word, new blank doc.
2. Type the same grammar sample, pause.
3. Overlay appears; Tab applies; Esc dismisses.
4. Confirm typing feels normal (no lag).

---

## D. Chrome

1. Open a page with a textarea (or the `data:` URL above).
2. Same grammar sample → overlay → Tab insert.
3. Click a password field on a login page → **no** overlay / no marker.

---

## E. Completion (optional LLM)

1. Settings → LLM: local Ollama `http://127.0.0.1:11434/v1` (or your profile), Save key if needed, Test.
2. Type a short start of a sentence and pause at the end with no grammar issue.
3. Completion overlay may appear (~1–2 s). Typing again should cancel it.
4. With Ollama off, grammar + phrases still work.

---

## F. Privacy / storage smoke

1. Settings → My phrases / My dictionary: add one entry each.
2. Privacy → Clear learned data (confirm phrases/dictionary behavior per UI copy).
3. Diagnostics → counters only; no typed text.

---

## Pass criteria for Checkpoint B

- Notepad + Word + Chrome: caret marker (A), grammar overlay, Tab insert, Esc dismiss.
- No focus steal; no typed text in logs.
- Elevated / password skips still hold.
