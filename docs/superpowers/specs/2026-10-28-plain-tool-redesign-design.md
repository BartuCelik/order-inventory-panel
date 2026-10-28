# Plain-tool redesign — design

Date: 2026-10-28 · Status: proposed, waiting for the owner's review

## Why

The owner finds the current glass look "AI-made": the blue→purple gradient app icon, frosted glass over colour
washes, KPI tiles with tinted icon squares and glowing corners, pill buttons, an icon beside every label. The app is
a portfolio piece for Upwork clients; it should read as a serious, hand-made work tool, not a template.

**Success:** a visitor sees a calm, plain tool where colour only means something (late, short, below minimum, done).
Every screen keeps its content and behaviour; only the visual language and the navigation layout change.

## Direction (approved by the owner on a mockup, 2026-10-28)

Direction A, *plain tool* (Linear / Vercel dashboard family), **roomy** spacing, **black accent**.
The approved mockup is the reference for every decision below: dashboard only, light and dark.

| Topic | Now | New |
| --- | --- | --- |
| Navigation | Left rail with grouped icons | **Top bar**: brand, text links in three groups split by hairlines, language, theme, user. Active page: text colour + 2px underline |
| Surfaces | Frosted glass over colour washes | Flat: page `--bg` (#fafafa / #0b0b0c), panels `--surface` (#fff / #131315), 1px `--border`, **no shadows**, no `backdrop-filter`, no background washes |
| Accent | Blue | **Black** (white in dark): primary buttons, links, active nav, running dot, chart "in" bars |
| Meaningful colour | Blue, green, amber, red | Only red (late, below minimum, failed), amber (material short) and green (done) |
| Radii | 20px panels, pill buttons | Panels 10px, controls and buttons 6px, badges 4px |
| KPIs | Five floating tiles with tinted icon squares | One bordered strip split by dividers; a 7px dot before the label; the value is coloured only when it is a problem |
| Chips / flags | Tinted fills | Outlined: coloured text with a matching hairline border, no fill |
| Tables | — | Header row on `--muted`, 16px/24px cell padding, row hover on `--muted` |
| Brand | Gradient app icon | 24px square in `--text` with a white glyph (inverted in dark). Avatar: neutral grey circle, not the orange gradient |
| Type | Inter, large tight titles | Inter kept. Page title 32px/650; panel titles 15px/600; body 14.5px. Tabular figures only on numbers (rule kept) |
| Kept as is | | The greeting ("Good afternoon, Mira"), status shapes (ring, dot, tick, strike), the `.is-short` / `.is-late` leading bars, light/dark/system theme, the language picker with flags, all copy |

## What changes, file by file

1. **`client/src/styles.scss`** — the core of the work.
   - Replace the token set in both themes with the mockup's tokens (`--bg`, `--surface`, `--muted`, `--border`,
     `--border-soft`, `--text`, `--text-2`, `--text-3`, `--accent`, `--on-accent`, `--success`, `--caution`,
     `--caution-line`, `--alarm`, `--alarm-line`). Remove `--glass`, `--highlight`, `--bg-field`, `--shadow*`
     and `--surface-strong` after their last use is gone.
   - Restyle the shared classes in place, keeping their names so templates do not change: `page`, `page-header`,
     `page-title`, `panel*`, `kpi*`, `chip`, `status`, `flag`, `meter`, `data-table`, `empty-state`, `toolbar`.
   - Angular Material overrides (`theme-overrides`): buttons 6px radius, filled button = accent, outlined
     button = `--border`; menus, dialogs and form fields flat with a 1px border.
2. **Shell (`app/layout/shell/*`)** — `mat-sidenav` is replaced by a top bar.
   - Wide screens: one 64px row as in the mockup. The nav groups and labels come from the existing `navGroups`;
     the icons are no longer shown in the nav.
   - Narrow screens (< 900px): the bar becomes two rows, brand + user on top and the nav as a horizontally
     scrollable row below. No drawer, no hamburger.
   - User: avatar + name/role, then the sign-out icon button (as now). On narrow screens the name is hidden.
   - Theme toggle: an icon button; it still cycles light → dark → system through `ThemeService`.
   - Language picker: flag + language code in the bar; the menu keeps flag + full name.
   - The demo note ("demo data from an invented furniture workshop" and the time) moves to a small footer
     under the page content.
   - The content area gets the full width, max 1320px, centred.
3. **Dashboard (`app/features/dashboard/*`)** — KPI tiles become the strip; the glowing corners
   (`radial-gradient`) and the gradient chart bars go; the production queue is shown as a table with the columns
   *Work order, Status, Planned, Material*, as in the mockup. The weekly production chart (Chart.js) keeps reading
   its colours from the tokens.
4. **Login (`features/login/login.ts`)** — the card follows the same language: flat, 1px border, 10px radius, the
   new brand mark, the demo role buttons as outlined 6px buttons instead of tinted pills.
5. **Other screens** — products, stock, work orders, sales orders, customers, dialogs: no layout change; they follow
   from the shared classes. Each is checked visually; leftover glass or pill styles in their own styles are removed.
6. **`CLAUDE.md` → *Design rules*** — rewritten to describe this language, replacing the glass rules. Colour meaning
   and the status-shape rules stay.

## Not in scope

New screens, new features, changed copy or translations, changed layouts inside the content screens, a component
library, new dependencies. Ideas that come up go to *Later*.

## How it is checked

- `ng build` clean (warnings are errors) and the client tests pass.
- Every screen is opened in the running app with Playwright, light and dark, at 1440 px and at 390 px (phone);
  the screenshots are reviewed for leftover glass, gradients, pills and broken alignment.
- Keyboard: the top bar links and buttons are reachable and show a visible focus ring.
- Afterwards: the README screenshots are retaken from the live demo right after a reset, and the README is updated
  where it describes the look.

## Delivery

One branch, small commits (tokens and shared classes → shell → dashboard → login → screen-by-screen fixes →
CLAUDE.md), one PR, because a half-restyled app should not be merged. The README screenshots come in a second PR
after the deploy, so they show the live demo.
