---
name: Physiquinator
description: Cyberpunk brutalist field-terminal design system for the offline-first workout tracker
colors:
  void: "#0E0F17"
  well: "#141624"
  chip: "#181A2A"
  chip-well: "#22253B"
  ink: "#C0CAF5"
  ink-dim: "#A9B1D6"
  stone: "#8A90B8"
  hairline: "#282B42"
  hairline-strong: "#414770"
  volt-yellow: "#FAFF00"
  tokyo-blue: "#4D7FFF"
  neon-cyan: "#00E5FF"
  electric-violet: "#C084FC"
  cyber-magenta: "#FF0055"
  toxic-green: "#A3E635"
  signal-amber: "#E0AF68"
  plate-red: "#FF0055"
  plate-blue: "#4D7FFF"
  plate-gold: "#FAFF00"
  plate-green: "#A3E635"
typography:
  display:
    fontFamily: "'Departure Mono', 'JetBrains Mono', ui-monospace, monospace"
    fontSize: "22px"
    fontWeight: 900
    lineHeight: 1.1
    letterSpacing: "0.02em"
    fontFeature: "tnum, lnum"
  title:
    fontFamily: "'Departure Mono', 'JetBrains Mono', ui-monospace, monospace"
    fontSize: "18px"
    fontWeight: 800
    lineHeight: 1.2
    letterSpacing: "-0.015em"
  label:
    fontFamily: "'Departure Mono', 'JetBrains Mono', ui-monospace, monospace"
    fontSize: "11px"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "0.12em"
  body:
    fontFamily: "'Departure Mono', 'JetBrains Mono', ui-monospace, monospace"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.5
  timer:
    fontFamily: "'Departure Mono', 'JetBrains Mono', ui-monospace, monospace"
    fontSize: "clamp(4.8rem, 20vw, 6.8rem)"
    fontWeight: 900
    lineHeight: 0.88
    fontFeature: "tnum, lnum"
  scale:
    micro: "11px"
    body: "14px"
    name: "16px"
    header: "18px"
    stat: "22px"
    hero: "33px"
rounded:
  none: "0px"
spacing:
  page-x: "16px"
  page-max: "640px"
  touch: "44px"
  rail-h: "56px"
  pill-h: "56px"
  fab: "48px"
  fab-mini: "40px"
  gap-xs: "4px"
  gap-sm: "8px"
  gap-md: "12px"
  gap-lg: "16px"
components:
  button-primary:
    backgroundColor: "{colors.volt-yellow}"
    textColor: "#10111A"
    rounded: "{rounded.none}"
    padding: "0 16px"
    height: "44px"
    typography: "{typography.label}"
  button-secondary:
    backgroundColor: "{colors.chip-well}"
    textColor: "{colors.ink}"
    rounded: "{rounded.none}"
    padding: "0 16px"
    height: "44px"
    typography: "{typography.label}"
  button-accent:
    backgroundColor: "{colors.tokyo-blue}"
    textColor: "#10111A"
    rounded: "{rounded.none}"
    height: "44px"
    typography: "{typography.label}"
  fab-primary:
    backgroundColor: "{colors.volt-yellow}"
    textColor: "#000000"
    rounded: "{rounded.none}"
    size: "48px"
  fab-secondary:
    backgroundColor: "{colors.chip-well}"
    textColor: "{colors.ink}"
    rounded: "{rounded.none}"
    size: "48px"
  chip:
    backgroundColor: "{colors.chip}"
    textColor: "{colors.ink}"
    rounded: "{rounded.none}"
    height: "24px"
    typography: "{typography.label}"
  card:
    backgroundColor: "{colors.chip}"
    textColor: "{colors.ink}"
    rounded: "{rounded.none}"
    padding: "12px"
  input:
    backgroundColor: "{colors.chip}"
    textColor: "{colors.ink}"
    rounded: "{rounded.none}"
    height: "44px"
    typography: "{typography.body}"
  nav-tab:
    backgroundColor: "{colors.tokyo-blue}"
    textColor: "#10111A"
    rounded: "{rounded.none}"
    height: "56px"
    typography: "{typography.label}"
  stat-card:
    backgroundColor: "{colors.chip}"
    textColor: "{colors.ink}"
    rounded: "{rounded.none}"
    height: "88px"
---

# Design System: Physiquinator

## Overview

**Creative North Star: "Tokyo Night split-flap plate proof"**

A meet-day attempt board rendered as a design-annual plate proof in Tokyo Night. The sport's own Operate interface is the hero: no lifestyle photography, no neon halo, just the mechanical flap, the hairline, and the ink. Every screen reads like a printed weight-class sheet that happens to count down in real time.

The material is matte and printed. Surfaces are flat plates of platform rubber and paper separated by 1.5px hairlines, never by shadows. A quiet 24px grid or 8px dot matrix fills voids so empty space still feels machined. Color is disciplined: stone and ink carry the reading, and one high-voltage accent per surface (usually volt yellow) marks the one live thing. Status is never color alone; every state pairs hue with a drawn shape and a label.

The app is used between sets with one thumb, so the system optimizes for large targets, instant legibility, and numbers that never shift. Everything is mono, everything is tabular, everything is uppercase where it is a label.

**Key Characteristics:**

- Flat plates with 1.5px hairlines, radius 0 everywhere, no shadows at rest
- Tokyo Night palette: deep navy voids, periwinkle ink, one volt accent per surface
- Departure Mono for every glyph, tabular numerals everywhere, uppercase micro-labels
- 44px minimum touch targets, phone-width 640px container, chrome that swaps by breakpoint
- One authored motion moment per surface (flap tick), reduced-motion collapses to opacity

## Colors

Tokyo Night: a deep blue-violet night ground with periwinkle ink, punctuated by one high-voltage accent. The palette is colorblind-safe: blue, yellow, green, and red differ in luminance, never in hue alone.

### Primary

- **Volt Yellow** (#FAFF00, both themes): the live wire and the active state. Rest timer digits and edge track, primary buttons, NEW PLAN, active tabs everywhere (nav pill, settings tabs, metric tabs, schedule days, filter chips), in-progress badges, heatmap today ring. In light, text-sized volt prints as #6E6400 (see the light theme mapping). At most one volt surface per viewport region; its rarity is the point.
- **Volt Fill** (#FAFF00, both themes): large-area volt fills only (CTA ramps, FABs, active tabs, user chat bubbles). Text sits on the `--pl-volt-fg` token: ink on neon yellow in both themes (about 15:1). Borders on filled controls stay ink via `--pl-volt-edge` in both themes. Small volt-colored text uses the text-safe token.
- **Circuit Blue** (#4D7FFF dark / #1E5EFF light): the data and information accent for chart lines, selection highlights, and informational states. Colorblind-safe against every other accent. It no longer owns actions; volt does.

### Secondary

- **Electric Violet** (#C084FC dark / #7C3AED light): the seal. Proof marks, PR celebration, tertiary chart series.
- **Neon Cyan** (#00E5FF dark / #0891B2 light): scheduled and informational states, heatmap planned dots, secondary chart series.

### Tertiary

- **Acid Green** (#A3E635 dark / #65A30D light): success, completed heatmap cells, completed set pills.
- **Cyber Magenta** (#FF0055 dark / #E11D48 light): errors, destructive confirmations, urgent timer.
- **Signal Amber** (#E0AF68 dark / #D97706 light): warnings and warm-up badges. In light mode amber and volt collapse toward one rich yellow because the neon pair fails contrast on white.

### Neutral

- **Void** (#0E0F17 dark / #D5D6DB light): page ground.
- **Well** (#141624 dark / #E9E9ED light): recessed ground behind stat labels.
- **Chip** (#181A2A dark / #FFFFFF light): card and plate surface.
- **Chip Well** (#22253B dark / #F5F5F7 light): wells inside cards, steppers, secondary buttons, table heads.
- **Ink** (#C0CAF5 dark / #1A1B26 light): primary text, 9.1:1 on chip in dark, 14:1 in light.
- **Ink Dim** (#A9B1D6 dark / #24283B light): secondary emphasis.
- **Stone** (#8A90B8 dark / #565A6E light): secondary text, 4.7:1 on chip dark, 6.8:1 light.
- **Hairline** (#282B42 dark / #B4B8C5 light): 1.5px decorative rules.
- **Hairline Strong** (#414770 dark / #9AA0B5 light): 1.5px structural borders on cards, inputs, buttons.

### Plate load colors

Plate dots encode barbell load with shape plus hue: **Plate Red** (#FF0055 / #E11D48) solid 25, **Plate Blue** (#4D7FFF / #1E5EFF) double-ring 20, **Plate Gold** (#FAFF00 / #D97706) hatched 15, **Plate Green** (#A3E635 / #65A30D) solid 10. Each dot carries its number as text; hue is reinforcement, never the message.

### Named Rules

**The Shape-Plus-Label Rule.** Every status pairs color with a drawn shape and a text label. Heatmap days render solid, hollow, dashed, hatched, or circled. Timer urgency shifts color and text together. No state relies on hue alone.

**The One Volt Rule.** Volt yellow marks the single live or primary element per region. If two things glow, one of them is wrong.

**The Contrast Floor Rule.** Body text meets 4.5:1 and large text 3:1 in both themes. Measured pairs: ink on chip 9.1:1 dark and 14:1 light, stone on chip 4.7:1 dark and 6.8:1 light. Hairlines are decorative and exempt.

### Light theme mapping

The light theme keeps every role and swaps the night for warm print stock: ground #E5E1D2 bone paper, chip #FFFFFF (a white plate laid on the bone), wells #F0EDE2, raised paper #EFECE0, deep recess #D8D3C0, ink #1A1B26, warm slate #565349, hairlines #C6C1AB and #A39D82. Volt survives the switch, in two prints: fills stay #FAFF00 under #10111A ink text (buttons, FABs, active tabs, chips in both themes), and text-sized volt prints as #6E6400 deep olive-volt ink (timer digits, first stat value, volt borders and hover accents), which lands about 6:1 on white. The timer edge track and TIME'S UP plate carry the bright fill so the surface still glows. Accents go vibrant: electric purple #7C3AED and vivid red #E11D48 carry the tertiary roles, teal #0369A1 and leaf #166534 print the data accents, and acid lime #65A30D keeps success. Grid lines go from rgba(192,202,245,0.05) to rgba(26,27,38,0.26), which lands the 24px checker at the same perceived weight as dark. The page aurora: candle white top, violet whisper bottom corner.

**The Visible-Ladder Rule.** Surface steps must be tellable apart in both themes, not just in dark. In light, chip (#FFFFFF) and chip-well (#F0EDE2) differ by about 6 luminance points so card header bars, wells, steppers, and table heads read as their own layer. If two adjacent steps look identical, deepen the lower one.

## Typography

**Display Font:** Departure Mono (fallback JetBrains Mono, then ui-monospace)
**Body Font:** Departure Mono (fallback JetBrains Mono, then ui-monospace)
**Label/Mono Font:** Departure Mono, the same face at micro sizes with wide tracking

One family carries every role. Departure Mono is a pixel monospace in the terminal tradition: square, chunky, self-assured. It ships a single 400 weight, so the app's requested 600 to 900 weights are synthesized by the browser. JetBrains Mono sits behind it in the stack as the self-hosted glyph-coverage fallback for characters the pixel face lacks. All fonts live under `Physiquinator.UI/wwwroot/fonts`, so the app works offline. No italics anywhere.

### The Eleven-Pixel Grid Rule

Departure Mono is drawn on an 11px unit. **For pixel-perfect results, set the font size to increments of 11px: 11, 22, 33, 44.** At those sizes every pixel lands on the layout grid and glyphs stay razor sharp. At other sizes the renderer resamples the bitmap grid and strokes go soft.

New styles must snap to the 11px ladder. The ladder is in force everywhere the type is a label, a title, a stat, or a timer readout: buttons and micro-labels render at 11px, page titles and stat values at 22px, and the timer digits use `round(20vw, 11px)` so they land on the ladder at every viewport width. Grandfathered off-ladder sizes are the reading sizes the ladder cannot express without doubling: body text at 14px, plan and exercise names at 16px, section headers at 18px, the 16px input floor that keeps iOS Safari from zooming, and the 7px stamps inside the 14px load dots, which are drawn glyphs rather than text.

### Hierarchy

- **Display** (900, 22px, line-height 1.1): page titles in the plate header, home included. Uppercase, tracked +0.02em, plain ink with no shadow or plate.
- **Title** (800, 16 to 18px, line-height 1.2): card titles, plan names, exercise names, section headers. Uppercase for card headers, sentence case for exercise names.
- **Stat** (800, 22px, line-height 1.1): stat card values. Tabular numerals, one accent color per card.
- **Body** (400 to 700, 14px, line-height 1.5): reading text, AI chat messages, descriptions.
- **Label** (700 to 800, 11px, letter-spacing 0.08 to 0.14em, uppercase): micro-labels under stats, button text, table heads, chips, badges. This is the system's signature voice: tiny, wide-tracked, shouting politely.
- **Timer** (900, clamp(77px, round(20vw, 11px), 110px), line-height 0.88): rest timer digits. The round() snaps the viewport-relative size onto the 11px ladder at every width. Each digit sits in a fixed 1ch slot so the countdown never shifts. `font-synthesis: none` keeps the digits clean.

### Named Rules

**The One-Family Rule.** One typeface for every role. Hierarchy comes from size, weight, tracking, and case, never from a second family.

**The Tabular-Numerals Rule.** Every number renders with `font-variant-numeric: tabular-nums lining-nums` and `font-feature-settings: "tnum" 1, "lnum" 1`. Weights, reps, times, and timers align in columns and never jiggle.

**The Shouting-Micro-Label Rule.** Labels under 12px are always uppercase with 0.08em or wider tracking and weight 700 or more. They are machine stamps, not sentences.

## Layout

A phone-width column in a chrome that swaps by breakpoint. The page container maxes at 640px with 16px inline padding, 16px top padding, and 24px bottom padding.

- **Chrome split.** The bottom pill is the only chrome at every width: fixed, centered, 12px from the bottom, max 440px wide (52px tall with row tabs at 960px and up). The former desktop top rail was retired in August 2026 so big screens keep the same thumb-first navigation, centered over the content column instead of stretched across the window.
- **Immersive mode.** Active workout pages hide all chrome so the timer and steppers own the screen.
- **Grids.** The shell and scroll surfaces carry a 24px hairline grid background. Chart plates carry a 16px grid. Voids can fill with an 8px dot matrix. All are decorative and sit under the hairline contrast floor.
- **Touch.** 44px minimum height for every button, tab, stepper, list row action, and input. FABs are 48px (40px mini), fixed, always visible, and stack clear of the nav pill (84px bottom offset above a pill). From 769px up, FABs anchor to the centered content column (`calc(50% - 320px - 64px)` from each edge) instead of the viewport edges, so they sit beside the content instead of drifting toward the window frame.
- **Breakpoints.** 520px caps the pill, 540px widens AI quick actions to four columns, 576px widens history stats to four columns, 768/769px swaps chrome, 960px enables desktop spacing.
- **Density.** Cards pad 10 to 16px. Lists gap 8 to 12px. Stacks gap 4/8/12/16px. Nothing floats loose; every block sits on the 8px rhythm.

## Elevation & Depth

The world is print, so depth comes from registration, not optics. Since August 2026 plates cast hard offset shadows with zero blur, and blurred shadows stay banned.

- **Plate shadows.** Solid, zero-blur offsets in one shadow voice per theme: deep ink `#1B1E33` on paper in light, pure black on void in dark. Three thick sizes carry hierarchy: sm 3px for small controls (active tabs, chips, bubbles), md 5px for cards and panels, lg 8px for floating chrome (nav pill) and overlays (dialogs). Shadows pair with a 1px inset top bevel highlight (`rgba(255,255,255,0.65)` light, `rgba(192,202,245,0.07)` dark) so plates read as machined stock.
- **Press physics.** Volt CTAs sit on an sm shadow and translate 3px into it on press; FABs sit on md and translate the full 5px. The surface physically drops.
- **Interactive lift.** Clickable cards (plan, session, exercise picker) translate 2px up-left on hover while their offset grows to 7px.
- **Fused blocks.** The workout stats plate and rest timer panel fuse into one unit during a session; the parent `.workout-top--with-timer` casts the single shadow so the seam never double-stacks.
- **Shader washes.** Static, decorative gradients sit under content: one aurora radial per corner on the page grounds (blue/violet dark, candle white and violet light), and CRT scanlines over the splash screen. The timer surface stays clean: no scanlines, no text effects, urgency is color plus the pre-existing pulse alone.
- **Contrast opt-out.** `prefers-contrast: more` strips every plate shadow; separation returns to borders alone.

### Named Rules

**The Hard-Offset Rule.** Every shadow is a solid-color offset with zero blur. If a shadow has a blur radius or a soft edge, delete it. MudBlazor's default elevation shadows stay globally suppressed.

**The Sharp-Edges Rule.** Every drawn effect is zero-blur and text carries no shadow at all: no outlines, no halos. Rings and hatches use hard offsets only. If a shadow has a blur radius, delete it.

## Shapes

Radius 0 everywhere. `--radius: 0px` and MudBlazor `DefaultBorderRadius: 0px` enforce it on buttons, cards, dialogs, chips, inputs, and images. Corners are machined, not softened.

Borders are the shape language: 1.5px solid hairlines for decoration and 1.5px solid hairline-strong for structure (card frames, input outlines, button edges). Dashed 1.5px borders mark scheduled or future states. Focus is a 2px solid outline (ink in light, volt in dark) with 2px offset. Selected states draw inside with the inset ring.

Glyphs are drawn, never typed: heatmap legend keys, load dots, hatches, and registration lights are CSS shapes (borders, gradients, pseudo-elements). No font-dependent Unicode status characters.

## Components

### Buttons

- **Shape:** radius 0, 1.5px hairline-strong border, 44px minimum height.
- **Primary:** volt yellow fill (#FAFF00), #10111A text, 1.5px #10111A border in dark. Light theme keeps the volt fill with an ink border.
- **Secondary:** chip-well fill, ink text, hairline-strong border. Hover shifts to chip with a volt border.
- **Accent:** volt fill with ink text for primary MudBlazor actions, identical in both themes.
- **Type treatment:** 11px, weight 700, uppercase, +0.12em tracking. No sentence-case buttons.
- **Error confirm:** light is white on #BE123C (6.4:1), dark is #10111A on #FF0055 (4.8:1). Both audited; do not trust library defaults for filled error buttons.
- **Focus:** 2px outline, ink in light and volt in dark.

### FABs

48px squares, radius 0, fixed corners, always visible (never scroll-parked). Primary is volt with black icon and volt glow. Secondary is chip-well with ink icon and hairline border. Dialog FABs pin to screen corners at 16px. On pages with the bottom pill, FABs float at 84px so they clear it. Undo sits bottom-left; primary actions bottom-right.

### Chips

24px tall, radius 0, hairline border, 11px uppercase mono text. Filter chips fill volt when selected. The plan count chip inverts: volt fill with dark text in dark mode, ink fill with volt text in light.

### Cards

- **Corner:** radius 0.
- **Background:** chip, with chip-well for interior wells.
- **Border:** 1.5px hairline-strong frame.
- **Shadow:** none.
- **Padding:** 10 to 16px.
- **Stat card:** white (light) or chip (dark) plate in two rows. The value sits on a plate tinted 12 percent with its own accent, and the label below the hairline divider is a plain 11px uppercase ink stamp, no plate, no shadow. Values print in the text-safe accent of their card (volt ink, teal, purple, leaf), never the raw neon, so every value clears 4.5:1 on its tint in both themes. Accents run yellow, cyan, violet, green by card position, so a KPI row reads as a graded strip rather than four identical boxes.
- **Plan card:** handle, name (16px 800), load dots, and a 44px accent START control. The whole card is a reorderable plate.
- **Session card:** title, mono subtitle with time and duration badges, delete and chevron actions. Hover shifts border to volt.

### Inputs

Outlined style: chip fill, 1.5px hairline-strong border, radius 0, 44px minimum height. Labels are 12px uppercase mono weight 800 sitting on the border line with chip background. Focus thickens the border to 2px and turns it volt; the label follows. Helper text is 11px mono. Inputs floor at 16px font size so iOS Safari never zooms.

### Navigation

Bottom pill on phones, top rail at 769px and up (see Layout). Tabs are 44px-plus rows of icon over 11px uppercase mono labels. The active tab fills volt (#FAFF00 dark, #FFD600 light) with #10111A text; inactive tabs are stone on chip. The rail adds three 8px registration lights (red, cyan, amber) as pure set dressing.

### Tabs (segmented)

Mode switchers (AI Chat/Clipboard, settings sections) are chip-well trays with 4px padding holding 44px flat tabs. The active tab fills volt with black text and weight 900. Sibling buttons (Clear) stretch to the same 52px tray height and share the inactive-tab surface so the header row reads as one machined strip.

### Dialogs and snackbars

Radius 0, hairline-strong border, chip fill, mono titles with a hairline underline. Overlay is rgba(12,13,15,0.56) with a 2px blur. Dialogs center vertically when they fit and pin to the top only once they fill the viewport, with 76px reserved bottom padding so a full-height dialog clears the dialog FABs. Confirm buttons carry the action color (error red for destructive); cancel is the ghost secondary. Snackbars sit bottom-left as hairline plates.

### Tables

Header row on chip-well with 11px uppercase mono labels at +0.12em and a hairline-strong bottom edge. Body cells 14px with hairline row rules. Numeric cells are right-aligned and tabular. The exercise progression table is the canonical example.

### Heatmap (signature)

53-week activity grid of 26px cells with 3px gaps. States are drawn shapes: off (flat well), done (acid green fill), scheduled (dashed stone border), planned (chip with cyan center dot), missed (stone hatch at 20 percent, error-red hatch for the most recent week only), today (volt inset ring), selected (ink inset ring). Day labels are 11px uppercase mono. The legend uses drawn key glyphs, and the grid is a roving-tabindex `role="grid"`: one tab stop, arrow keys walk cells, future cells are skipped, focus lands on today.

### Rest timer (signature)

The page's hero. Digits render in Departure Mono at clamp(4.8rem, 20vw, 6.8rem), weight 900, line-height 0.88, each digit in a fixed 1ch slot. Color walks volt to magenta to red as time runs out (in light the volt step prints as #6E6400 and the final red is #BE123C), with a 220ms color transition and a 1.2s urgent pulse under 10 seconds. A 6px edge track fills via composited scaleX. Controls are 44px secondary buttons: +30s, reset, skip. On completion the panel flips to a green-tinted plate with a 70px "TIME'S UP!" stamp.

### Workout logging (signature)

The steppers pre-fill last session's numbers for each set; the app never re-displays those numbers as text, so nothing on the panel says the same thing twice. The active panel carries no set label: the counter pill on the exercise header (3/6) and the edge track already say where you are, and warm-up sets keep their amber chip. The Discard and End controls live on a sticky machined strip at the scroll bottom, so ending a session never requires scrolling past the last exercise.

### Session summary (signature)

Finishing a workout opens a plate, not a stock dialog: volt trophy box beside "WORKOUT COMPLETE!" and the plan name, three stat plates in the home-strip anatomy (duration, volume, sets), personal records as drawn volt star marks with exercise and record text, and one volt BACK TO HOME action. Sessions without records say so in a quiet dashed stamp. The session state stays alive behind the plate until it is dismissed, so the dialog never hangs over an empty shell.

### Chrome clearance

A paper fade (96px, solid gradient to the page ground, no blur) sits under the fixed nav pill and FABs so scrolling rows exit cleanly; `prefers-contrast: more` removes it.

### Button and overlay plates

Every button is a machined plate: filled and outlined MudBlazor buttons and all icon buttons carry the 1px inset bevel plus a hard sm offset shadow, and press physics translate them 2px into the shadow. Icon buttons without an explicit surface get a chip-well plate with a hairline-strong border, so the shadow never floats on air. Text buttons stay flat; they are links, not plates. Dialogs cast the lg plate shadow, snackbars and popover menus the md, each with the bevel. Menu lists divide their items with 1px hairlines. `prefers-contrast: more` strips every one of these shadows and returns separation to borders alone.

### Title ladder

One ladder, everywhere: page titles 22px 900 uppercase; section and panel titles on plates 18px 800 uppercase; card and accordion titles 16px 800 uppercase with 0.02em tracking; subtitles and micro-labels 11px stone uppercase mono. Exercise and plan names stay sentence case at 18px and 16px respectively.

### Share card

A 400px light-mode plate rendered off-screen (fixed at -9999px, opacity 0) and captured to PNG by html2canvas at scale 2. It uses explicit hex colors only, because html2canvas cannot resolve `color-mix()`. Brand header, plan name, date, three stat wells, per-exercise set lists with warm-up highlights, and a "Tracked with Physiquinator" footer.

## Do's and Don'ts

### Do:

- **Do** set Departure Mono sizes in 11px increments (11, 22, 33) for pixel-perfect rendering; snap sizes to the ladder whenever you touch a surface.
- **Do** keep every number tabular (`tnum 1, lnum 1`) and every timer digit in a fixed 1ch slot.
- **Do** pair every status color with a drawn shape and a text label.
- **Do** keep touch targets at 44px minimum and FABs always visible.
- **Do** use uppercase wide-tracked mono for anything under 12px.
- **Do** keep fonts self-hosted under `wwwroot/fonts`; the app must render offline.
- **Do** render dates short and unambiguous ("8/22" this year, "8/22/25" otherwise, clock time alone for today) and round volumes to whole units.
- **Do** snap chart Y maxima to 1, 2, 2.5, 3, 4, and 5-decade steps so ticks land on round values.

### Don't:

- **Don't** introduce blurred shadows, glows, or backdrop blur for depth or decoration. Depth is hard offset plate shadows, hairlines, bevels, and surface steps (see Elevation and Depth). Everything stays sharp.
- **Don't** round any corner; radius stays 0.
- **Don't** add a second type family, italics, or non-tabular numerals.
- **Don't** rely on hue alone for state, and don't use font-dependent Unicode glyphs for status; draw them.
- **Don't** put two volt elements in one viewport region.
- **Don't** use `color-mix()` inside the share card; html2canvas cannot parse it. Explicit hex only there.
- **Don't** add entrance animations or scattered hovers. One authored moment per surface (the flap tick), 120ms to 220ms, and `prefers-reduced-motion` collapses everything to opacity 150ms.
- **Don't** use border-left accents, gradient text, glass blur, section numbers, or kicker/eyebrow labels. They were tried and removed.
