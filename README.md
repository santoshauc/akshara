# SchoolErp — Multi-Tenant School Management SaaS (India)

Enterprise school-management platform: .NET 8 Clean Architecture backend on
PostgreSQL 16, Blazor WebAssembly admin portal, and React Native (TypeScript)
Parent and Driver apps.

## Repository layout

```
school-erp/
├── backend/
│   ├── src/
│   │   ├── SchoolErp.Domain/          # Entities, value objects, domain events (no dependencies)
│   │   ├── SchoolErp.Application/     # CQRS handlers, validators, abstractions
│   │   ├── SchoolErp.Infrastructure/  # EF Core + PostgreSQL, tenancy, integrations
│   │   ├── SchoolErp.Api/             # ASP.NET Core Web API (composition root)
│   │   └── SchoolErp.Shared/          # Shared kernel (results, pagination, constants)
│   └── tests/
│       ├── SchoolErp.UnitTests/
│       └── SchoolErp.IntegrationTests/  # Testcontainers-based, incl. tenant isolation
├── admin-portal/SchoolErp.AdminPortal/  # Blazor WASM PWA (MudBlazor)
├── mobile/parent-app/                   # React Native — parents (iOS/Android)
├── mobile/driver-app/                   # React Native — drivers (iOS/Android)
├── deployment/docker-compose.yml        # Local PostgreSQL 16 + Redis
├── docs/                                # Architecture & design documentation
└── scripts/
```

## Multi-tenancy model

Single PostgreSQL database, shared schema. Every business table carries
`tenant_id` plus audit columns. Isolation is enforced in **two independent
layers**:

1. **EF Core global query filters** — composed automatically in
   `AppDbContext` for every entity deriving from `TenantEntity`; inserts are
   stamped (and updates guarded) by `AuditableEntityInterceptor`.
2. **PostgreSQL row-level security** — `RlsSessionInterceptor` binds the
   session variable `app.tenant_id` on every connection; per-table policies
   (installed via `RlsMigrationExtensions.EnableTenantRls`) compare it with
   `app_current_tenant_id()`. Policies use `FORCE`, so even the owning role
   cannot bypass them.

Tenant resolution order (middleware `TenantResolutionMiddleware`):
JWT `tenant` claim → `X-Tenant-Id` header → custom domain → subdomain.

## Prerequisites

- .NET 8 SDK
- Docker Desktop (PostgreSQL 16 + Redis via compose; Testcontainers for tests)
- Node.js 20+ (mobile apps)

## Getting started

```bash
# 1. Start local infrastructure
docker compose -f deployment/docker-compose.yml up -d

# 2. Apply database migrations
dotnet ef database update --project backend/src/SchoolErp.Infrastructure --startup-project backend/src/SchoolErp.Api

# 3. Run the API (Swagger at /swagger in Development)
dotnet run --project backend/src/SchoolErp.Api

# 4. Run tests
dotnet test
```

## Security notes

- Dependency advisories are tracked in `docs/security-notes.md`.
- Local compose credentials are development-only; production secrets are
  injected via environment/secret manager and never committed.

## Documentation

| Document | Audience |
|---|---|
| [docs/architecture.md](docs/architecture.md) | Engineers — system design, tenancy, auth, jobs, payments |
| [docs/api-guide.md](docs/api-guide.md) | Integrators — auth flows, conventions, endpoint catalog |
| [docs/user-manual-admin.md](docs/user-manual-admin.md) | School administrators — portal walkthrough |
| [docs/user-manual-apps.md](docs/user-manual-apps.md) | Parents & drivers — mobile app guides |
| [docs/security-notes.md](docs/security-notes.md) | Engineers — accepted advisories, licensing notes |
| [docs/observability.md](docs/observability.md) | Operators — OTLP setup, dashboards, alerts |
