# SchoolErp — Architecture

A multi-tenant School Management SaaS for Indian schools. One deployment
serves many schools; each school's data is isolated by two independent
mechanisms that are both always on.

## System shape

```
┌─────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  Admin portal   │  │   Parent app     │  │   Driver app     │
│  Blazor WASM    │  │ React Native/Expo│  │ React Native/Expo│
│  (MudBlazor PWA)│  │                  │  │                  │
└────────┬────────┘  └────────┬─────────┘  └────────┬─────────┘
         │        HTTPS + JWT (permission claims)   │
         └──────────────┬─────────────┬─────────────┘
                        ▼             ▼
                ┌───────────────────────────┐
                │       SchoolErp.Api       │  ASP.NET Core 8
                │  MediatR CQRS pipeline    │  (validation → audit)
                │  Hangfire job server      │  outbox dispatch
                └─────┬───────────┬─────────┘
                      ▼           ▼
              ┌──────────────┐  ┌─────────┐
              │ PostgreSQL 16│  │  Redis  │
              │  + RLS       │  │ (cache) │
              └──────────────┘  └─────────┘
```

## Layering (Clean Architecture)

| Project | Depends on | Contains |
|---|---|---|
| `SchoolErp.Domain` | nothing | Entities, enums, base classes (`AuditableEntity`, `TenantEntity`) |
| `SchoolErp.Shared` | nothing | Permission catalog, well-known constants |
| `SchoolErp.Application` | Domain, Shared | CQRS commands/queries (MediatR), FluentValidation validators, abstractions (`IApplicationDbContext`, `ITenantContext`, `ICurrentUser`, `IClientContext`, `IPaymentGateway`, `ISmsSender`, `IAuthService`) |
| `SchoolErp.Infrastructure` | Application | EF Core + Npgsql, ASP.NET Identity, JWT issuance, RLS session interceptor, outbox processor, payment gateways, dev seeder |
| `SchoolErp.Api` | Infrastructure | Controllers, middleware (tenant resolution), authorization policies, Hangfire hosting, Serilog |

The MediatR pipeline runs, in order: **validation** (FluentValidation, maps
to 400) → **audit** (successful `*Command` requests append an `audit_events`
row) → handler.

## Multi-tenancy: two locks on every door

1. **EF Core global query filters** — every entity extending `TenantEntity`
   is automatically filtered by `TenantId == current tenant` (plus
   soft-delete). Business code cannot forget a `WHERE`; the filter is
   composed into every query.
2. **PostgreSQL Row-Level Security** — every tenant table has an RLS policy
   comparing `tenant_id` to `app_current_tenant_id()`, a function reading
   the `app.tenant_id` session variable. `RlsSessionInterceptor` sets the
   variable when a pooled connection is bound to a scope. The API connects
   as the NON-superuser role `schoolerp_app` — superusers silently bypass
   RLS, so the runtime role choice is load-bearing.

Tenant resolution happens in middleware after authentication: the JWT's
`tenant` claim binds the scope via `ITenantContextSetter`. Platform users
(Super Admin) carry no tenant claim and operate above RLS-protected tables
only through platform-scoped endpoints.

**Documented platform-scoped exceptions** (no RLS; explicit or nullable
`tenant_id` column; reachable only by narrow lookups): `outbox_messages`,
`payment_orders`, `refresh_tokens`, `otp_codes`, `audit_events`. Each
entity's XML docs state why.

## AuthN / AuthZ

- **Password login** (staff) and **SMS OTP login** (parents/drivers; hashed,
  single-use, throttled 3-per-15-min). Empty school code = platform login.
- **JWT access tokens (15 min)** embed roles and *effective permissions* as
  claims, so authorization is database-free: `[HasPermission("x.y")]` +
  a dynamic policy provider. Roles are named permission bundles, editable
  per tenant; endpoints never test role names. SuperAdmin implicitly holds
  every permission.
- **Rotating refresh tokens (7 days)** — SHA-256 hashes at rest, single-use;
  replaying a rotated token revokes the whole family (theft response).
  Each chain carries a device label (from User-Agent) and session start,
  surfaced in "My devices" with per-session revocation.
- **MFA (TOTP)** — optional per user. Password success returns a 5-minute
  challenge JWT (no permissions) that must be exchanged with an
  authenticator or single-use recovery code. Wrong codes count toward the
  5-attempt lockout.
- **Identity-scoped mobile APIs** carry no permission claims:
  `ParentAccess` resolves children through guardian links (foreign
  children return 404, never 403 — no existence leaks) and `DriverAccess`
  resolves the driver's route by user id/phone.

## Side effects: transactional outbox + Hangfire

State changes never call SMS/push providers inline. Handlers append an
`outbox_messages` row **in the same SaveChanges** as the business change;
the Hangfire recurring job `outbox-dispatch` (every 15 s) delivers pending
rows (5 attempts, then dead-lettered by flag). Hangfire stores jobs in the
`hangfire` schema (created via the owner connection; job code still runs on
the restricted app connection so RLS applies). Dev dashboard: `/jobs`.

Producers today: attendance absence SMS, exam result publication, fee
receipts, trip board/drop notifications.

## Payments

`IPaymentGateway` abstracts order creation + webhook verification.
`RazorpayGateway` (Orders API, basic auth, integer paise;
`X-Razorpay-Signature` HMAC-SHA256 verification) activates when
`Razorpay:KeyId` is configured; otherwise `DevPaymentGateway` mints
deterministic orders and verifies the same HMAC scheme locally. Webhook
processing is two-phase: a tenant-less order lookup, then a fresh
tenant-bound scope for the RLS-legal payment insert; replays are idempotent.
Receipts are sequential per school.

## Auditability (DPDP)

`AuditBehavior` records every successful command with user id/name, tenant,
caller IP (`IClientContext`) and UTC instant — never payloads, so PII and
secrets cannot leak into the trail. School admins see only their school's
rows (`audit.view`); the table is append-only.

## Operational notes

- **Migrations** run under the owner role (`schoolerp`); the runtime uses
  `schoolerp_app`. New tenant tables MUST call `EnableTenantRls` in their
  migration.
- **Docker**: `deployment/Dockerfile.api` (multi-stage, non-root, :8080).
  Health: `/health/live`, `/health/ready`.
- **CI**: `.github/workflows/ci.yml` — build + tests (Testcontainers),
  advisory scan gated by `docs/security-notes.md`, mobile type-checks,
  image build.
- **Config**: `Jwt:SigningKey` (≥32 chars) is validated at startup;
  containers fail fast when the database is unreachable.
