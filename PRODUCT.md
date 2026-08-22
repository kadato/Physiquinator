# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Users

**Primary.** Hybrid athlete, daily tracker. Trains 3-6x/week, at the gym and at home. Phone is on the bench, on the floor, or in a pocket between sets. Hands may be sweaty, chalked, or gloved. Attention is fragmented, with 30 to 90 seconds of rest between sets. Needs to log a set in under 3 seconds with one thumb, see immediately whether they beat last session, and never lose context if interrupted by a call or by switching apps.

Tracks more than sets: bodyweight almost daily, PRs as identity, streaks as motivation. Reviews history nightly and weekly with heatmap, progression charts, and per-exercise volume. Wants reassurance that the plan works, and wants the AI to answer "am I progressing on bench?" without exporting data.

**Secondary.** Beginner who wants a plan that just starts. Opens the app on day one and expects seeded plans to work immediately, with zero account creation. Needs guidance over choice: which plan, how many sets, how long to rest. The beta staging must protect them from gap errors.

Both audiences share one behavior: they return because the app never punishes a rest day the schedule said was a rest day, and never demands connectivity.

## Product Purpose

Physiquinator lets people plan workouts, log sets with a rest timer that survives the OS, and understand progress without a backend.

What success means: a user completes a 45-60 minute session without fighting the UI, sees every PR the moment it happens, and a week later can answer from the heatmap and charts whether the program is working, all offline, with data that stays in a local SQLite database until they choose to export it.

It also ships the same Blazor UI as a server-rendered web app plus a Model Context Protocol server on `/mcp`, so any AI agent (Claude, Cursor, Copilot) can manage plans and history externally with the same 15 tools the in-app assistant uses.

## Positioning

**One UI, every runtime, and every tool an agent can call.**

The exact same Razor UI runs natively via WebView2 on Android, Windows, iOS, macOS (MAUI Blazor Hybrid) and in the browser (ASP.NET Core). One component library, one interaction model, one set of tools. Not a mobile app with a separate web dashboard.

Those 15 assistant tools (create/edit plans, log bodyweight, pull history stats, control the rest timer and settings, and more) are exposed identically in-app and over MCP as Streamable HTTP at `/mcp` with JSON schemas and `input_required` confirmation for destructive calls. No competitor provides an external, agent-callable API at this fidelity while staying offline-first and local-storage by default.

## Operating Context

**Where and how the app is used:**
- Between sets: standing over a barbell, timer counting down, adding weight/reps via a stepper that must work with a thumb and survive a sweaty tap.
- Between sessions: reviewing the 53-week activity grid, per-exercise progression charts, session summaries with volume and PRs, bodyweight trends.
- Between apps: on Android the rest timer lives as a draggable floating overlay in a foreground service, with optional sound, vibration, and exact alarms that survive Doze, visible while the user is in Spotify, timer, or camera.
- On the web: cookie-authenticated, per-account SQLite databases mirrored to IndexedDB while the page is open; served behind HTTPS with rate limiting and `/healthz`.

**Workflows:**
- Create a plan, add exercises, set per-exercise rest and set count, start, log sets, rest, repeat, and finish with a summary showing volume and PRs.
- History: filter by plan or exercise, open a session, open exercise progression, see every set ever logged.
- Settings: appearance (light/dark), backup/restore JSON (plans, history, settings), update checks from GitHub Releases, AI provider config (OpenAI, OpenRouter, OpenCode, local Ollama, custom OpenAI-compatible).

**Rituals and materials:**
- Seeded sample plans and workouts on first launch so the empty state is never empty.
- GitHub-style activity heatmap with "scheduled / missed / planned" states.
- Personal records computed automatically (weight, reps, volume, session duration).
- Profiles are isolated with one-tap switching; no cloud account is required on native.

## Capabilities and Constraints

**Confirmed capabilities:**
- Custom plans: unlimited exercises, per-exercise rest interval and set count, one-tap start, quick edit, JSON import/export (single plan or full backup).
- Live tracking: real-time set logging with rep/weight/metric editing and undo, second-accurate rest countdown with add-time presets, reset, skip, pause/resume.
- History and analytics: 53-week heatmap, per-exercise charts, automatic PRs, bodyweight tracking, schedule-aware streaks.
- AI assistant: in-app streaming chat with 15 built-in tools, provider presets, SSE tool-call loops; same tools exposed externally over MCP.
- Platform services behind interfaces (notifications, vibration, file transfer, update installation) with real/no-op/test-double implementations.
- Single service registry `AddPhysiquinatorServices()`: singletons on MAUI, scoped per Blazor circuit on web.
- Automatic updates from GitHub Releases on Android and Windows.

**Durable constraints:**
- Local SQLite via `sqlite-net-pcl`, offline-first; web host mirrors to IndexedDB. No required cloud, no sync server.
- .NET 11 + MAUI + Blazor Hybrid + MudBlazor + Markdig + sqlite-net-pcl. `Physiquinator.Core` owns domain, repositories, services; `Physiquinator.UI` owns all Razor UI.
- The visual system is free to be replaced wholesale. Teal `#0F766E` / volt `#10B981` / Outfit / 16px radius / floating pill nav / dual extended FABs are the incumbent, not a brand commitment.
- Must keep: workout domain truth, set-level data, schedule-aware heatmap semantics, rest timer second accuracy, foreground-service overlay on Android, backup JSON shape.

**Explicitly undecided:**
- Next AI model policy and pricing.
- Future sync or team/sharing scope (out of scope for this world).

## Brand Commitments

- **Name:** Physiquinator (verb + physique; already seeded as `Physiquinator`, `Physiquinator.UI`, `Physiquinator.Web`).
- **Voice:** Direct, athletic, quietly technical. The copy in-app is terse ("Continue workout", "Your plans", "In progress", "Done today"). Keep that brevity.
- **Incumbent assets that may be retired:** Outfit type, teal/volt palette, MudBlazor elevation/border language, pill nav + dual FAB pattern, card gradients with `color-mix` tints. None is binding; the replacement must still read as a training tool, not lifestyle marketing.
- **No locked logo mark, mascot, or illustration library.** Author whatever the new world needs at production fidelity; do not fabricate customers, pricing, benchmarks, or testimonials.

## Evidence on Hand

- Repo: `Physiquinator.slnx` with projects `Core`, `UI` (Razor class library), `Web` (ASP.NET Core + MCP at `/mcp`), `Tests` (xUnit), `Physiquinator` (MAUI shell).
- Current theme: `Physiquinator.UI/PhysiquinatorThemes.cs` (PaletteLight Teal `#0F766E`, PaletteDark Volt `#10B981` + Cobalt/Magenta accents, 16px radius) and `wwwroot/css/app-overrides.css` (~1900 lines: nav pill, FAB system, home hero, heatmap, rest timer panel, stepper).
- Screens in `docs/`: `rest-timer-*.png`, `home-*.png`, `ai-chat-*.png`, `history-*.png`, `session-details-*.png`, `exercise-progression-*.png`, `create-plan-*.png`, `edit-plan-*.png`, `settings-*.png` (light + dark).
- Live verification: MAUI shell `MainLayout.razor` with adaptive nav pill (`Home`/`History`/`Settings`), immersive shell variants, splash screen, update dialog.
- No fabricated claims exist to preserve; any marketing proof (metrics, testimonials, case studies) must be authored as clearly synthetic or sourced.

## Product Principles

1. **One logged set drives everything.** Timer, stepper, PR detection, volume, summary, and heatmap dot all derive from a single logged set. If logging a set gets slower, nothing else can compensate.
2. **Rest is a first-class state, not idle time.** The timer is a training partner with its own surface, progression (track + fill + urgency colors), and presence beyond the app (overlay, alarm). It earns attention, not just counts down.
3. **Same UI, no second-class runtime.** A workout started on Android and inspected on the web must feel like one product. Divergent chrome per platform breaks the promise that the tools and data are portable.
4. **Offline is the default, not a fallback.** Local SQLite and zero-required-account are product promises that constrain architecture. Network and AI are enhancements that must degrade gracefully.
5. **Quiet coaching over gamified pressure.** Streaks, PRs, and "missed" vs "scheduled" are cues for self-understanding, not punishments or streak-shaming. The tone stays precise and athletic.

## Accessibility and inclusion

- Operate surface under fragmented attention: large thumb targets (44-56px FABs, 48px icon buttons), one-hand reachability, full keyboard and screen-reader support for set logging and navigation, `prefers-reduced-motion` disables continuous motion while preserving state opacity transitions.
- Timers and confirmations must remain operable with screen readers (`aria-busy`, `aria-label` on icon buttons, `role="button"` on heatmap cells with keyboard focus).
- Color must not be the sole signal for PRs, schedule states, or urgency. Pair it with shape, label, and icon.
