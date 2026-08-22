# Physiquinator

<div align="center">

![.NET 11](https://img.shields.io/badge/.NET-11.0-512BD4?logo=dotnet)
![.NET MAUI](https://img.shields.io/badge/.NET_MAUI-11.0-512BD4?logo=dotnet)
![Blazor Hybrid](https://img.shields.io/badge/Blazor-Hybrid-512BD4?logo=blazor)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows%20%7C%20iOS%20%7C%20macOS-blue)
[![Build](https://img.shields.io/github/actions/workflow/status/kadato/Physiquinator/ci.yml?label=CI&logo=github)](https://github.com/kadato/Physiquinator/actions/workflows/ci.yml)
[![GitHub Release](https://img.shields.io/github/v/release/kadato/Physiquinator)](https://github.com/kadato/Physiquinator/releases/latest)

A cross-platform workout tracking app built with **.NET MAUI and Blazor Hybrid**. Plan workouts, log sets against a rest timer that keeps running across apps, track progress and personal records, and ask an on-device AI assistant to analyze your training. All data lives in a local SQLite database and works offline.

It also ships a **web client with a Model Context Protocol (MCP) server**, so any AI agent (Claude, Cursor, Copilot) can query your workout history and manage your plans.

[Preview](#preview) - [Download and install](#download-and-install) - [Features](#features) - [Agent API (MCP)](#agent-api-mcp) - [Architecture](#architecture) - [Tech stack](#tech-stack) - [Getting started](#getting-started) - [Testing and CI](#testing-and-ci)

</div>

---

## Preview

<div align="center">

<p align="center">
<strong>Live workout</strong> - <strong>Plans home</strong> - <strong>AI assistant</strong><br><br>
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/rest-timer-dark.png">
  <img alt="Active workout with rest timer" src="./docs/rest-timer-light.png" width="220">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/home-dark.png">
  <img alt="Home dashboard and plan list" src="./docs/home-light.png" width="220">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/ai-chat-dark.png">
  <img alt="AI assistant chat" src="./docs/ai-chat-light.png" width="220">
</picture>
</p>

<br>

<p align="center">
<strong>Activity history</strong> - <strong>Session summary</strong> - <strong>Exercise detail</strong><br><br>
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/history-dark.png">
  <img alt="Workout history with activity grid" src="./docs/history-light.png" width="220">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/session-details-dark.png">
  <img alt="Completed session summary" src="./docs/session-details-light.png" width="220">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/exercise-progression-dark.png">
  <img alt="Per-exercise session history" src="./docs/exercise-progression-light.png" width="220">
</picture>
</p>

<br>

<p align="center">
<strong>Create plan</strong> - <strong>Edit plan</strong> - <strong>Settings</strong><br><br>
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/create-plan-dark.png">
  <img alt="Create a new workout plan" src="./docs/create-plan-light.png" width="220">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/edit-plan-dark.png">
  <img alt="Edit workout plan exercises" src="./docs/edit-plan-light.png" width="220">
</picture>
&nbsp;&nbsp;
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="./docs/settings-dark.png">
  <img alt="Settings with appearance and JSON backup" src="./docs/settings-light.png" width="220">
</picture>
</p>

</div>

---

## Download and install

**Latest release**: [![GitHub Release](https://img.shields.io/github/v/release/kadato/Physiquinator)](https://github.com/kadato/Physiquinator/releases/latest)

| Platform | Package | Size | Requirements |
|----------|---------|------|--------------|
| Android | [Physiquinator-Android.apk](https://github.com/kadato/Physiquinator/releases/latest/download/Physiquinator-Android.apk) | ~115 MB | Android 7.0+ |
| Windows | [Physiquinator-Windows.zip](https://github.com/kadato/Physiquinator/releases/latest/download/Physiquinator-Windows.zip) | ~70 MB | [.NET 11 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/11.0) (one-time, free) |

**Android:** enable "Install from unknown sources", open the APK, tap **Install**.

**Windows:** extract the ZIP and run `Physiquinator.exe`. No installation needed. See [WINDOWS-INSTALL.md](WINDOWS-INSTALL.md) for troubleshooting.

> The app seeds sample plans and workouts on first launch so you can explore immediately.

---

## Features
 
### AI assistant
- In-app chat with streaming responses and 15 built-in tools: create and edit workout plans, log bodyweight, query training history and progression, and configure rest timer or application settings.
- Provider presets for OpenAI, OpenRouter, OpenCode, local [Ollama](https://ollama.com) instances, or any custom OpenAI-compatible endpoint.
- Clipboard bridge for closed models such as Gemini or ChatGPT that have no API access. Copy a generated prompt carrying your training context and the app's action schemas into the web chat, then paste the reply back. The app parses the actions, shows a summary of each, and applies them through the same tool registry. No API key required.
- Full parity with external AI agents via MCP (see [Agent API](#agent-api-mcp)).
 
### Workout plans and live tracking
- Configurable plans with custom exercises, rest intervals, and target sets/reps.
- Three logging types per exercise: weight and reps, bodyweight reps with an optional added-weight offset, or plain duration.
- Bodyweight share for calisthenics: volume counts a configurable percentage of your logged bodyweight, pre-filled from a built-in catalog (push-ups 65%, pull-ups 100%). Weighted variations such as weighted pull-ups add their plate load on top.
- Real-time set logging with weight and rep steppers, undo support, and completion summaries with total volume and newly achieved personal records.
- Session-to-session carryover: each set's weight and reps pre-fill from the same set number in your most recent session of that plan, falling back to the plan defaults where no history exists.
 
### Rest timer
- Accurate interval countdown with quick-add increments, reset, and skip controls.
- On Android, a draggable floating overlay runs inside a foreground service to maintain countdown visibility across apps, with optional audio, haptics, and exact alarms that survive Doze mode.
 
### History and analytics
- 53-week activity heatmap, per-exercise progression charts, automatic personal record tracking (weight, reps, volume, session duration), and schedule-aware streaks that account for rest days.
- Bodyweight exercises compute their volume from your current logged bodyweight, so set volume reflects real load rather than zero.
 
### Profiles and data management
- Isolated user profiles with one-tap switching.
- Offline-first local SQLite persistence with JSON backup and restore (plans, history, preferences).
- Automated GitHub release update checks on Android and Windows.
 
---
 
## Agent API (MCP)
 
The web client exposes all assistant tools over the [Model Context Protocol](https://modelcontextprotocol.io) (Streamable HTTP). MCP-compatible agents connect to `https://<host>/mcp`. With the default launch settings (`dotnet run --project Physiquinator.Web`), the local endpoint is `http://localhost:5149/mcp`.
 
All 15 tools, including `get_workout_plans`, `create_workout_plan`, `log_bodyweight_entry`, and `get_workout_history_stats`, publish standard JSON schemas. Destructive actions (`delete_workout_plan`, `delete_bodyweight_entry`) prompt for user confirmation using the protocol's `input_required` flow.
 
Configuration (`appsettings.json` or environment variables):
 
```json
"Mcp": {
  "ApiKey": "",     // Required in production: /mcp rejects requests without a matching X-Api-Key or Authorization header
  "CorsOrigins": "" // Comma-separated allowed origins for browser-based clients
}
```

---

## Web client

`Physiquinator.Web` is the same Blazor UI as a server-rendered web app, plus the MCP endpoint at `/mcp`. Every account gets its own SQLite database with cookie auth and PBKDF2 password hashes. While the page is open, the server mirrors databases to browser IndexedDB. The app ships security headers, rate limiting, and a `/healthz` readiness probe.

Run it locally with `dotnet run --project Physiquinator.Web`. The `/mcp` endpoint rejects requests without a matching API key, so set `Mcp__ApiKey` before hosting publicly. Optionally set `AUTH_DEMO_USERNAME` and `AUTH_DEMO_PASSWORD` for a demo login. Serve the app behind an HTTPS reverse proxy that sets `X-Forwarded-*` headers.

---

## Architecture

Five projects sharing a common domain model and service layer:

| Project | Purpose |
|---------|---------|
| `Physiquinator.Core` | Domain model, SQLite repositories, and business logic (workouts, history, stats, AI assistant, backup, updates) |
| `Physiquinator.UI` | Blazor Hybrid UI (Razor class library): pages, components, and theming |
| `Physiquinator` | .NET MAUI host for Android, iOS, macOS, and Windows |
| `Physiquinator.Web` | ASP.NET Core web host: browser-rendered UI and MCP endpoint |
| `Physiquinator.Tests` | xUnit test suite covering repositories, services, and the MCP surface |

Key design points:

- **Shared Blazor Hybrid UI:** The same Razor component tree renders natively via WebView2 in MAUI and on the web.
- **Platform abstractions:** Notifications, vibration, file transfer, and update installation are abstracted behind interfaces with native, no-op, and test-double implementations.
- **Unified service registration:** `AddPhysiquinatorServices()` registers core dependencies across both hosts (singletons on MAUI, scoped per Blazor circuit on Web).
- **Background rest timer:** On Android, the rest timer runs within a foreground service with a draggable overlay and exact alarms.

---

## Tech stack

- **.NET 11** with **.NET MAUI** (Android, iOS, macOS, Windows) and **Blazor Hybrid**
- **[MudBlazor](https://mudblazor.com/)** Material Design components and **[Markdig](https://github.com/xoofx/markdig)** for AI response rendering
- **[SQLite](https://www.sqlite.org/)** via [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) - local, offline-first storage
- OpenAI-compatible client (SSE streaming, tool-call loops) and [ModelContextProtocol.AspNetCore](https://github.com/modelcontextprotocol/csharp-sdk) MCP server
- [Plugin.LocalNotification](https://github.com/thudugala/Plugin.LocalNotification), MAUI Essentials, and Android foreground services
- GitHub Actions workflows for CI, SonarCloud analysis, and signed releases, plus Playwright for E2E tests and screenshot generation

---

## Getting started

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

# Run the tests
dotnet test Physiquinator.Tests/Physiquinator.Tests.csproj
```

Requires the [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0) (pinned in `global.json`) and the MAUI workload.

To build the Android APK without installing an Android SDK, see [DOCKER.md](DOCKER.md). To regenerate the screenshots in `docs/`, run `tools/screenshot-generator/run.ps1`.

---

## Testing and CI

- **xUnit tests** covering repositories, workout/session/history services, stats, formatting, the AI tool registry, and the MCP surface
- **CI on every push and PR**: restore, build, test, and `dotnet format` verification (`.github/workflows/ci.yml`)
- **SonarCloud** analysis with coverage (`.github/workflows/sonarcloud.yml`)
- **Tag-based releases** (`v*`): a signed Android APK and Windows package are published automatically (`.github/workflows/release.yml`)
- **Web E2E**: Playwright suite in `tools/web-e2e` covering registration, seeded plans, and the IndexedDB sync roundtrip. Start the web host on port 8080, then run the tests:
  ```bash
  dotnet run --project Physiquinator.Web --urls http://localhost:8080
  cd tools/web-e2e
  npm test
  ```
  To test a host on another port, set `PLAYWRIGHT_BASE_URL`.
