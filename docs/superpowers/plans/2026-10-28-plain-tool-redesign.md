# Plain-tool redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the glass look with the approved plain-tool look (roomy, black accent, top-bar navigation) on every screen, without changing content or behaviour.

**Architecture:** The look lives in `client/src/styles.scss` (tokens + shared classes + Material overrides); most screens follow from it. Four places have their own styling that must be rebuilt: the shell (rail → top bar), the dashboard, the login page, and a handful of feature `.scss` files that use glass tokens or pill radii. Token *names* stay where their meaning stays, so templates and most component styles need no edits.

**Tech Stack:** Angular 22 (standalone, signals), Angular Material (M3 `mat.theme` + `theme-overrides`), SCSS, Chart.js, Vitest (`ng test`), Playwright (checks only, from a scratch folder, never a project dependency).

**Spec:** `docs/superpowers/specs/2026-10-28-plain-tool-redesign-design.md`
**Visual reference:** `docs/superpowers/specs/2026-10-28-plain-tool-mockup.html` (open it in a browser; it is the source of every value below).

## Global Constraints

- No new dependencies. No changed copy: every label reuses an existing i18n key (`messages/en.ts`); if a key is truly missing, add it to all six `messages/*.ts` files.
- Colours only through tokens; no hex values in components (the brand mark too: `--text` square, `--bg` glyph).
- `TreatWarningsAsErrors`-equivalent for the client: `npx ng build` must print no warnings or errors.
- Accent is black in light (`#18181b`) and near-white in dark (`#f4f4f5`); `--on-accent` is `#ffffff` in light and `#18181b` in dark.
- Radii: panels `10px`, controls/buttons `6px`, badges/flags `4px`. No `999px` pills except round dots and the avatar.
- No `backdrop-filter`, no `box-shadow` on panels/tiles, no gradients except the half-filled `.chip.semi` dot and the struck-through `.status.cancelled` ring.
- Keep: status shapes, `.is-short`/`.is-late` leading bars, the greeting, light/dark/system theme cycle, the flag language picker, tabular figures only on numbers.
- One branch `feat/plain-tool-redesign`, Conventional Commits, one PR at the end.

## Review Focus

1. **Phone width (390 px):** the page must never scroll sideways — the top bar's nav row scrolls inside itself. Checked by `screens.mjs` (`scrollWidth > innerWidth` fails) in Tasks 2 and 6.
2. **Long German labels at 1024 px:** nav links must stay on one line and the bar at 64 px. Checked in Task 2 (`de` run of `screens.mjs`, bar height assertion).
3. **Dark theme with a near-white accent:** filled buttons must show dark text (`--on-accent`), focus rings must stay visible. Checked in Task 1 on the product list (light + dark screenshots) and the keyboard step in Task 6.
4. **Material overlays not covered by shared classes** (select panel, autocomplete, menu, dialog, datepicker, snackbar, paginator): no glass, no 20px corners. Checked in Task 5 by opening one of each.
5. **Role-limited views:** a Warehouse or Planner user sees fewer buttons; headers must not collapse oddly. Checked in Task 6 by running `screens.mjs` as Planner.

---

## Checking tool (used by every task)

Create once, outside the repo, in a scratch folder (e.g. `%TEMP%/redesign-check`): `npm init -y`, `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1 npm i playwright@1.56.1`, then `screens.mjs`. It drives the local Edge (`channel: 'msedge'`) against `http://localhost:4200` (API `dotnet run --project api/src/OrderInventory.Api`, client `npm start` in `client/`, Docker up).

`screens.mjs <outDir> [lang=en] [role=Admin] [routes=all]`:
- Signs in through the login page's `Sign in as <role>` button (set `localStorage['workshop.language']` first for `lang`).
- Looks up ids through `/api/products|work-orders|sales-orders?pageSize=100` with the token from `localStorage['workshop.session']`: product `FG-DT-160`, work order `WO-00005`, sales order `SO-00001`.
- Routes: `/login` (before sign-in), `/dashboard`, `/work-orders`, `/work-orders/{id}`, `/sales-orders`, `/sales-orders/new`, `/sales-orders/{id}`, `/customers`, `/products`, `/products/{id}`, `/stock`.
- For each route × `light|dark` (`colorScheme`) × width `1440|1024|390` (height 900, `deviceScaleFactor` 1): wait for `networkidle` + 800 ms, save `<route>-<theme>-<width>.png`, and print `FAIL <file>: <reason>` when `document.documentElement.scrollWidth > innerWidth` or (from Task 2 on, signed-in routes) `header.topbar` is missing or, at ≥ 1024 px, taller than 64 px.
- Exit code 1 if anything failed.

Every "check" step below means: run it, read the printed FAIL lines (must be none), and look at the named screenshots.

---

### Task 1: Tokens, shared classes and Material overrides

**Files:**
- Modify: `client/src/styles.scss` (whole token block, `body`, `.mat-mdc-button-base`, `.page*`, `.panel*`, `.kpi*`, `.chip`, `.status`, `.flag`, `.meter`, `.data-table`, `.toolbar`, overlays block, header comment)

**Interfaces:**
- Produces (token names every later task uses): `--bg`, `--surface`, `--surface-muted` (= the mockup's `--muted`), `--border`, `--border-soft`, `--text`, `--text-2`, `--text-3`, `--accent`, `--accent-hover`, `--accent-soft`, `--on-accent`, `--success`, `--success-soft`, `--caution`, `--caution-ink`, `--caution-soft`, `--caution-line`, `--alarm`, `--alarm-ink`, `--alarm-soft`, `--alarm-line`, `--radius-lg: 10px`, `--radius: 8px`, `--radius-sm: 6px`, `--font`, `--ease`.
- Kept for now, removed in Task 6: `--surface-strong` (set equal to `--surface`), `--glass` (set to `none`), `--highlight` (`transparent`), `--shadow` and `--shadow-lift` (`none`), `--bg-field` (`none`). Neutralising instead of deleting keeps not-yet-migrated components from breaking mid-branch.

- [ ] **Step 1: Baseline.** Run `screens.mjs baseline` on `main` styles (before editing) to have before-pictures.
- [ ] **Step 2: Tokens.** Light/dark values from the mockup's `:root` / `[data-theme="dark"]` blocks with the black accent (`:root[data-accent="black"]` values): light `--bg #fafafa`, `--surface #fff`, `--surface-muted #f4f4f5`, `--border #e4e4e7`, `--border-soft #efeff1`, `--text #18181b`, `--text-2 #52525b`, `--text-3 #8b8b93`, `--accent #18181b`, `--accent-hover #3f3f46`, `--accent-soft #f4f4f5`, `--success #1f8a4c`, `--caution #b45d00` (= `--caution-ink`), `--caution-line #f2c48d`, `--alarm #c8302a` (= `--alarm-ink`), `--alarm-line #f0b3ae`; dark `--bg #0b0b0c`, `--surface #131315`, `--surface-muted #1b1b1e`, `--border #27272a`, `--border-soft #1f1f22`, `--text #f4f4f5`, `--text-2 #a1a1aa`, `--text-3 #71717a`, `--accent #f4f4f5`, `--accent-hover #d4d4d8`, `--accent-soft #27272a`, `--on-accent #18181b`, `--success #4cc27e`, `--caution #e9a24f`, `--caution-line #5a3d17`, `--alarm #f0726b`, `--alarm-line #5c2421`. `*-soft` tokens become `transparent` (fills are gone; outlines carry the colour).
- [ ] **Step 3: Base and Material.** `body`: `background: var(--bg)`, font-size `14.5px`, no fixed background. `theme-overrides`: `surface*` → `var(--surface)`, `surface-container-highest` → `var(--surface-muted)`, corners `extra-small 4px, small 6px, medium 8px, large 10px, extra-large 12px, full 6px` (so Material buttons are 6px, not pills). Focus ring: `outline: 2px solid var(--accent); outline-offset: 2px`.
- [ ] **Step 4: Shared classes.** `.page` max-width `1320px`, padding `48px 40px 64px` (phone `24px 16px 40px`), no entrance animation; `.page-title` `32px/650`, letter-spacing `-0.022em`; `.page-subtitle` `15px`; `.panel` flat (`background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-lg)`), delete the `glass` mixin; `.panel-header` padding `18px 24px` with a bottom `--border-soft` line, `.panel-title` `15px/600`; `.panel-body` `24px`; `.kpi-icon` hidden (`display: none`) — Task 3 replaces it on the dashboard and other screens fall back to label-only; `.kpi-value` `34px/600`; `.chip` and `.flag`: no fill, `1px solid` border (`--border` for plain chips, `--caution-line` / `--alarm-line` for warning/danger/short/late), radius `4px`, text in `--caution-ink` / `--alarm-ink`; `.status.in-progress::before` without the halo; `.meter` height `4px`, radius `2px`, fill `--text` (low → `--alarm`, short → `--caution`); `.data-table` header row `background: var(--surface-muted)`, cell padding `16px 24px` (first/last), row hover `--surface-muted`; overlays: no `backdrop-filter`, `box-shadow: 0 8px 24px rgb(0 0 0 / 12%)` allowed on overlays only (menus/dialogs need separation), radius `8px`; `.cdk-overlay-dark-backdrop` without blur. Rewrite the file's header comment to describe the plain look.
- [ ] **Step 5: Check.** `npx ng build` clean; `npx ng test --watch=false` passes; `screens.mjs t1` — look at `products-light-1440`, `products-dark-1440`, `work-orders-{id}-light-1440`: flat panels, outlined chips, black (dark: white) filled buttons with readable text.
- [ ] **Step 6: Commit** `feat(client): plain-tool tokens and shared styles`.

### Task 2: Shell — top bar instead of the rail

**Files:**
- Modify: `client/src/app/layout/shell/shell.html`, `shell.scss` (rewrite), `shell.ts`
- Modify: `client/src/app/shared/language-picker/language-picker.ts`
- Create: `client/src/app/layout/shell/shell.spec.ts`

**Interfaces:**
- Consumes: Task 1 tokens.
- Produces: `<header class="topbar">` (used by `screens.mjs`), `LanguagePicker` input `compact = input(false)`; when true the button shows flag + upper-case code (`EN`) and no chevron, the menu is unchanged.

- [ ] **Step 1: Failing test** `shell.spec.ts` (Vitest via `ng test`; `await loadLanguage()` in `beforeEach`, providers `provideRouter([])`, `provideHttpClient()`, `provideHttpClientTesting()`):
  - `it('shows the six pages as links in the top bar')`: `fixture.nativeElement.querySelectorAll('header.topbar nav a')` has length 6 with `getAttribute('href')` equal to `['/dashboard','/work-orders','/sales-orders','/customers','/products','/stock']`.
  - `it('has no side drawer')`: `querySelector('mat-sidenav')` is `null`.
  - `it('shows the demo note in the footer')`: `querySelector('footer.demo-note')?.textContent` contains `t('shell.demoNote')`.
- [ ] **Step 2: Run** `npx ng test --watch=false` → the three new tests FAIL (no `header.topbar`).
- [ ] **Step 3: Implement.** `shell.ts`: drop `MatSidenavModule`, `BreakpointObserver`, `compact`, `toSignal`, `map`; drop `icon` from `NavItem`. `shell.html`: `header.topbar > .topbar-inner` with brand link (`.mark` 24px square in `--text` holding the `carpenter` mat-icon in `--bg`, 16px, then `app.name`), `nav[aria-label]` rendering each group's items with `routerLinkActive="active"` + `ariaCurrentWhenActive="page"` and a `span.nav-sep` (`aria-hidden`) between groups (group labels become `visually-hidden` headings for screen readers via the existing `aria-labelledby` ids), then `.top-right`: `<app-language-picker compact />`, theme icon button (`mat-icon-button`, existing `themeIcon()` and `theme.change` aria-label), user (neutral avatar, name + role, sign-out icon button). After `<router-outlet />`: `<footer class="demo-note">` with the existing time + `shell.demoNote` text. `shell.scss`: values from the mockup (`.topbar` sticky, 64px, `--surface`, bottom `--border`; inner max-width 1320px, padding `0 40px`, gap 36px; nav links `--text-2`, active `--text` + 2px underline in `--text`; footer centred `--text-3` 12.5px, padding 24px). Below 900px: `.topbar-inner` wraps into two rows (brand + right side, then `nav` full width with `overflow-x: auto`, no scrollbar), padding `0 16px`, user name hidden.
- [ ] **Step 4: Run** tests → PASS. `npx ng build` clean.
- [ ] **Step 5: Check** `screens.mjs t2` and `screens.mjs t2-de de`: no FAIL lines (horizontal scroll at 390, bar height at ≥1024); look at `dashboard-light-1440`, `dashboard-dark-390`, `products-light-1024` (de). Tab from the address bar: every nav link, the language button, the theme button and sign-out get a visible focus ring.
- [ ] **Step 6: Commit** `feat(client): top bar navigation`.

### Task 3: Dashboard

**Files:**
- Modify: `client/src/app/features/dashboard/dashboard.html`, `dashboard.scss`
- Modify (only if colours read differently): `client/src/app/features/dashboard/production-chart/production-chart.ts`

**Interfaces:**
- Consumes: Task 1 tokens; existing i18n keys `workOrders.workOrder`, `common.status`, `workOrders.planned`, `workOrder.materials` for the queue's column headers.

- [ ] **Step 1: KPI strip.** `.tiles` becomes one bordered block (`--surface`, 1px `--border`, `--radius-lg`), five equal columns split by `--border-soft` (2 columns below 900px, 1 below 480px). Each tile keeps its link and data; `.tile-icon` is replaced by a 7px dot before the label (`.alarm` → `--alarm`, `.caution` → `--caution`, `.running` → `--accent`, `.done` → `--success`, otherwise `--text-3`); the reading is coloured only for `.alarm` / `.caution`. Remove the `radial-gradient` glow and all tile shadows/lift.
- [ ] **Step 2: Queue as a table.** Add a header row (`<li class="queue-head" aria-hidden="true">` with the four keys above) and lay rows out as a 4-column grid: *work order* (product × qty, number · customer), *status* (status shape + label), *planned* (`planLabel`), *material* (right-aligned `Short` flag). Keep `.is-short` leading bar. Header row on `--surface-muted`, 12.5px `--text-3`.
- [ ] **Step 3: Charts and lists.** Stock movement bars: flat `--accent` for in, `--border` (dark: `--text-3`) for out, radius `2px 2px 0 0`, no gradient; `Orders due` / `Below minimum` rows padding `16px 24px`; panel header links as plain text links (`--accent`, underline on hover) instead of `mat-button`. Production chart keeps `token('--accent')`; confirm it reads black/white.
- [ ] **Step 4: Check** `npx ng build`; `screens.mjs t3 en Admin dashboard`: compare `dashboard-light-1440` with the mockup side by side — same structure, spacing within a few px; `dashboard-dark-1440`, `dashboard-light-390` without FAIL.
- [ ] **Step 5: Commit** `feat(client): plain dashboard`.

### Task 4: Login page

**Files:**
- Modify: `client/src/app/features/login/login.ts` (styles block, brand markup)

- [ ] **Step 1: Restyle.** Card: `--surface`, 1px `--border`, `--radius-lg`, no shadow/animation/glass; brand: the same `.mark` square as the top bar (40px, icon 22px) instead of the gradient icon; title `26px/650`; role buttons: outlined 6px buttons (`1px solid var(--border)`, `--surface`, `--text`, hover `--surface-muted`), icon in `--text-2`; disabled `opacity: 0.6`. Page background `--bg`.
- [ ] **Step 2: Check** `screens.mjs t4 en Admin login` light/dark/390; one-click sign-in still works (the script itself signs in through these buttons).
- [ ] **Step 3: Commit** `feat(client): plain login page`.

### Task 5: Screen sweep and Material overlays

**Files (only the glass/pill/gradient leftovers found by grep):**
- Modify: `features/orders/sales-order-detail/sales-order-detail.scss`, `features/orders/sales-order-new/sales-order-new.scss`, `features/products/product-detail/product-detail.scss`, `features/products/product-list/product-list.scss`, `features/work-orders/work-order-detail/work-order-detail.scss`, `features/stock/movement-table/movement-table.ts`, `shared/import-dialog/import-dialog.scss`, `shared/language-picker/language-picker.ts`

- [ ] **Step 1: Grep** `grep -rn "radius-lg\|var(--radius)\|radius-sm\|--glass\|--shadow\|--highlight\|surface-strong\|999px\|gradient\|-soft)" client/src/app` and fix each hit to the Task 1 rules (pills → `--radius-sm`, tinted fills → outline with the matching `*-line`, glass/shadow → removed). Leave round dots/avatars at `50%`.
- [ ] **Step 2: Overlays.** In the running app open: a product select / autocomplete (sales order new), a menu (language), a dialog (Record movement on a product), the date picker (sales order new due date), a snackbar (start a work order), the paginator (stock ledger). Each: flat, 8px corners, 1px border, no blur. Fix in `styles.scss` overlay block if not.
- [ ] **Step 3: Check** `screens.mjs t5` all routes, light + dark, three widths: no FAIL; look at every `-1440` picture and `work-orders-{id}`, `sales-orders-new`, `products-{id}` at 390.
- [ ] **Step 4: Commit** `fix(client): remove glass leftovers on feature screens`.

### Task 6: Clean-up, design rules, final check

**Files:**
- Modify: `client/src/styles.scss` (delete `--surface-strong`, `--glass`, `--highlight`, `--shadow`, `--shadow-lift`, `--bg-field` once grep shows no use)
- Modify: `CLAUDE.md` (*Design rules* section rewritten; *Current status*: redesign item ticked with the PR number left for the owner)

- [ ] **Step 1:** `grep -rn "surface-strong\|--glass\|--highlight\|--shadow\|--bg-field" client/src` → only the definitions; delete them; build clean.
- [ ] **Step 2: Design rules.** Replace the glass bullets with: plain tool, flat surfaces with 1px borders and no shadows (overlays excepted), roomy spacing, radii 10/6/4, black accent (white in dark) for primary actions, links and the current page; colour only for meaning (red late/below minimum/failed, amber short, green done), outlined chips/flags, top-bar navigation, status shapes and leading bars unchanged, Inter with tabular figures only on numbers. Point to the mockup file.
- [ ] **Step 3: Final check.** `npx ng build`; `npx ng test --watch=false`; `screens.mjs final` (Admin, en), `screens.mjs final-planner en Planner`, `screens.mjs final-tr tr`: no FAIL; keyboard pass on the dashboard (Tab through top bar and tiles, focus visible in light and dark).
- [ ] **Step 4: Commit** `docs: design rules for the plain-tool look`, push, open the PR with before/after screenshots of the dashboard (baseline vs final) described in the body.
