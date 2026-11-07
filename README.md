# Typee

**Typee – typing assistant** for Windows: an open-source tray app that offers offline grammar fixes, next-thought completions, saved phrases, and offline voice typing.

> Phase 1 delivers the tray shell, settings, logging, and documentation. Later phases add caret tracking, Harper grammar, LLM completions, and Whisper voice typing.

## Requirements

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Quick start (Phase 1)

```powershell
cd Typee
dotnet restore
dotnet build
dotnet run --project src/Typee.App
```

The app starts **to the system tray only** (no main window).

| Tray menu | Action |
|-----------|--------|
| Enable / Pause suggestions | Toggle suggestions (used from Phase 4+) |
| Settings… | Edit `settings.json` |
| Open logs folder | `%LOCALAPPDATA%\Typee\logs` |
| Exit | Quit Typee |

A second launch activates the Settings window of the running instance (`Local\Typee.SingleInstance`).

## Configuration

- User settings: `%LOCALAPPDATA%\Typee\settings.json` (created on first run from defaults)
- Example: [`settings.example.json`](settings.example.json)
- API keys: environment variables only (see [`.env.example`](.env.example)); never stored in `settings.json`
- Settings hot-reload when the file changes on disk

## Privacy (non-negotiable)

- Never logs or transmits raw keystrokes
- Skips password fields and blocklisted processes
- Grammar (Harper) is fully local (Phase 6)
- LLM text is sent only when configured and the app is not blocklisted (Phase 7)
- Phrase learning stores only accepted/explicitly saved text (Phase 5)

## Solution layout

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Building & testing

```powershell
dotnet build Typee.sln
dotnet test Typee.sln
```

CI: [`.github/workflows/build.yml`](.github/workflows/build.yml) (Windows).

## License

MIT — see [`LICENSE`](LICENSE). Third-party notices: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

## Credits

Grammar engine (Phase 6): [Harper](https://github.com/Automattic/harper) by Automattic (Apache-2.0).
