# Plan: selected UI and feature improvements

Date: 2026-09-02
Scope: the 15 items you picked from the review. One doc, one mode: explanation.

## What you asked for

- AI clipboard bridge as one guided flow with diff preview
- Superset display as a real grouping, not a chip
- Duplicate plan
- Exercise catalog autocomplete that still allows custom values
- Fix MudChart limits on ExerciseProgress and BodyweightProgress
- Share card chooser
- Session comparison
- All five AI assistant fixes
- Finish and polish bundle

## How we ship it

Five phases. Each ticket is one tracer bullet: testable, reviewable, shippable alone. No ticket breaks the incumbent design system `DESIGN.md:138` or the offline SQLite promise `PRODUCT.md:65`.

### Phase 1 — plan management quick wins

**T01 Duplicate plan**
- Where: `Physiquinator.Core/Services/WorkoutPlanService.cs:28` add `DuplicatePlanAsync(Guid id)` that clones `WorkoutPlan` with new `Id`, `CreatedAt = UtcNow`, `Name = $"{Name} Copy"`, `SortOrder = max+1`, new `ExercisePlan.Id` per row.
- UI: `Home.razor:131` `PlanCard` gets `OnDuplicate` param, add Duplicate to `PlanCard.razor:33` `MudMenu`. `PlanWorkout.razor:455` save path reuses `SavePlanAsync`.
- Accept: duplicate appears below original, undo snackbar works, history not affected.
- Verify: `dotnet test --filter PlanService` + manual home drag still works.

**T02 Autocomplete everywhere, custom still allowed**
- Today: `PlanExerciseSheet.razor:21` has `MudAutocomplete` with `CoerceText true` and `SearchExerciseNamesAsync:255` that merges catalog plus history. `PlanWorkout.razor:107` quick add is a plain `MudTextField` with no suggestions.
- Change: replace quick add field with the same `MudAutocomplete` component, extracted to `ExerciseNameAutocomplete.razor` so both entry points share `ExerciseCatalog.MergeSuggestionNames` `ExerciseCatalog.cs:211` plus `HistoryRepository.GetExerciseNamesAsync(100)`. Keep `CoerceText=true` so custom names save.
- Polish: show `LogType` icon and `BodyweightPercent` hint in suggestion row, pre-fill on select via `ExerciseCatalog.Find:200` already done in `PlanExerciseSheet.razor:275`.
- Accept: typing "bench" suggests Bench Press, Incline Dumbbell Bench, etc. Typing "My Cuban Press" with no match saves as custom.
- Verify: add custom, then create second plan and see custom appears in history suggestions.

### Phase 2 — workout clarity

**T03 Superset as grouping**
- Today: chip only `PlanExerciseRow.razor:21` `SupersetGroupId` A/B/C/D, no grouping, reorder can split a superset `PlanWorkout.razor:214`, workout deck shows one exercise at a time `Workout.razor:145` and `GetNextUncompletedExerciseInSameGroup:945` is unused.
- Change:
  - `PlanExerciseList.razor:8` groups consecutive exercises sharing `SupersetGroupId` into a visual block: 1.5px hairline-strong outer frame, 3px left edge in group color (`plate-red/blue/gold/green` `DESIGN.md:193`), internal divider 1px hairline.
  - `PlanExerciseRow.razor` keeps chip but adds connector dot per row.
  - Reorder: when dragging a grouped row, the whole block can move together if header is dragged, else single row move stays allowed but warns if it splits a group.
  - `Workout.razor:511` `NextExerciseInfo` checks same group: if current exercise is part of a superset and the partner is not done, show partner as Up Next with "SUPERSET B" kicker and no rest countdown between them `WorkoutSessionService` stays unchanged, only the deck decides whether to call `StartRest`.
- Accept: two exercises with group A appear as one fused plate, workout shows them paired, rest between them is skipped.
- Verify: `WorkoutSessionServiceTests` still pass, manual reorder preserves grouping.

### Phase 3 — charts, share, compare

**T04 Fix chart limits**
- Today: single `MudChart` line `ExerciseProgress.razor:110` with `ChartSeries<double>`, `YAxisTicks = TickStep` `ExerciseProgress.razor:423`, `SparseChartLabelBuilder.BuildLabels:405` with max 6, `ShowDataMarkers=false`.
- Change:
  - Keep `MudChart` but add: PR markers as overlay dots, tooltip on tap/hover with exact value and date, Y axis label via `FormatVolumeWithUnit` or weight, X sparse labels stay.
  - `ProgressChartShell.razor:1` gains `OnPointClicked` callback so tapping a point navigates to that session.
  - `BodyweightProgress.razor:260` same fix: add moving average series when entries >= 10, keep `EntryLimit 1000` but cap chart markers at `MaxChartMarkerEntries 60:120`.
  - `ChartAxisScale.cs:24` keep `SuggestYAxis`, add `SuggestYAxisMin` for bodyweight so Y never starts at 0.
- Accept: at least 2 series or marker overlays render, sparse labels never overlap, PR dot aligns with max.
- Verify: `ChartAxisScaleTests` and screenshot check on 4 vs 40 point histories.

**T05 Share card chooser**
- Today: fixed 400px off screen `WorkoutShareCard.razor:5` `shareCard.js:25` `CreateElementAsPngAsync`, no options.
- Change: before capture in `HistoryDetail.razor:733` `ShareSessionAsync` open `ShareCardOptionsDialog` with toggles: include volume, include PRs, per-exercise checkboxes, theme light/dark, format choice. Pass filtered `ShareCardExercise` list to `WorkoutShareCard` and set `data-theme` attr so `html2canvas` at scale 2 captures explicit hex `app-overrides.css:1442`.
- Keep fallback copy-to-text `HistoryDetail.razor:570` for when canvas fails.
- Accept: user can uncheck one exercise and share still renders 400px, text copy still works offline.

**T06 Session compare**
- Today: `History.razor:180` select bar only has Delete. `GetSetsForSessionAsync:624` exists but no diff.
- Change: add Compare button enabled when 2 selected, `History.razor:276` `ToggleSelect` caps at 2 or replaces oldest. New `SessionCompareDialog.razor` loads both sessions via `HistoryRepository.GetSetsForSessionAsync`, builds per-exercise table: set count, best weight, volume `PersonalRecordCalculator.ComputeVolume:70`, duration, and delta column `+1.2 kg` colored by sign, same `ExerciseWeightFormatter` for unit.
- Accept: selecting two sessions from different plans still compares shared exercise names, shows "exercise only in A/B" rows.

### Phase 4 — AI

**T07 Clipboard bridge as one flow**
- Today: two cards `AiChatView.razor:209` Generate and `AiChatView.razor:285` Paste plus three handlers `GenerateAndCopyPromptAsync:521`, `InspectPastedActions:546`, `ApplyActionsAsync:557`, no diff before apply.
- Change: `AiChatView.razor` clipboard tab becomes a 3-step wizard with progress line 1 Generate, 2 Paste, 3 Review and apply. Step 3 shows a diff table per action:
  - `create_workout_plan` lists exercises with sets/reps/weight.
  - `update_workout_plan` loads current plan via `WorkoutPlanService.GetPlanAsync`, computes added/removed/modified exercises and weight deltas, renders as table not JSON.
  - `log_bodyweight_entry` shows previous vs new for same date.
- Keep `AiClipboardBridgeService.ParseResponse:173` and `BuildBridgeAction:358` but add `BuildDiffAsync` that resolves `planId` to plan when possible. Validation errors still show `ai-bridge-action-card--invalid:708`.
- Accept: paste a response with one update and one create, see two diff tables and an Apply 2 button that still respects `IsDestructive` warn pill.

**T08a Connection test without polluting chat**
- Today: `Settings.razor:817` `TestAiConnectionAsync` saves settings then calls `AiAssistant.SendUserMessageAsync("Hello...")` which appends to `AiAssistantService._messages` and persists via `SaveHistory:120`.
- Fix: add `AiAssistantService.TestConnectionAsync()` that calls `OpenAiCompatibleClient.StreamChatCompletionAsync` with a single user message isolated from `_messages` and `_apiHistory`, returns success/error without mutating chat. Wire `Settings.razor:817` to it.

**T08b Model picker as one control**
- Today: free text `Model Name` `Settings.razor:370` plus a second `Fetched models` select `Settings.razor:376` plus `OnFetchedModelPicked:743`.
- Fix: merge into one `MudAutocomplete` `CoerceText=true` with items from `_availableModels` plus any custom typed value. Keep `FetchModelsAsync:752` but debounce it when provider or base URL changes.

**T08c Offline degrader**
- Today: `AiChatView.razor:58` `IsApiKeyMissing` shows a banner but quick actions `AiChatView.razor:153` stay visible disabled.
- Fix: when missing, hide quick actions, promote clipboard tab with a one-line explainer and a direct Settings link `NavigateToSettings:664`.

**T08d Context range**
- Today: `AiBridgePromptOptions` in `AiBridgeAction.cs:6` has 3 bools, no range.
- Fix: add `HistoryRange Days30/Days90/AllTime` field, pass to `AppendTrainingHistoryStatsContextAsync:114` so prompt uses `GetRecentSessionsAsync(5)` vs 20 and trims plan dump to exercise names only for AllTime.

**T08e Tool parity visibility**
- Add a read-only panel in Settings AI section listing 15 tools from `AiToolRegistry.GetAllTools()` with name and `Description` `IAiTool.cs:3` so users know they can say "log 82 kg today".

### Phase 5 — finish and polish

**T09a CSS weight**
- Today: `app-overrides.css` 6,116 lines, 242 KB, single import `tokens.css:1`.
- Split into `app-shell.css`, `workout.css`, `charts.css`, `ai.css` and lazy load workout slice only on `/workout/*` via JS dynamic import. Keep `tokens.css` 853 lines as the single source for `PhysiquinatorThemes.cs:104` and `DesignTokens.cs:342`.

**T09b Typography**
- Keep `Departure Mono` for numbers, labels, chips `DESIGN.md:211` but render AI markdown `AiChatView.razor:119` `markdown-body` and `MudText` body with a proportional stack `Inter, system-ui` at 14px so chat is readable. Mono stays for timer `DESIGN.md:50` and stat values.

**T09c Motion and polish**
- Add `prefers-reduced-motion` for rest pulse `app-overrides.css:1929` and map `animation-duration 150ms` already exists. Add focus ring 2px volt `DESIGN.md:274` audit.

**T09d Accessibility**
- `ActivityHeatmap.razor:37` already roving tabindex, add `aria-describedby` summary "3 workouts this week, streak 5, scheduled Tue/Thu". Rest timer announce every 5s not every second via `aria-live polite` in `RestTimerPanel.razor`.

**T09e PWA for Wasm**
- `Physiquinator.Wasm/wwwroot` has no manifest today. Add `manifest.json`, `theme-color` meta to `index.html:9`, and a minimal `service-worker.js` that caches `blazor.webassembly.js` and font woff2 so the static build works offline after first load. No change to `Physiquinator.Web` auth.

## Dependency and order

T01 and T02 no deps, ship first. T03 touches both plan and workout so after T01/T02. T04/T05/T06 parallel. T07 depends on T08d for range field. T08a/T08b/T08c/T08e can ship before T07. T09a last because it touches every CSS line.

Suggested build order:

1. T01, T02, T08a
2. T03, T08b, T08c, T08e
3. T04, T05, T06
4. T07 and T08d
5. T09a-e

## Verification

Each ticket runs `dotnet format --verify-no-changes` and `dotnet test Physiquinator.Tests/Physiquinator.Tests.csproj` before merge. Chart and share tickets add a screenshot check via `tools/screenshot-generator/screenshot-web.js`. PWA ticket checks Lighthouse PWA score on `Physiquinator.Wasm` publish output.

## Out of scope kept

No cloud sync, no team sharing, no new AI model pricing `PRODUCT.md:71`. Tokens palette stays in `tokens.css` and `DesignTokens.cs`.

## Next step

Tell me which tickets to start now. I recommend T01 plus T02 plus T08a as a first slice, about one session, then T03.
