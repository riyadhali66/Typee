# Typee architecture

## Overview

Typee is a Windows tray application. A WPF host (`Typee.App`) composes Microsoft.Extensions.Hosting services. Suggestion UI is a non-activating overlay; platform input/caret work lives in `Typee.Platform.Windows`. Feature engines (grammar, LLM, speech, storage) implement Core interfaces.

```
Typee.App
  ├─ Typee.Core          (interfaces, models, pipeline — no infra refs)
  ├─ Typee.Platform.Windows
  ├─ Typee.Grammar       (Harper LSP)
  ├─ Typee.Llm           (OpenAI-compatible client)
  ├─ Typee.Speech        (NAudio + Whisper.net)
  └─ Typee.Storage       (SQLite settings/phrases)
```

**Dependency rule:** only App references feature projects; feature projects reference Core only. Core has zero project references to other Typee assemblies.

## Phase map

| Phase | Focus |
|-------|--------|
| 1 | Skeleton, tray, settings, logging, docs, CI |
| 2 | LL keyboard hook, hotkeys, typing activity |
| 3 | Caret locator + UIA text context |
| 4 | Floating suggestion overlay |
| 5 | Phrase cache + SQLite |
| 6 | Harper grammar (LSP) |
| 7 | Suggestion pipeline + LLM |
| 8 | Text insertion (SendInput / clipboard) |
| 9 | Push-to-talk + Whisper |
| 10 | Polish, publish, releasing docs |

## Runtime data locations

| Path | Purpose |
|------|---------|
| `%LOCALAPPDATA%\Typee\settings.json` | User settings (hot-reloaded) |
| `%LOCALAPPDATA%\Typee\logs\` | Serilog rolling files |
| `%LOCALAPPDATA%\Typee\typee.db` | Phrases / cache / dictionary (Phase 5) |
| `%LOCALAPPDATA%\Typee\models\` | Whisper models (Phase 9) |

## Settings schema

See root `settings.example.json`. API keys are read from the environment variable named by `llm.apiKeyEnvVar`, never persisted in JSON.

## Single instance

Named mutex `Local\Typee.SingleInstance`. A second process signals the first to open Settings, then exits.

## Privacy boundaries

- Keyboard hook: pause/hotkey detection only — no keystroke logging or storage
- Skip `IsPassword` UIA elements, blocklisted processes, elevated targets
- Grammar offline; LLM optional and consent-gated

## Threading

- UI: WPF dispatcher
- Hooks / UIA: dedicated threads with message loops (Phases 2–3)
- LLM / Whisper / Harper I/O: background; never on hook callbacks
