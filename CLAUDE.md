# CLAUDE.md — Order & Inventory Panel

Guidance for AI assistants and contributors working in this repository. Read this before writing code.

## What this is

A portfolio project (Upwork/Fiverr showcase): **order, inventory and production tracking for small manufacturers**
(10–50 people, currently running on Excel — furniture, textile, metal parts workshops).
Not a generic stock panel. The differentiator is the **bill of materials (BOM)** and the
**work-order-based material availability / shortage calculation**.

Pitch: *"I turned enterprise ERP experience into something a small workshop can actually use."*

Hard rule: **everything is written from scratch.** No code, data, naming or business rules copied
from any employer. Seed data uses an invented **furniture workshop** (deliberately not textile).

## Stack (decided — do not reopen)

| Area      | Choice |
| --------- | ------ |
| API       | .NET 10 (LTS) Web API, controllers, EF Core 10 |
| Database  | **PostgreSQL** (Npgsql provider) in Docker Compose |
| Client    | Angular 22, standalone components, signals, **Angular Material** (already installed — no PrimeNG) |
| Charts    | Chart.js (Phase 4) |
| Auth      | JWT bearer; roles `Admin`, `Planner`, `Warehouse` |
| Tests     | xUnit; Testcontainers (PostgreSQL) for integration tests |
| Excel     | ClosedXML |
| PDF       | QuestPDF |
| CI        | GitHub Actions |

Do **not** add: MediatR, AutoMapper, generic repository over EF Core, CQRS, microservices.
The layering below is enough for this size.

## Repository layout

```
api/
  src/OrderInventory.Domain          Entities, enums, pure domain logic (no dependencies)
  src/OrderInventory.Application     Use-case services, DTOs, IAppDbContext abstraction → Domain
  src/OrderInventory.Infrastructure  AppDbContext, EF configurations, migrations, auth, Excel/PDF → Application
  src/OrderInventory.Api             Controllers, auth setup, composition root
  tests/OrderInventory.UnitTests         Pure logic (BOM explosion, availability, status rules)
  tests/OrderInventory.IntegrationTests  Real PostgreSQL via Testcontainers (transactions, concurrency)
client/src/app/
  core/       API, auth, interceptors, guards
  layout/     Shell
  features/   products, stock, orders, work-orders, dashboard
```

Application may reference EF Core through `IAppDbContext` (pragmatic choice; avoids a repository layer).

## Domain model

```
Product         Id, Code (unique), Name, Type, Unit, MinStock, IsActive
                Type: RawMaterial | SemiFinished | Finished
                Unit: Piece | Meter | SquareMeter | Kilogram | Liter
BomLine         Id, ParentProductId, ComponentProductId, QtyPerUnit
StockMovement   Id, ProductId, Quantity (signed), Type, RefType?, RefId?, Note?, CreatedBy, CreatedAt
                Type: Receipt(+) | Issue(-) | Adjustment(±) | ProductionConsumption(-) | ProductionOutput(+)
Customer        Id, Name, ContactName?, Email?, Phone?
SalesOrder      Id, Number, CustomerId, OrderDate, DueDate, Status, ShippedAt?
                Status: Open | Shipped | Cancelled
SalesOrderLine  Id, SalesOrderId, ProductId (Finished only), Quantity
WorkOrder       Id, Number, ProductId, Quantity, SalesOrderLineId?, Status, PlannedDate, CompletedAt?
                Status: Planned | InProgress | Done | Cancelled
WorkOrderComponent  Id, WorkOrderId, ComponentProductId, RequiredQty, ConsumedQty
User            Id, Email, DisplayName, PasswordHash, Role
```

All quantities are `decimal` stored as `numeric(18,4)`. Timestamps are UTC (`timestamptz`).

## Business rules (the core — keep them exact)

### Stock
1. **Stock is never stored.** On-hand = `SUM(StockMovement.Quantity)` per product.
2. The ledger is **append-only**. No update/delete of movements; mistakes are fixed with an `Adjustment`.
   Enforced twice: `AppDbContext` rejects tracked changes, and a database trigger
   (`trg_stock_movements_append_only`) rejects any UPDATE/DELETE. TRUNCATE still works (Phase 5 nightly reset).
3. **Stock can never go negative.** Any operation that would make on-hand < 0 is rejected as a whole.
4. Every stock-changing operation runs in **one database transaction** and locks the affected
   `Product` rows with `SELECT ... FOR UPDATE`, **ordered by ProductId** (prevents deadlocks).
   The check ("is there enough?") and the write happen inside that lock. Covered by an integration test
   that fires two concurrent operations at the same product.

### BOM
5. `RawMaterial` products cannot have BOM lines. `QtyPerUnit > 0`. No duplicate (Parent, Component).
6. **No cycles**, including indirect ones (A→B→A). Checked when a BOM line is added.
7. BOM is multi-level. `BomExplosion` (pure function) returns total raw-material need for
   *N units* of a product by recursing through SemiFinished components. Shown on the product screen.

### Work orders
8. On creation, the product's **direct** BOM lines are **snapshotted** into `WorkOrderComponent`
   (`RequiredQty = QtyPerUnit × Quantity`). Later BOM edits do not affect existing work orders.
   SemiFinished components are consumed from stock; producing them is a separate work order.
9. **Availability (with reservation):**
   `Reserved(p)  = Σ (RequiredQty − ConsumedQty)` over open work orders (Planned/InProgress), excluding the one being evaluated
   `Available(p) = OnHand(p) − Reserved(p)`
   `Shortage(p)  = max(0, RequiredQty − Available(p))`
   Without reservation, two work orders would both report the same material as available — that is the bug this prevents.
10. **Complete** (InProgress → Done): one transaction, rows locked per rule 4; for every component writes
    `ProductionConsumption`, then one `ProductionOutput` for the product; sets `ConsumedQty`, `CompletedAt`.
    Fails entirely if any component is short. **No partial completion** (out of scope).
    "Short" here means **physical on-hand** (the ledger's rule 3 check), not rule 9's reserved shortage:
    with reservations, two work orders can block each other forever (10 on hand, A needs 5, B needs 8:
    neither is "available") although A could be made. Reserved shortage is a planning signal only.
11. **Cancel** is allowed from Planned/InProgress; nothing was consumed, so it only releases the reservation.
12. Transitions: Planned → InProgress → Done; Planned/InProgress → Cancelled. Anything else is rejected.

### Sales orders
13. Lines must reference `Finished` products. Work orders can be created from an order line.
14. **Ship** (Open → Shipped): one transaction, writes `Issue` movements for each line; fails if any finished good is short.
14a. **Cancel** (Open → Cancelled) also cancels the order's open work orders (Planned/InProgress) in the same
    transaction, releasing their reservations. Done work orders stay Done. Locks: sales order row, then its
    work order rows in id order.
15. **Late** = `Status == Open && DueDate < today` (derived, not stored).

### Roles
| Action | Admin | Planner | Warehouse |
| --- | --- | --- | --- |
| Products / BOM edit | ✓ | ✓ | read |
| Stock receipt / issue / adjustment | ✓ | – | ✓ |
| Customers, sales orders | ✓ | ✓ | read |
| Work orders: create / start / cancel | ✓ | ✓ | read |
| Work orders: complete | ✓ | – | ✓ |
| Ship sales order | ✓ | – | ✓ |
| Users | ✓ | – | – |

`CreatedBy` on movements comes from the JWT, never from the request body.

## Conventions

- Code, identifiers and commits in **English**. UI text is English by default and translated (see *Translations*).
- Errors: RFC 7807 `ProblemDetails`. Business-rule violations → 409/422 with a clear `detail`, validation → 400.
- Paging: `?page=&pageSize=` returning `{ items, page, pageSize, totalCount }`.
- Controllers stay thin; logic lives in Application services or Domain.
- Client: one folder per feature, lazy-loaded routes, signals for state, functional interceptors/guards.
- Client styling: use the shared classes and tokens in `client/src/styles.scss` (`page`, `page-header`, `grid`,
  `panel`, `kpi`, `chip`, `status`, `flag`, `data-table`, `meter`, `empty-state`, `toolbar`) before writing component styles.

### Translations (decided 2026-10-25)

- Languages: English (default), Turkish, German, French, Spanish, Italian (Russian removed by the owner 2026-10-28).
  Own small module, no library:
  `client/src/app/core/i18n/` — `t('key', params)` in TypeScript, `{{ 'key' | t: params }}` in templates.
- **Every UI string is a key.** Add it to `messages/en.ts` first, then to every other `messages/*.ts`; they are typed
  against English, so a missing or misspelt key fails the build. `{name}` placeholders; plurals are objects keyed by
  `Intl.PluralRules` category (`one`/`other`), chosen by `params.count`.
- The language is fixed per page load (`main.ts` loads it before the app; switching reloads). Labels built at module
  load (status lists, nav) are therefore safe. Dates and numbers follow `LOCALE_ID` = the chosen language.
- Not translated: API error `detail` texts (English from the server) and data (product, customer names).
- The language picker shows a flag per language: SVG files in `client/public/flags/` (not emoji — Windows renders
  flag emoji as two letters). English uses the UK flag (owner decision 2026-10-28).

### Design rules (plain tool, decided 2026-10-28 — replaces the glass look; keep consistent)

Reference: `docs/superpowers/specs/2026-10-28-plain-tool-mockup.html` (the dashboard the owner approved).

- **Plain and flat:** panels, tiles and the top bar are `--surface` on a `--bg` page with a 1px `--border`.
  **No shadows** (overlays — menus, dialogs, dropdowns — are the one exception), no `backdrop-filter`, no gradients,
  no background washes. Radii: panels 10px (`--radius-lg`), tiles/alerts/overlays 8px, controls and buttons 6px,
  badges/chips/flags 4px. Roomy spacing: page padding 48/40px, panel padding 24px, table rows 60px.
- **Navigation is a top bar:** brand, the pages in three groups split by hairlines, language (flag + code), theme,
  user. Below 1440px the user's name is hidden, below 1100px the bar becomes two rows and the page links scroll on
  their own. The page itself never scrolls sideways (checked at 390px).
- **Themes:** every colour is a token defined twice in `styles.scss` (light, dark). Dark follows the system unless
  `ThemeService` sets `data-theme` on `<html>` (toggle in the top bar). Components use tokens only — never hex
  values. Angular Material reads the same tokens (`theme-type: color-scheme` + `theme-overrides`).
- **Colour has meaning, nothing else is coloured:** the accent is **black** (white in dark, `--accent` /
  `--on-accent`) = primary actions, links, the current page; green (`--success`) = done; amber (`--caution`) =
  material short (rule 9); red (`--alarm`) = late, below minimum, failed. Chips and flags are **outlined**
  (`*-ink` text, `*-line` border), never filled. Links show themselves by underlining on hover, since the accent
  equals the text colour.
- Status is shape **and** text: `.status.planned` ring, `.in-progress` dot, `.done` green tick,
  `.cancelled` struck through. Abnormal rows get `.is-short` / `.is-late` (a bar on the leading edge).
- Focus: every interactive element shows a 2px `--accent` outline on keyboard focus (Material buttons too).
- Type: Inter (variable) self-hosted via @fontsource-variable; titles 32px/650 (`.page-title`), panel titles
  15px/600. Tabular figures only on numbers (`.num`, table cells, readouts) — on body they widen hyphens in codes.
  Sentence case; no all-caps labels, no monospace. No icon tiles; icons only inside buttons and empty states.
- Copy says what happens ("Ship", "Plan production"); errors state what is wrong and what to do.
- `TreatWarningsAsErrors` is on — keep the build clean.
- Commits: Conventional Commits (`feat:`, `fix:`, `test:`, `chore:` …).

## Working method

- **Tests first** for stock, BOM and work-order logic: write the failing test, then the implementation.
- Rules 3, 4, 9, 10, 14 must each have tests. Concurrency (rule 4) is an integration test.
- For auth, authorization and stock transactions, **explain the reasoning** in the PR/commit or a short comment —
  the owner reviews these line by line.
- **Scope is frozen.** New ideas go to the *Later* list below, not into the current phase.
- Small, reviewable steps; one concern per commit.

## Roadmap

- **Phase 1 — Foundation:** SQL Server → PostgreSQL; test projects; entities + EF configs + initial migration;
  realistic seed (furniture workshop); GitHub Actions (build + test API, build client); **live deploy of the skeleton**.
- **Phase 2 — Products, BOM, stock:** API + screens; BOM explosion and cycle check (tests first); stock ledger with locking.
- **Phase 3 — Orders and work orders:** sales orders, work orders from order lines, availability/shortage with reservation,
  complete/cancel/ship; JWT auth + roles. → *Minimum demo-able product. Open the Upwork profile here.*
- **Phase 4 — Dashboard and outputs:** late orders, critical stock (available < MinStock), weekly production chart;
  PDF work order; Excel export, then Excel import (products, opening stock) last.
- **Phase 5 — Showcase:** one-click demo login per role, nightly seed reset, README with screenshots, 1–2 min demo video.

### Later (not now)
Partial completion, purchasing/suppliers, lot/serial tracking, multi-warehouse, routing/capacity, multi-tenant,
dashboard export to Excel (numbers only; ClosedXML cannot draw charts), translated PDFs (the PDF is English only),
Users screen for Admin (owner decision 2026-10-28: demo users come from the seed; editing them on a public demo
would break it until the nightly reset).

## Current status

- [x] Scaffold: .NET 10 layered solution, health checks, Angular 22 + Material shell, Docker Compose
- [x] Phase 1 — Foundation: PostgreSQL, test projects, entities + EF configs + initial migration, furniture workshop seed (no users until Phase 3 auth), GitHub Actions CI, live demo on Render (API + static client) with PostgreSQL on Neon
- [x] Phase 2 — Products, BOM, stock: BOM explosion and cycle check, BOM line validation (rule 5), products + BOM API (ProblemDetails errors), stock ledger with row locking (rules 2–4, concurrency tests), screens for products, BOM editing and stock
- [x] Design refresh and a first dashboard (requested ahead of Phase 4, merged in #10): shared design system,
      dark sidebar shell, restyled screens; `GET /dashboard` with product counts, below-minimum (on-hand, until
      reservations exist), latest movements and 14-day activity, shown as KPI tiles and a CSS chart.
- [x] Phase 3 — API and screens merged in #12, auth in #14 (live on Render):
  - [x] Work order transitions (rule 12) and availability with reservation (rule 9) — pure domain + unit tests
  - [x] Customers API; sales orders API: create (rule 13), late flag (rule 15), ship (rule 14), cancel
  - [x] Work orders API (rules 8–12): BOM snapshot, availability per component, start/cancel/complete with
        row locks, complete through `StockLedger` on physical on-hand; concurrency test for double complete
  - [x] Owner decisions 2026-10-25: cancelling a sales order cancels its open work orders (rule 14a);
        append-only ledger enforced by a database trigger (rule 2)
  - [x] Dashboard: order and production figures (late orders, short work orders, queue)
  - [x] Screens: shop-floor dashboard, work orders (list, detail), customers, sales orders (list, new, detail with
        plan production / ship / cancel); first built in an industrial HMI style, replaced by the glass look in #16
  - [x] JWT auth + roles: `POST /auth/login` (Identity `PasswordHasher`, same 401 for unknown email and wrong
        password), HS256 token for 8 h, fallback policy = signed in, `[Authorize(Roles = Roles.Planning|Warehouse)]`
        per the roles table, `CreatedBy` = email claim; demo users seeded even into an already seeded database
        (password `workshop-demo`, public by design); signing key from config (Render `generateValue`)
  - [x] Client: login page (demo accounts listed), bearer interceptor (signs out on 401), guard, user in the rail,
        actions hidden by role (`AuthService.canPlan` / `canHandleStock`)
  - Users screen (Admin) moved to *Later* (owner decision 2026-10-28)
- [x] Redesign requested by the owner (#16): Apple-like glass look, light/dark themes with a toggle, Inter,
      restyled shell, dashboard tiles and login (see *Design rules*)
- [x] Translations requested by the owner (#17): seven languages (six since Russian was removed 2026-10-28), picker in the rail and on the login page
      (see *Translations*)

- [x] Phase 4 — Dashboard and outputs (finished 2026-10-27):
  - [x] Critical stock on *available* (on-hand minus open work order reservations) on the dashboard (#19)
  - [x] Weekly production chart: completed work orders per UTC week, last 8 weeks, Chart.js bars coloured
        from the theme tokens (#20)
  - [x] PDF work order: `GET /work-orders/{id}/pdf` (QuestPDF, Community licence), *Download PDF* on the
        detail page (#21)
  - [x] Product list and detail flag "below minimum" on available, like the dashboard; the list shows both
        on-hand and available (#24)
  - [x] Excel export (ClosedXML): `GET .../export` on products, stock movements, sales orders and work orders
        with the list's own query (paging ignored, at most 10,000 rows, else 400); *Export to Excel* on each
        list screen, every role; English headers like the PDF (#25)
  - [x] Excel import (#27): `POST /products/import` (Planning) and `POST /stock/opening-stock` (Warehouse),
        `?dryRun=true` for the preview; one shared dialog (choose file → preview or errors by row → Import).
        Owner decisions 2026-10-27:
        - Products use the export's columns; a new code creates, an existing code updates name, minimum and
          active flag (type and unit cannot change).
        - Opening stock is one "Opening stock" Adjustment per product, **only for products with an empty
          ledger** (a second run is rejected, never doubled); the empty-ledger check runs inside the rule 4
          row lock (`StockLedger.LockAsync`). Quantity 0 is skipped.
        - All or nothing: every problem is returned as a 400 keyed "Row 7"/"File"; nothing is written.
        - Limits: 5 MB, 10,000 rows. Server messages are English, like other API errors.

- [x] Phase 5 — Showcase (order chosen by the owner 2026-10-28, finished 2026-10-29):
  - [x] One-click demo login (#29): the role buttons on the login page fill in the demo account and sign in
  - [x] Nightly seed reset (#30, live and verified 2026-10-28): `POST /demo/reset` (only with
        `Database:SeedDemoData` and `Demo:ResetKey`, else 404; the key in `X-Demo-Reset-Key` is the credential,
        not a JWT, because demo passwords are public) truncates everything except users and reseeds in one
        transaction; `.github/workflows/demo-reset.yml` calls it at 03:00 UTC (06:00 Turkey) and can be run by hand
        (a timer in the API would not fire while the free host sleeps). `Demo__ResetKey` on Render and the GitHub
        secret `DEMO_RESET_KEY` hold the same value.
  - [x] README rewritten for visitors, with screenshots of the live demo in `docs/screenshots/` (#31; taken right
        after a reset with Playwright driving the local Edge; retake them when a screen changes)
  - [x] Fix: separator on the product's Available tile (#32)
  - [x] Language picker shows SVG flags (`client/public/flags/`, UK flag for English); Russian removed (#33)
  - [x] **Plain-tool redesign** (owner request 2026-10-28: the glass look read as "AI-made"): plain tool, roomy
        spacing, **black accent**, navigation in a **top bar**, the greeting kept (see *Design rules*). Spec and plan
        in `docs/superpowers/`; built on `feat/plain-tool-redesign`, one PR.
  - [x] README screenshots retaken from the live demo after the redesign (#36)
  - [x] 90-second demo video at the top of the README: recorded on the final UI with Playwright, English voice-over
        (edge-tts, `en-US-AndrewNeural`, owner's choice) and captions burned in, identical to the spoken text. Story:
        late order → shortage after reservations → BOM → complete → outputs → themes and languages. The video is
        uploaded to GitHub (user-attachments), not committed; the recording and narration scripts are kept outside
        the repo. Re-record it when a screen in the story changes.

### Next session

The roadmap is complete. New ideas go to *Later* and are started only when the owner asks.

The owner wants a mockup before any large visual change (the redesign started with a static HTML mockup).

Local dev note: the local database holds leftover test products (`UI-TEST-…`, `UIV2-…`, `UI-IMP…`) and their
opening stock from manual checks; `docker compose down -v` resets it to the seed.
