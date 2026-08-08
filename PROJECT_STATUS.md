# SchoolErp — Project Status & Session Handoff

> Purpose: lets any development session (human or AI) resume this build with
> zero conversation history. Keep this file updated at the end of every
> working session.

Last updated: 2026-08-08.

## What this is

Multi-tenant School Management SaaS for Indian schools.
Stack: .NET 8 Clean Architecture + CQRS (MediatR) + EF Core 8 + PostgreSQL 16,
Blazor WASM admin portal (MudBlazor), React Native (Expo + TypeScript) parent
and driver apps.
Monorepo layout is described in `README.md`.

## Completed (all with tests + browser-verified where UI exists)

| Vertical | Backend | API | Portal UI | Parent app |
|---|---|---|---|---|
| Multi-tenancy (EF filters + PostgreSQL RLS) | ✅ | — | — | — |
| Auth: JWT + rotating refresh (reuse-detect), OTP, permission policies, lockout | ✅ | ✅ | ✅ login | ✅ OTP login |
| Tenant catalog/onboarding (Super Admin) | ✅ | ✅ | ✅ | — |
| SIS: years, classes/sections, students, guardians, enrollments | ✅ | ✅ | ✅ | ✅ (children) |
| Attendance + absence SMS via outbox | ✅ | ✅ | ✅ grid | ✅ card |
| Examinations: subjects, papers, marks, publish + SMS, results w/ rank | ✅ | ✅ | ✅ | ✅ card |
| Fees: heads, plans, receipts + SMS, gateway abstraction + HMAC webhook | ✅ | ✅ | ✅ | ✅ card |
| Parent API (family-scoped guard, 404-not-403) | ✅ | ✅ | — | ✅ |
| Notices + Homework (class/section visibility) | ✅ | ✅ | ✅ | ✅ cards |
| Transport: routes/stops/vehicles/assignments | ✅ | ✅ | ✅ | — |
| Trips: inspection-gated start, GPS pings, board/drop SMS, live bus query | ✅ | ✅ | — | ✅ live-bus card |
| Timetable: define→publish, calendar views | ✅ | ✅ | ✅ week grid | ✅ day tabs |
| Teachers: directory CRUD, timetable linkage, clash detection, schedules | ✅ | ✅ | ✅ | ✅ (names resolve) |
| Driver app (Expo): OTP login, manifest, checklist gate, trip loop | — | ✅ | — | ✅ driver-app |
| Audit trail: every command logged (user, tenant, IP), portal viewer | ✅ | ✅ | ✅ | — |
| Device sessions: UA-labelled sign-ins, "My devices" list + revoke | ✅ | ✅ | ✅ | — |
| MFA: TOTP enrollment, login gate, recovery codes, Security page | ✅ | ✅ | ✅ | — |
| Library: catalog, issue/return (availability + 3-loan limit), overdue | ✅ | ✅ | ✅ | ✅ card |
| Hostel: buildings/rooms, capacity-checked stays, warden contact | ✅ | ✅ | ✅ | ✅ card |

Test suite: 48 unit + 74 integration = **122 green** (`dotnet test` from `school-erp/`).
Integration tests use Testcontainers (needs Docker running).

## Remaining scope (in rough priority order)

1. ~~React Native driver app~~ DONE + E2E-verified (OTP login → manifest →
   inspection gate blocks Start trip until all 4 checks → pickup trip with
   GPS ping loop → Board fires guardian SMS via outbox → End trip resets).
   ~~Parent live-bus card~~ DONE + E2E-verified: polls
   /parent/children/{id}/bus every 20s; live state (trip type, started-at,
   last GPS fix + Maps link) and idle state both verified in the browser.
2. ~~Timetable module~~ DONE (bfeef3c): define→publish workflow, portal
   editor, parent schedule card, 4 integration tests, E2E-verified.
   ~~Teachers module~~ DONE: Teacher entity (RLS) + staff.view/staff.manage
   (claims backfilled), CRUD API, TeacherId on timetable entries (free-text
   TeacherName kept for guest slots), define-time clash detection (in-batch
   + cross-class time overlap; inactive teachers rejected), per-teacher
   schedule query, portal Teachers page + timetable teacher picker,
   4 integration tests, E2E-verified (demo teacher: Anita Rao EMP-001).
3. Auth hardening (task list #7): ~~audit-log trail~~ DONE — AuditBehavior
   (MediatR pipeline) appends an audit_events row for every successful
   command (user, tenant, IP via IClientContext; queries never logged;
   table is a documented no-RLS exception with nullable tenant_id filtered
   in the handler), audit.view permission (backfilled), portal "Audit log"
   page, 3 integration tests. NOTE: permission claims live in the JWT —
   after a claims backfill users must sign out/in to see the new page.
   ~~Device registration~~ DONE — refresh tokens carry DeviceName (derived
   server-side from User-Agent) + SessionStartedAt across rotations;
   GET/DELETE /api/v1/auth/sessions (self-service, ownership-checked);
   portal "My devices" page under the account menu; 2 integration tests.
   ~~MFA~~ DONE — TOTP via Identity's authenticator provider (enroll →
   shared key + otpauth URI, enable verifies a code and issues 8 one-time
   recovery codes); password login returns a 5-min MFA challenge JWT
   instead of tokens, completed at /auth/mfa/verify with TOTP or recovery
   code (wrong codes count toward lockout); portal Security page +
   two-step login; disable requires a current code and resets the key.
   ~~Dev-seeder claim backfill~~ DONE — DevSeeder backfills missing
   permission claims onto SchoolAdmin roles at every startup (no more
   manual SQL when adding permission constants; re-login still required).
   Task #7 auth hardening is COMPLETE.
4. ~~Hangfire hosting for jobs~~ DONE — Hangfire 1.8 + PostgreSQL storage
   (schema "hangfire", created via the OWNER connection string because the
   restricted runtime role can't CREATE SCHEMA; jobs still use the normal
   app connection so RLS is intact). Outbox dispatch is the recurring job
   "outbox-dispatch" (*/15s cron); the old BackgroundService is deleted.
   Dev dashboard at :5199/jobs (local-only). Hangfire is LGPL-3.0 — noted
   in docs/security-notes.md (fine for SaaS).
5. ~~Real Razorpay adapter~~ DONE — RazorpayGateway (plain HttpClient, no
   SDK): Orders API with basic auth + integer-paise amounts, webhook
   verification per X-Razorpay-Signature (HMAC-SHA256 hex, fixed-time
   compare), payment.captured/failed parsing. Activates automatically when
   `Razorpay:KeyId` is configured; otherwise the dev HMAC gateway stays.
   4 unit tests over a fake HTTP handler; production keys go in
   appsettings/environment (Razorpay:KeyId/KeySecret/WebhookSecret).
6. ~~CI/CD + Dockerfile~~ DONE — .github/workflows/ci.yml (backend build +
   full test run with Testcontainers on ubuntu, vulnerability scan failing
   on unaccepted High/Critical advisories, mobile type-checks for both
   Expo apps, Docker image build) and deployment/Dockerfile.api
   (multi-stage, non-root, port 8080; verified by a real local build).
   NOTE: no git remote is configured yet — push to GitHub to activate CI.
   ~~Docs pack~~ DONE — docs/architecture.md, docs/api-guide.md,
   docs/user-manual-admin.md, docs/user-manual-apps.md (+ README index).
   ~~Localization (en/te)~~ DONE — both Expo apps have a typed i18n module
   (src/i18n: en source-of-truth keys, Telugu coverage compiler-enforced),
   an EN/తెలుగు toggle (login + home headers) persisted via
   SecureStore/localStorage, and every UI string translated. School-entered
   data (names, subjects) intentionally stays as entered. Portal/SMS remain
   English (SMS localization would need per-guardian language preference —
   future work).
   ~~Report-card PDFs~~ DONE — QuestPDF (Community, see security-notes)
   renderer behind IReportCardRenderer; GetReportCardPdfQuery composes the
   result + student + school header. Staff endpoint
   GET exams/{examId}/results/{studentId}/report-card (drafts allowed for
   proofing) and parent endpoint
   GET parent/children/{id}/exams/{examId}/report-card (published only).
   Integration test covers draft-hiding + valid PDF output; E2E-verified
   with a real 39KB PDF for Ananya's Mid-Term 1.

ALL originally-scoped roadmap items are now complete, including Library and
Hostel modules. Parent app also has a report-card download button on the
result card (web: blob open; device: expo-file-system File + expo-sharing
share sheet; localized en/te). Local infra (task #4) is DONE: real
liveness/readiness split (live = process only; ready = tagged postgres+redis
checks; verified live — killing redis 503s ready while live stays 200) and a
docker-compose "full" profile that builds and runs the API container against
the data services (`docker compose --profile full up -d --build`).
Remaining backlog: portal/SMS localization only. (AutoMapper is REMOVED —
hand-written mappings; the High advisory GHSA-rvv3-g6hj-g44x is resolved.)

## Roadmap v2 (gap closure — see docs/roadmap-v2.md for the full plan)

- A1 Staff & role administration — DONE (Users page: staff accounts +
  role permission editor; tenant-resolved role IDs, never names).
- A2 Production SMS (MSG91) — DONE (Msg91SmsSender activates on
  Sms:Provider=msg91; DLT template config; dev fallback logs).
- A3 Real online payments — DONE (RazorpayGateway activates on
  Razorpay:KeyId; hosted checkout page + dev-simulate flow; parent
  "Pay now" opens checkout, webhook completes, receipt SMS).
- A4 File storage + student photos — DONE. IFileStorage →
  LocalDiskFileStorage (keys `{tenant:N}/{category}/{guid:N}{ext}`,
  regex-validated + path-prefix check, extension allowlist).
  POST students/{id}/photo (students.manage, 2 MB, jpg/png/webp) swaps
  the photo and deletes the orphaned file; GET files/{**key} serves
  anonymously (unguessable double-GUID keys) with 1h response cache.
  Portal profile shows the photo avatar + "Photo" upload button
  (InputFile + label pattern); parent app child switcher shows the
  avatar (falls back to initial). 6 unit tests (roundtrip, traversal,
  bad extension/category). E2E-verified in portal + parent app.
- A5 Plan enforcement — NEXT.

## How to run (Windows dev box)

- `dotnet` is at `C:\Program Files\dotnet\dotnet.exe` (may not be on PATH in
  shells); `dotnet-ef` via `%USERPROFILE%\.dotnet\tools`.
- Local stack: `docker compose -f deployment/docker-compose.yml up -d`
  (PostgreSQL 16 on 5432 + Redis; init script creates the NON-superuser
  runtime role `schoolerp_app` — the API must use it or RLS is silently inert).
- Migrations (run as owner role):
  `dotnet-ef database update --project backend/src/SchoolErp.Infrastructure --startup-project backend/src/SchoolErp.Api --connection "Host=localhost;Port=5432;Database=schoolerp;Username=schoolerp;Password=schoolerp_dev_only"`
- Dev servers are defined in `<workspace>/.claude/launch.json`:
  `api` (http://localhost:5199), `portal` (http://localhost:5050),
  `parent` (Expo web, http://localhost:8081), `driver` (Expo web,
  http://localhost:8082).
- App type-check: `npx tsc --noEmit` in `mobile/parent-app` / `mobile/driver-app`.

## Dev credentials (Development seeder + manual rows)

- Super Admin (platform): empty school code + `superadmin@schoolerp.local` / `ChangeMe@12345`
- School admin: `DEMO01` + `admin@demo.school` / `ChangeMe@12345`
- Parent (OTP): school `DEMO01`, phone `+919876501234` (Priya Reddy — guardian
  of demo student Ananya Reddy, Grade 5 A). OTP code appears in API log as
  `[DEV SMS]`.
- Driver (OTP): school `DEMO01`, phone `+919888877766` (Ramesh Kumar, assigned
  to Route 1 — West; Ananya rides from stop "Jubilee Hills").

## Conventions (follow these when adding modules)

- Every business entity extends `TenantEntity`; every migration creating a
  tenant table MUST call `migrationBuilder.EnableTenantRls("table")` (and
  `DisableTenantRls` in Down). Platform-scoped exceptions (documented in each
  entity): `outbox_messages`, `payment_orders`, `refresh_tokens`, `otp_codes`.
- Module pattern: Domain entity → EF config (`Infrastructure/Persistence/Configurations`)
  → DbSet on `IApplicationDbContext` + `AppDbContext` → CQRS command/query with
  FluentValidation validator → controller with `[HasPermission(...)]` →
  integration tests (copy an existing module fixture) → portal page → parent
  endpoint/card if parent-facing.
- New permissions go in `Shared/Authorization/Permissions.cs`. GOTCHA: seeded
  roles hold permission claims copied at seed time — adding a permission later
  requires a claims backfill for existing roles (bit us with `homework.*`).
- Side effects (SMS/push) go through the transactional outbox
  (`OutboxMessage` + `SmsPayload`), written in the same SaveChanges as the
  business change. Dispatcher: Hangfire recurring job `outbox-dispatch`
  (OutboxDispatchJob → OutboxProcessor) every 15s; dashboard at /jobs (dev).
- Package pins are deliberate (OSS licensing): MediatR 12.x,
  FluentAssertions 6.x. AutoMapper was removed — mappings are hand-written
  in `*Mappings` static classes (EF-translatable `Expression` fields for
  query projection + `ToDto()` extensions for in-memory maps).
- Zero-warning policy: builds must be clean (analyzers on). Test naming with
  underscores is allowed via `backend/tests/Directory.Build.props`.

## Gotchas learned the hard way

- STOP the dev servers (preview `api`/`portal`) before `dotnet build` —
  running processes lock the DLLs (MSB3027).
- PostgreSQL superusers bypass RLS: runtime connection must be `schoolerp_app`.
- Identity tables: PascalCase table names, snake_case columns.
- MVC validation attributes on records must target constructor parameters,
  not `[property: ...]`.
- Razor: never name a loop variable `section` (collides with `@section`).
- EF can't translate ordering by projected record properties — order before
  `.Select`. `jsonb` columns have no LIKE operator.
- Expo web is the fast way to browser-verify the parent/driver apps; API CORS
  must include `http://localhost:8081` and `http://localhost:8082`.
- The portal is a PWA: after rebuilding it, the service worker can serve a
  STALE cached bundle (new routes 404 / odd auth behavior). Fix in the browser:
  unregister service workers + clear CacheStorage, then reload.
- MudBlazor chrome (MudAppBar/MudDrawer/MudMainContent) must be DIRECT
  children of MudLayout — nesting them inside AuthorizeView breaks drawer
  offsets/toggling (was the cause of the old 88px padding hack). Login uses
  EmptyLayout; MainLayout renders chrome unconditionally and wraps @Body in
  an ErrorBoundary that auto-recovers on navigation.
- NEVER put a padding class (pa-*) directly on MudMainContent — it overrides
  the component's own padding-top (the fixed-appbar offset), shifting content
  under the app bar and misaligning drawer click targets (clicking one menu
  item hit another). Pad an inner div instead.
- The PWA service worker now activates new versions immediately
  (skipWaiting + clients.claim + one-time reload on controllerchange) so
  users stop getting stuck on stale bundles.
- API root (:5199) redirects to /swagger in Development (JSON service info
  otherwise) — it's an API host, there is no UI there.
- In Blazor login flows, never hard-navigate right after submitting — it can
  kill the async token write to localStorage; let the SPA navigate itself.
