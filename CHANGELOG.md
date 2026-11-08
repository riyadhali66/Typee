# Changelog

All notable changes to Typee are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- §9 hardening (now): crash marker + diagnostic clipboard offer; global exception handlers with tray notifications; `ServiceSupervisor` with 1s/5s/30s backoff for the keyboard hook.
- Settings `schemaVersion` 1, migrator/validator, `settings.backup.json`, import/export/reset; Privacy tab (clear local data; DB wipe Phase 5).
- `SetDefaultDllDirectories` at app startup; hook watchdog (`GetLastInputInfo`); system resume + `TaskbarCreated` handling.
- Hardening & Release Readiness spec (`docs/MASTER_PROMPT.md` §9): Credential Manager keys, Velopack installer plan, Arabic/a11y, test harness (deferred).
- Agent handoff section in `docs/PROGRESS.md`.

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
