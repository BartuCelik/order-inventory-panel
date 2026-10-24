# Order & Inventory Panel

An admin panel for small and medium-sized businesses to manage orders and track inventory. It pairs an Angular front end with a .NET 10 Web API built on a layered (Clean Architecture) design, backed by PostgreSQL.

> **Live demo:** https://order-inventory-client.onrender.com (free hosting; the first load after a quiet period can take up to a minute)
>
> **Status:** Foundation and live deploy are done; BOM explosion and cycle check are implemented and tested. Product, stock, order and work-order screens are in progress.

## Tech stack

| Layer     | Technology                                                       |
| --------- | ---------------------------------------------------------------- |
| Front end | Angular 22 (standalone components, signals), Angular Material 3 |
| API       | .NET 10 Web API, Entity Framework Core 10, OpenAPI               |
| Database  | PostgreSQL 18 (Docker), Npgsql EF Core provider                  |

## Architecture

```
order-inventory-panel/
├── api/                             .NET 10 solution (OrderInventory.slnx)
│   ├── src/
│   │   ├── OrderInventory.Domain          Entities and domain rules (no dependencies)
│   │   ├── OrderInventory.Application     Use cases and abstractions      → Domain
│   │   ├── OrderInventory.Infrastructure  EF Core, DbContext, external services → Application
│   │   └── OrderInventory.Api             HTTP layer and composition root → Application, Infrastructure
│   └── tests/
│       ├── OrderInventory.UnitTests         Pure domain/application logic
│       └── OrderInventory.IntegrationTests  Real PostgreSQL via Testcontainers
├── client/                          Angular application
│   └── src/app/
│       ├── core/                          App-wide services (API, health)
│       ├── layout/                        App shell (toolbar, sidenav)
│       └── features/                      Feature areas (dashboard, ...)
└── docker-compose.yml               PostgreSQL
```

Dependencies point inward. The Domain layer depends on nothing, and infrastructure concerns (EF Core, PostgreSQL) are confined to the Infrastructure layer.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) and npm
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (on Windows, WSL 2 and hardware virtualization must be enabled)

## Getting started

**1. Database**

```bash
cp .env.example .env          # change the credentials if you like
docker compose up -d
```

**2. API** → http://localhost:5080

```bash
cd api
dotnet run --project src/OrderInventory.Api
```

In the Development environment the API creates the database on startup if needed and applies any pending EF Core migrations.

**3. Client** → http://localhost:4200

```bash
cd client
npm install
npm start
```

During development, requests from the client to `/api/*` and `/health` are forwarded unchanged to the API by `proxy.conf.json`.

**4. Tests**

```bash
cd api
dotnet test
```

Integration tests start a throwaway PostgreSQL container through Testcontainers, so Docker must be running.

**5. Database migrations**

`dotnet-ef` is pinned as a local tool in `api/dotnet-tools.json`. After changing an entity or its configuration:

```bash
cd api
dotnet tool restore
dotnet ef migrations add <Name> -p src/OrderInventory.Infrastructure -s src/OrderInventory.Api -o Persistence/Migrations
```

A test fails if the model has changes that no migration covers.

> If you change the credentials in `.env`, update the connection string in `api/src/OrderInventory.Api/appsettings.Development.json` as well, or override it with user secrets:
> `dotnet user-secrets set "ConnectionStrings:Default" "<connection-string>" --project src/OrderInventory.Api`

## Endpoints

| Endpoint               | Description                                                  |
| ---------------------- | ------------------------------------------------------------ |
| `GET /health`          | Status of the app and its database connection (503 if unhealthy) |
| `GET /health/live`     | Liveness only; external dependencies are not checked         |
| `GET /openapi/v1.json` | OpenAPI document (Development only)                          |
| `GET /api/products`    | Paged products with on-hand stock; `?page=&pageSize=&type=&search=` |
| `GET /api/products/{id}` · `POST /api/products` · `PUT /api/products/{id}` | Read, create, update a product |
| `GET /api/products/{id}/bom` · `POST /api/products/{id}/bom` · `DELETE /api/products/{id}/bom/{lineId}` | Direct BOM lines of a product |
| `GET /api/products/{id}/bom/explosion?qty=N` | Total leaf-component need for N units (multi-level) |
| `POST /api/stock/movements` | Receipt / Issue (positive quantity) or Adjustment (signed); rejected if stock would go negative |
| `GET /api/stock/movements?productId=` | Stock ledger, newest first, paged; append-only (no update or delete) |

Errors are RFC 7807 ProblemDetails: 400 validation, 404 not found, 409 conflict (e.g. duplicate code), 422 business rule.

## Ports

| Service    | Port |
| ---------- | ---- |
| Client     | 4200 |
| API        | 5080 |
| PostgreSQL | 5432 |

## Roadmap

- [ ] Product and category management
- [ ] Stock movements (receiving / dispatch / stock count)
- [ ] Order management and status workflow
- [ ] Low-stock alerts
- [ ] Authentication and authorization
- [ ] CI
