# Design

<!-- impeccable:design-schema 1 -->

## Visual world

**Tokyo Night split-flap plate proof**

A meet-day attempt board rendered as a design-annual plate proof in Tokyo Night. The sport's own Operate interface is the hero: no lifestyle photography, no neon halo, just the mechanical flap, the hairline, and the ink.

- **Material:** Platform rubber #1A1B26 and paper #D5D6DB with chip #24283B and #FFFFFF, matte dot-matrix void fill (8px radial). Every surface is a proof sheet with a hairline and no shadow.
- **Palette: Tokyo Night, colorblind-safe.** Ink #C0CAF5 / #1A1B26, stone #8A90B8 / #565A6E (4.7:1 on chip, 5.5:1 on bg / 4.7:1 on paper, 6.8:1 on chip), hairline 1px muted blue, plate accents red #F7768E / blue #7AA2F7 / gold #E0AF68 / green #9ECE6A each paired with a shape and a label (solid, double-ring, hatch, solid), never color alone. Amber #E0AF68 for live, seal #BB9AF7 as proof mark. All text pairings meet 4.5:1 (body) and 3:1 (large) in both themes.
- **Type:** JetBrainsMono Nerd Font Mono carries display, body, tokens, and numerals. Chakra Petch Bold 700 carries the workout timer. Both are self-hosted under `/fonts` with Nerd-patched glyphs for JetBrainsMono and subset woff2 for Chakra Petch, so the app works offline. No italics. Display uses -0.02em tracking. Every load and timer uses tabular numbers.
- **Grid:** 12 columns with 40px gutters on desktop. On mobile the container is phone width at 640px max. Hairline 1px rules divide the surface. Crosshair marks sit at plate corners. An 8px dot matrix fills voids. Chrome is unified per breakpoint. The sticky top rail serves viewports at 769px and up. It shows the mono PHYSIQUINATOR mark on the left, primary tabs on the right, and three registration lights on the far right. The floating bottom pill serves phones. Both share chip surface, hairline edge, mono caps tabs, and the accent underline for the active state. Immersive workout pages hide chrome entirely.
- **Motion:** One authored moment: flap tick (scale + dot, 120ms) and seven-seg color shift (220ms), plus `prefers-reduced-motion` that collapses to opacity only. No scattered hovers, no entrance parade.
- **Icon:** Single stroke, `1em` square, `currentColor`, from Material Icons. Drawn, not emoji.

## Theme

MudBlazor theme mirrors the world:

- **Light:** Primary #24283B (ink, 12:1), Secondary #34548A, Success #33635C, Error #8C4351, Background #D5D6DB, Surface #E9E9ED, TextPrimary #1A1B26 (14:1), TextSecondary #565A6E (6.2:1), Lines #B4B8C5.
- **Dark:** Primary #7AA2F7 (blue, 6.1:1, colorblind-safe), Secondary #7DCFFF, Success #9ECE6A, Error #F7768E, Background #1A1B26, Surface #24283B, TextPrimary #C0CAF5 (9.1:1), TextSecondary #8A90B8 (4.7:1 on surface, AA), Lines rgba(192,202,245,0.14).
- **Shape:** 0 radius (mechanical flap), 0.14em mono caps, tabular nums everywhere. `DefaultBorderRadius: 0px`.

## Surfaces

- **Home operate:** The sticky top rail shows PHYSIQUINATOR TOKYO REG. 01 on the left, IDLE, LIVE, and REST in the center, and three registration lights on the right with two hollow and one solid. Then the flap board shows the left meet card. It has an IN PROGRESS hero and three flap tiles for STREAK, THIS WEEK, and LAST. On the right it shows the 53-week heatmap with micro flap tiles. Its legend shows WEEKS 01 to 53 with keys for TODAY, SCHEDULED, and MISSED. Below are triplicate plan cards. Each has a header strip with PLAN 01 - FORM A - CARBONLESS, a line with 01 - 7 EXERCISES plus plate load dots and Done today, and a foot with an ink START. FABs sit at 44px with mono caps, hairline, and 0 radius. They use AI ASSISTANT as secondary and NEW PLAN as primary and do not obscure content.
- **History:** The page uses the same top rail and a full-width heatmap. Then it shows four flap tiles for STREAK, LONGEST, THIS WEEK, and LAST WEEK. Next is the bodyweight chart. It hides the legend and draws a 3.5px line. Below is the session list as scoring desk rows. Each row has a title, a mono subtitle, delete, and chevron with hairline and dot matrix.
- **Settings:** The page has the top rail and search. Then it shows six expansion panels for Appearance, Units, Rest timer, User profiles, Workout schedule, and Account. Each panel has a 44px icon, hairline, and 0 radius.
- **Plan:** The header shows Edit plan. Then the plan details card shows a Plan name input with a filled slot at #E1E2E7, DEFAULT SETS and REST steppers with 44px minus and plus and a 20px value. Then the exercise list shows a handle at 44px, chevron, and 8px dot matrix.
- **Workout:** Session stats bar shows ELAPSED and TIME and VOLUME as a flap bar with a divider hairline. Then the exercise accordion shows names at 18px 800 with 44px steppers and a 44px Log set button. Then the rest timer panel shows a clean chip surface. Its digits are Chakra Petch Bold 700 at 4.2 to 5.8rem. Each digit sits in a fixed 1ch slot so the timer acts like a mono font and never shifts when the digit changes. The color is ink and shifts to amber and then to error red when time is short. A 6px edge track fills with scaleX.
- **Dialogs and snackbars:** 0 radius, hairline-strong border, overlay at rgba 12,13,15 at 0.56 with blur 2px, and mono titles. Dialogs center vertically when they fit the viewport and pin to the top only once they fill the space, which is when they would otherwise slide behind the floating FABs. The container reserves 76px of bottom padding so a full-height dialog always clears the dialog FABs.

## Colorblind and contrast checks

- Every status uses shape + label + icon alongside color. Heatmap days render solid, hollow, dashed, or circled, plus rings for TODAY and SELECTED and a hollow marker for SCHEDULED. Plan load dots come in solid red, double-ring blue, hatched gold, and solid green, each with a label. The rest timer pairs amber or error color with text and icon. Success versus error pairs a check or cross shape with a label, never hue alone.
- Measured: Light. Ink #1A1B26 on paper #D5D6DB 13.0:1, stone #565A6E on paper 4.7:1 and on chip #FFFFFF 6.8:1. Dark. Stone #8A90B8 on chip #24283B 4.7:1 and on background #1A1B26 5.5:1. All body text meets 4.5:1 and large text meets 3:1 in both themes. Hairlines are decorative, not text.
- Tested on Home, History, Settings, Plan, Workout in both themes and at 390 and 1280 viewports. No text relies on color alone.

## Motion

- Flap tick on stat change, heatmap hover scale 1.15, FAB in 0.28s cubic-bezier(.19,1,.22,1), rest edge `scaleX` (composited), dialog `translateY(12px) scale(0.98)` to `opacity 1`. One authored moment per surface, exponential ease-out, content visible by default.
- `@media (prefers-reduced-motion: reduce)` collapses to `opacity 150ms` only, no transform.

## Notes

- The rest timer's former 16px two-axis grid was removed on user request in August 2026. The readout now sits on the plain chip surface with only the hairline frame and urgency color. Detector advisory `codex-grid-background` is resolved, not retained.
- No `border-left` >1px, no gradient text, no glass blur decoration, no section numbers, no kicker or eyebrow. Added in August 2026: AI bridge result rows carry status via an 8px solid or delta marker shape instead of side-tab borders. The update-dialog blockquote uses a 1px hairline. MudBlazor elevation shadows are globally suppressed (`.mud-button-root`, `.mud-paper`, FAB, popovers) per the no-shadow material rule. Inset selection rings remain the only shadow-like device.
- Flap tick ships as `.flap-tick` (120ms scaleY squash), applied to stat-card values whose elements re-key on value change so the animation replays exactly when the number flips.
- Heatmap keyboard contract. The grid is a roving-tabindex `role="grid"` with one tab stop total. Arrow keys move between days, future cells are skipped, and default focus lands on today. A skip link targets `#main-content`.
- Shipped-chrome record. The top rail and bottom pill split duty by viewport as described in Grid. Body type is JetBrainsMono Nerd Font Mono by commitment, not pending change. The desktop 12-col and 40px grid stays deferred in favor of the phone-width container until a tablet layout pass.
- Data voice: dates render short and unambiguous everywhere ("8/22" for this year, "8/22/25" for other years, clock time alone for today). Volumes round to whole units (9,080 kg, never 9,079.9 kg). Chart Y maxima snap to 1, 2, 2.5, 3, 4, and 5-decade steps so ticks land on round values.
- Legend keys and status glyphs are drawn shapes (`hm-glyph` borders, dashes, hatches), never font-dependent Unicode characters.
- Floating FABs are always visible. The earlier scroll-linked parking made them vanish at unpredictable moments (for example after navigating without scrolling), so it was removed in August 2026 on user request.
