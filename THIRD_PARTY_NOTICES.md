# Third-party notices

Typee includes or depends on the following open-source components. Licenses are summarized here; full license texts should be consulted at the upstream projects. Harper's Apache-2.0 LICENSE is copied next to `harper-ls.exe` at build time (Phase 6).

## Harper (Automattic)

- Project: https://github.com/Automattic/harper
- License: Apache License 2.0
- Use in Typee: offline grammar via the official `harper-ls` binary over LSP (Phase 6). Typee does not vendor Harper source.

### Apache License 2.0 (Harper)

```
Copyright 2024 Automattic Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

Full Apache-2.0 text: https://www.apache.org/licenses/LICENSE-2.0

## Whisper.cpp / Whisper.net

- Whisper.cpp: https://github.com/ggerganov/whisper.cpp (MIT)
- Whisper.net: https://github.com/sandrohanea/whisper.net (MIT)
- Use in Typee: offline speech-to-text (Phase 9)

## NAudio

- https://github.com/naudio/NAudio
- License: MIT
- Use in Typee: microphone capture (Phase 9)

## H.NotifyIcon.Wpf

- https://github.com/HavenDV/H.NotifyIcon
- License: MIT
- Use in Typee: system tray icon

## Serilog

- https://github.com/serilog/serilog
- License: Apache-2.0
- Use in Typee: rolling file logging

## Microsoft.Data.Sqlite / Dapper

- Microsoft.Data.Sqlite: MIT (Microsoft)
- Dapper: Apache-2.0
- Use in Typee: local phrase/settings storage (Phase 5+)

## StreamJsonRpc

- https://github.com/microsoft/vs-streamjsonrpc
- License: MIT
- Use in Typee: Harper LSP over stdio (Phase 6)

## Interop.UIAutomationClient

- COM interop package for UI Automation
- Use in Typee: caret and text context (Phase 3)

## .NET / WPF / Microsoft.Extensions.*

- Microsoft .NET runtime and libraries under their respective Microsoft licenses
- Use in Typee: application host and UI
