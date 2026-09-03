# Physiquinator

<div align="center">

[![Build](https://img.shields.io/github/actions/workflow/status/kadato/Physiquinator/ci.yml?label=CI&logo=github)](https://github.com/kadato/Physiquinator/actions/workflows/ci.yml)
[![GitHub Release](https://img.shields.io/github/v/release/kadato/Physiquinator)](https://github.com/kadato/Physiquinator/releases/latest)
![.NET 11](https://img.shields.io/badge/.NET-11.0-512BD4?logo=dotnet)
![.NET MAUI](https://img.shields.io/badge/.NET_MAUI-11.0-512BD4?logo=dotnet)
![Blazor Hybrid](https://img.shields.io/badge/Blazor-Hybrid-512BD4?logo=blazor)
![License](https://img.shields.io/badge/License-MIT-green.svg)

A workout tracker that runs on Android, Windows, iOS, macOS, and the browser from one Blazor UI. You plan workouts and log sets against a rest timer that keeps running while you switch apps. Progress shows in a heatmap, charts, and automatic personal records. Your data stays in local SQLite. The app works offline.

An AI assistant with 15 tools answers questions about your training and edits your plans. The same tools are open to external agents over MCP.

</div>

<div align="center">

[![Live demo in the browser](https://img.shields.io/badge/Live%20Demo-Try%20in%20browser-2dd4bf?style=for-the-badge)](https://physiquinator.pages.dev)

No install, no account. Data stays in your browser.

</div>

## Download and install

Download the latest build from the [releases page](https://github.com/kadato/Physiquinator/releases/latest), or use the direct links below.

| Platform | Package | Size | Notes |
|----------|---------|------|-------|
| Android | [![APK](https://img.shields.io/badge/APK-3ddc84?style=for-the-badge&logo=android&logoColor=white)](https://github.com/kadato/Physiquinator/releases/latest/download/Physiquinator-Android.apk) | 115 MB | Needs Android 7.0 or later. Enable unknown sources first |
| Windows | [![Portable ZIP](https://img.shields.io/badge/Portable%20ZIP-1f6feb?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/kadato/Physiquinator/releases/latest/download/Physiquinator-Windows.zip) | 70 MB | Portable. Extract and run |
| iOS and macOS | *Source only* | - | Build from source using the .NET MAUI workload |
| Web | [![Web App](https://img.shields.io/badge/Web%20App-6b46c1?style=for-the-badge&logo=web&logoColor=white)](https://physiquinator.pages.dev) | - | Live demo, no install needed |

**Windows runtime note.** Install the [.NET 11 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/11.0) once.

### Installation instructions

#### Android

1. Allow **Install unknown apps** for your browser
2. Open the APK and install

#### Windows

Extract the ZIP file, then run `Physiquinator.exe`. If the app does not start, see [WINDOWS-INSTALL.md](WINDOWS-INSTALL.md).

Every `v*` tag builds a signed APK and a Windows package via `.github/workflows/release.yml`. Updates keep your local SQLite data. The app checks GitHub releases on start and shows an update prompt in **Settings**.

First launch seeds sample plans and workouts.

## Preview

<div align="center">

<p align="center">
<strong>Live workout</strong>, <strong>Plans home</strong>, <strong>AI assistant</strong><br><br>
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/rest-timer-dark.png">
  <img alt="Active workout with rest timer" src="./docs/rest-timer-light.png" width="190">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/home-dark.png">
  <img alt="Home dashboard and plan list" src="./docs/home-light.png" width="190">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/ai-chat-dark.png">
  <img alt="AI assistant chat" src="./docs/ai-chat-light.png" width="190">
</picture>
</p>

<p align="center">
<strong>Activity history</strong>, <strong>Session summary</strong>, <strong>Exercise detail</strong><br><br>
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/history-dark.png">
  <img alt="Workout history with activity grid" src="./docs/history-light.png" width="190">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/session-details-dark.png">
  <img alt="Completed session summary" src="./docs/session-details-light.png" width="190">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/exercise-progression-dark.png">
  <img alt="Per-exercise session history" src="./docs/exercise-progression-light.png" width="190">
</picture>
</p>

<p align="center">
<strong>Create plan</strong>, <strong>Edit plan</strong>, <strong>Settings</strong><br><br>
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/create-plan-dark.png">
  <img alt="Create a new workout plan" src="./docs/create-plan-light.png" width="190">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/edit-plan-dark.png">
  <img alt="Edit workout plan exercises" src="./docs/edit-plan-light.png" width="190">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/settings-dark.png">
  <img alt="Settings with appearance and JSON backup" src="./docs/settings-light.png" width="190">
</picture>
</p>

</div>

## Features

- **Plans.** Build plans with custom exercises, per-exercise rest, sets, and reps. Duplicate any plan. Undo removes the copy.
- **Logging.** Log weight and reps, bodyweight reps with added load, or duration. Each set pre-fills from your last session of the same plan. Names autocomplete from the catalog and your history. Custom names still save.
- **Supersets.** Group exercises into supersets. The workout shows the partner up next and skips the rest between the two.
- **Rest timer.** Count down with add-time, reset, skip, and pause. Progress counts completed time, not just sets. On Android the timer floats over other apps from a foreground service, with sound, vibration, and exact alarms.
- **History.** Read the 53-week heatmap, per-exercise charts with PR markers, and automatic records for weight, reps, volume, and session length. Streaks respect your training days. Open any session to edit its sets, continue it, or delete it.
- **Session compare.** Pick two sessions and compare them side by side.
- **Bodyweight.** Log bodyweight daily and read the trend by range. Calisthenics volume counts a share of your bodyweight. Push-ups count 65%, pull-ups 100%. Charts use 70 kg where you logged no weight.
- **Share cards.** Copy a session as text or save it as a PNG card. You pick the parts and the theme.
- **AI assistant.** Chat in the app with streaming answers. The assistant works through 15 tools that read plans, history, bodyweight, and settings. `generate_deload_plan` cuts volume for a light week. `calculate_progressive_overload` sets next targets from recent sessions.
- **AI providers.** Use OpenAI, OpenRouter, OpenCode, local Ollama, or any OpenAI-compatible endpoint. Fetch the model list from the endpoint and test the connection before you save. Custom instructions steer every chat. No API key? The clipboard bridge carries your context and the action schemas to Gemini or ChatGPT and back. You review each action as a diff before the app applies it.
- **Agent API.** The web host exposes the same 15 tools at `/mcp` over Streamable HTTP for MCP clients. Destructive tools ask for confirmation. Set `Mcp__ApiKey` before you expose the host. Browser clients also need `CorsOrigins` in `appsettings.json`.
- **Profiles and backup.** Switch one-tap profiles, each with its own database. Export and import plans, history, or everything as JSON. You preview the contents before you apply them.
- **Themes and settings.** Pick from 15 themes or follow the OS. The native chrome follows your pick. Set kg or lb, rest alerts, the floating bubble, and your training days. Settings has search.
- **Web and browser.** `Physiquinator.Web` serves the same UI with cookie auth and per-account SQLite mirrored to IndexedDB. It answers `/healthz` and needs `Mcp__ApiKey` in public, so serve it behind HTTPS. `Physiquinator.Wasm` runs with no server and no accounts, keeps per-browser SQLite with a 20-second autosave, and installs as an offline PWA. Clearing site data deletes history, so export from **Settings** regularly. Browser AI calls need CORS headers, so set `OLLAMA_ORIGINS` for Ollama. The WASM host has no `/mcp`.

## Tech stack

| Area     | Choice                                                                                          |
| -------- | ----------------------------------------------------------------------------------------------- |
| Runtime  | .NET 11 SDK, see `global.json`, plus the MAUI workload                                         |
| UI       | Blazor Hybrid, one Razor tree in `Physiquinator.UI` for native, web, and WASM                   |
| Hosts    | Native host `Physiquinator` covers Android, iOS, macOS, and Windows. `Physiquinator.Web` serves the UI and `/mcp`. `Physiquinator.Wasm` ships the static build |
| DB       | SQLite storage with sqlite-net-pcl, offline-first, one database per profile                              |
| AI       | SSE client with tool loops for OpenAI-compatible endpoints. ModelContextProtocol.AspNetCore serves MCP    |
| Timer    | Android foreground service with draggable overlay, sound, vibration, and exact alarms            |
| Tests    | xUnit, Playwright e2e in `tools/web-e2e`, screenshots in `tools/screenshot-generator`           |

## Get started

```bash
git clone https://github.com/kadato/Physiquinator.git
cd Physiquinator
dotnet restore

# Run on Windows
dotnet build -t:Run -f net11.0-windows10.0.19041.0

# Run on Android (device/emulator)
dotnet build -t:Run -f net11.0-android

# Run the web client (includes the MCP server on /mcp)
dotnet run --project Physiquinator.Web

# Enable format checks and commit linting (one time)
./install-hooks.sh
```

CI runs the same build, test, and format checks on every push and PR, plus SonarCloud analysis with coverage.

Other commands:

```bash
dotnet test Physiquinator.Tests/Physiquinator.Tests.csproj
dotnet run --project Physiquinator.Web --urls http://localhost:8080  # then: cd tools/web-e2e && npm test
```

To target another host, set `PLAYWRIGHT_BASE_URL`. To regenerate the docs screenshots, build `Physiquinator.Web` once, then run `node tools/screenshot-generator/screenshot-web.js`.

## More

- [WINDOWS-INSTALL.md](WINDOWS-INSTALL.md) - Windows setup and troubleshooting
- [docs/theming.md](docs/theming.md) - how to add a theme
