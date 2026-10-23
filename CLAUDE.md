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
11. **Cancel** is allowed from Planned/InProgress; nothing was consumed, so it only releases the reservation.
12. Transitions: Planned → InProgress → Done; Planned/InProgress → Cancelled. Anything else is rejected.

### Sales orders
13. Lines must reference `Finished` products. Work orders can be created from an order line.
14. **Ship** (Open → Shipped): one transaction, writes `Issue` movements for each line; fails if any finished good is short.
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

- Code, identifiers, commits and UI text in **English**.
- Errors: RFC 7807 `ProblemDetails`. Business-rule violations → 409/422 with a clear `detail`, validation → 400.
- Paging: `?page=&pageSize=` returning `{ items, page, pageSize, totalCount }`.
- Controllers stay thin; logic lives in Application services or Domain.
- Client: one folder per feature, lazy-loaded routes, signals for state, functional interceptors/guards.
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
Partial completion, purchasing/suppliers, lot/serial tracking, multi-warehouse, routing/capacity, multi-tenant.

## Current status

- [x] Scaffold: .NET 10 layered solution, health checks, Angular 22 + Material shell, Docker Compose
- [x] Phase 1 — Foundation: PostgreSQL, test projects, entities + EF configs + initial migration, furniture workshop seed (no users until Phase 3 auth), GitHub Actions CI, live demo on Render (API + static client) with PostgreSQL on Neon
- [ ] Phase 2 in progress — done: BOM explosion and cycle check, BOM line validation (rule 5), products + BOM API (ProblemDetails errors, no auth until Phase 3); next: stock ledger with locking (rules 1–4), then products/BOM/stock screens
