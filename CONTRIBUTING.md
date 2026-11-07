# Contributing to Typee

Thank you for helping improve Typee. This guide covers branching, code style, and how to extend the app.

## Branching

- `main` — stable, releasable
- Feature branches: `feature/<short-name>`
- Fixes: `fix/<short-name>`
- Open a PR against `main`; CI (`build.yml`) must pass on `windows-latest`

## Code style

- Target `net8.0-windows`, x64, nullable enabled, warnings as errors
- Namespaces start with `Typee.` (e.g. `Typee.Core`, `Typee.Platform.Windows`)
- Dependency direction: **App → everything; everything else → Core**. Core must not reference Platform, App, Grammar, Llm, Speech, or Storage
- All Win32 interop lives in one `NativeMethods` class per project, prefer `LibraryImport` with `SetLastError = true`
- Nothing slow on the UI thread or inside a hook callback
- Features fail safely: log and keep running

## Adding a new LLM provider

Providers that speak OpenAI-compatible `/v1/chat/completions` need **no new code** — set `llm.baseUrl`, `llm.model`, and `llm.apiKeyEnvVar` in settings.

For a non-compatible API:

1. Implement `ILlmClient` (Phase 7) in `Typee.Llm`
2. Register it in the App DI composition root
3. Keep prompt templates in `src/Typee.Llm/Prompts/*.txt`
4. Never put API keys in `settings.json`

## Adding a new suggestion level

1. Implement `ISuggestionProvider` in Core (or a satellite project that references Core only)
2. Register the provider with the suggestion pipeline / task router
3. Document priority relative to Level 1 (grammar) and Level 2 (completion)
4. Respect cancellation on `TypingResumed` and blocklists

## Bumping the Harper version

1. Edit `tools/harper.version` to the new release tag (e.g. `vX.Y.Z`)
2. Update the expected SHA256 in `tools/fetch-harper.ps1` (or the checksum file it reads)
3. Run `.\tools\fetch-harper.ps1` locally and confirm `harper-ls.exe` lands under `src/Typee.App/runtimes/harper/`
4. Confirm CI still downloads and builds
5. Note the bump in `THIRD_PARTY_NOTICES.md` / release notes

## Tests

```powershell
dotnet test Typee.sln
```

Prefer xUnit. Cover Core routing/debouncing, Grammar offset conversion, and Storage migrations.

## License

By contributing, you agree your contributions are licensed under the MIT License.
