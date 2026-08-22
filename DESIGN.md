# Design

<!-- impeccable:design-schema 1 -->

## Visual World

**Tokyo Night split-flap plate proof**

A meet-day attempt board rendered as a design-annual plate proof in Tokyo Night. The sport's own Operate interface is the hero: no lifestyle photography, no neon halo, just the mechanical flap, the hairline, and the ink.

- **Material:** Platform rubber #1A1B26 and paper #D5D6DB with chip #24283B and #FFFFFF, matte dot-matrix void fill (8px radial) and 16px graticule for the interval clock. Every surface is a proof sheet with a hairline and no shadow.
- **Palette: Tokyo Night, colorblind-safe.** Ink #C0CAF5 / #1A1B26, stone #8A90B8 / #565A6E (4.7:1 on chip, 5.5:1 on bg / 4.7:1 on paper, 6.8:1 on chip), hairline 1px muted blue, plate accents red #F7768E / blue #7AA2F7 / gold #E0AF68 / green #9ECE6A each paired with shape/pattern (solid, double-ring, hatch, solid) and label, never color alone. Amber #E0AF68 for live, seal #BB9AF7 as proof mark. All text pairings meet 4.5:1 (body) and 3:1 (large) in both themes.
- **Type:** JetBrains Mono carries display, body, tokens, and numerals as the committed terminal voice. Share Tech Mono renders the seven-seg rest readout. Both ship self-hosted under `/fonts` with Google subset blocks mirrored (OFL), so the offline-first promise holds. No italics, -0.02em display, tabular nums for every load and timer.
- **Grid:** 12-col with 40px gutters on desktop, phone-width container (640px max) on mobile. Hairline 1px rules divide, crosshair + at plate corners, 8px dot matrix fills voids. Chrome is unified per breakpoint: the sticky top rail (mono PHYSIQUINATOR mark left, primary tabs right, three registration lights far right) serves viewports 769px and up; the floating bottom pill serves phones. Both share chip surface, hairline edge, mono caps tabs, and the accent underline active state. Immersive workout pages hide chrome entirely.
- **Motion:** One authored moment: flap tick (scale + dot, 120ms) and seven-seg color shift (220ms), plus `prefers-reduced-motion` that collapses to opacity only. No scattered hovers, no entrance parade.
- **Icon:** Single stroke, `1em` square, `currentColor`, from Material Icons. Drawn, not emoji.

## Theme

MudBlazor theme mirrors the world:

- **Light:** Primary #24283B (ink, 12:1), Secondary #34548A, Success #33635C, Error #8C4351, Background #D5D6DB, Surface #E9E9ED, TextPrimary #1A1B26 (14:1), TextSecondary #565A6E (6.2:1), Lines #B4B8C5.
- **Dark:** Primary #7AA2F7 (blue, 6.1:1, colorblind-safe), Secondary #7DCFFF, Success #9ECE6A, Error #F7768E, Background #1A1B26, Surface #24283B, TextPrimary #C0CAF5 (9.1:1), TextSecondary #8A90B8 (4.7:1 on surface, AA), Lines rgba(192,202,245,0.14).
- **Shape:** 0 radius (mechanical flap), 0.14em mono caps, tabular nums everywhere. `DefaultBorderRadius: 0px`.

## Surfaces

- **Home (Operate):** Sticky top rail (PHYSIQUINATOR TOKYO • REG. 01 left, IDLE/LIVE/REST center, ◯◯● right), then flap board: left meet card (IN PROGRESS hero + 3 flap tiles STREAK/THIS WEEK/LAST) + right 53-week heatmap (micro flap tiles, legend WEEKS 01—53 • TODAY ○ • SCHEDULED ◐ • MISSED —). Below, triplicate plan cards (header strip with PLAN • 01 - FORM A • CARBONLESS, 01 • 7 EXERCISES + plate load dots + Done today, foot with ink START). FABs: AI ASSISTANT (secondary) + NEW PLAN (primary) at 44px, mono caps, hairline, 0 radius, positioned to not obscure.
- **History:** Same top rail and heatmap (full-width), then 4 flap tiles (STREAK/LONGEST/THIS WEEK/LAST WEEK) + Bodyweight chart (hidden legend, 3.5px line) + session list as scoring desk rows (title, mono subtitle, delete, chevron) with hairline and dot matrix.
- **Settings:** Top rail + search + 6 expansion panels (Appearance, Units, Rest timer, User profiles, Workout schedule, Account) each with 44px icon, hairline, 0 radius.
- **Plan:** Header Edit plan, then Plan details card (Plan name input with filled slot #E1E2E7, DEFAULT SETS/REST steppers with 44px minus/plus, 20px value), then Exercise list (handle 44px, chevron, 8px dot matrix).
- **Workout:** Session stats bar (ELAPSED/TIME, VOLUME as flap bar with divider hairline), then Exercise accordion (name 18px 800, set rows with 44px stepper, Log set 44px primary), then Rest timer panel (16px graticule, 4.5-6.5rem seven-seg, amber/urgent color shift, 6px edge track with scaleX fill).
- **Dialogs / Snackbars:** 0 radius, hairline-strong border, overlay `rgba(12,13,15,0.56)` + `blur(2px)`, mono titles.

## Colorblind and contrast checks

- Every status uses shape + label + icon alongside color: heatmap (solid/hollow/dashed/circle + TODAY ring + SELECTED ring + SCHEDULED hollow), plan load dots (solid red, double-ring blue, hatched gold, solid green + label), rest timer (amber vs error + text + icon), success vs error (check vs ✕ + label, not hue alone).
- Measured: Light. Ink #1A1B26 on paper #D5D6DB 13.0:1, stone #565A6E on paper 4.7:1 and on chip #FFFFFF 6.8:1. Dark. Stone #8A90B8 on chip #24283B 4.7:1 and on background #1A1B26 5.5:1. All body ≥4.5:1, large ≥3:1 in both themes; hairlines are decorative, not text.
- Tested on Home, History, Settings, Plan, Workout in both themes and at 390/1280 viewports; no text relies on color alone.

## Motion

- Flap tick on stat change, heatmap hover scale 1.15, FAB in 0.28s cubic-bezier(.19,1,.22,1), rest edge `scaleX` (composited), dialog `translateY(12px) scale(0.98)` to `opacity 1`. One authored moment per surface, exponential ease-out, content visible by default.
- `@media (prefers-reduced-motion: reduce)` collapses to `opacity 150ms` only, no transform.

## Notes

- The rest timer's 16px two-axis grid (`linear-gradient(to right, var(--pl-hairline) 1px, transparent 1px), linear-gradient(to bottom, var(--pl-hairline) 1px, transparent 1px)`) is a functional graticule for the 10-division measurement surface, not decorative. It ships on `.rest-timer-digits`. Detector advisory `codex-grid-background` is acknowledged and retained.
- No `border-left` >1px, no gradient text, no glass blur decoration, no section numbers, no kicker/eyebrow. Enforced 2026-08: AI bridge result rows carry status via an 8px solid/delta marker shape instead of side-tab borders; the update-dialog blockquote uses a 1px hairline; MudBlazor elevation shadows are globally suppressed (`.mud-button-root`, `.mud-paper`, FAB, popovers) per the no-shadow material rule. Inset selection rings remain the only shadow-like device.
- Flap tick ships as `.flap-tick` (120ms scaleY squash), applied to stat-card values whose elements re-key on value change so the animation replays exactly when the number flips.
- Heatmap keyboard contract: the grid is a roving-tabindex `role="grid"` with one tab stop total, arrows move between days, future cells are skipped, default focus lands on today. A skip link targets `#main-content`.
- Shipped-chrome record: the top rail and bottom pill split duty by viewport as described in Grid; body type is JetBrains Mono by commitment, not pending change; the desktop 12-col/40px grid remains deferred in favor of the phone-width container until a tablet layout pass.
- Data voice: dates render ISO yyyy-MM-dd everywhere (tooltips, subtitles, PRs); volumes round to whole units (9,080 kg, never 9,079.9 kg); chart Y maxima snap to 1/2/2.5/3/4/5-decade steps so ticks land on round values.
- Legend keys and status glyphs are drawn shapes (`hm-glyph` borders, dashes, hatches), never font-dependent Unicode characters.
- Floating FABs park off-screen while scrolling down and return on the first upward scroll, so they never sit over plan-card actions; reduced-motion keeps that behavior as a plain opacity fade with no transform.
