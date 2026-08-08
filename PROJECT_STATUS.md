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

Test suite: 48 unit + 88 integration = **136 green** (`dotnet test` from `school-erp/`).
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
- A5 Plan enforcement — DONE. Phase A complete.
  - Module gate: `[RequiresModule(TenantModules.X)]` on Exams, Fees,
    Transport, Driver, Library, Timetable and Hostel controllers, enforced
    by a global `ModuleGateFilter` (403 with a clear message; platform/
    Super Admin requests are never gated). TenantInfo now carries
    EnabledModules + SubscriptionExpiresOn (60s cache — changes apply
    within a minute).
  - Expired subscription: password AND OTP login return 423 with a renewal
    message (OTP *requests* are silently swallowed — no SMS burned); the
    portal login page shows the server's 423 title verbatim.
  - SMS metering: the outbox dispatcher atomically spends one credit per
    SMS (`UPDATE … WHERE sms_credits > 0` — concurrency-safe); at 0 the
    message is dead-lettered immediately with "No SMS credits remaining"
    (logged warning). Platform messages (no tenant) are unmetered.
  - DevSeeder backfills demo entitlements at startup (all shipped modules
    + 10k credits) so local demos never trip the gates; integration-test
    tenants seed SmsCredits explicitly.
  - 5 integration tests (metering + dead-letter, expiry lockout for both
    login paths, gate blocks/allows/skips-platform); all E2E-verified live
    (403 message, 423 message, restore).
- B1 Year-end promotion — DONE. PromoteClassCommand closes a section's
  Active enrollments as Promoted and creates Active ones in the target
  year/class/section; per-student opt-out list; idempotent (students
  already in the target year are skipped); roll numbers don't carry.
  POST academics/promotions (students.manage). Portal: "Year-end
  promotion" panel on Academics (from/to pickers with ToStringFunc so a
  preset year renders its name, student checkbox list defaulting to all,
  result snackbar). 1 integration test (promote/opt-out/idempotent/
  opt-out-lifted); E2E-verified in the browser ("Promoted 1 students" —
  demo data then reverted via SQL so Ananya stays in Grade 5 A).
  NOTE: portal student list for the opt-out picker caps at 100 (the
  GetStudents pageSize limit) — fine for real section sizes.
- B2 Teacher logins — DONE. Teacher.UserId (AddTeacherLogin migration,
  column-only). IUserAdminService.CreateTeacherLoginAsync get-or-creates
  the tenant "Teacher" role (students.view, attendance.view/mark,
  exams.view/enter-marks, homework.view/manage, timetable.view — an
  editable role, not system), creates the account from the teacher's own
  contact details + temp password, links it; one login per teacher (409).
  POST teachers/{id}/login (users.manage). Portal: key icon on rows
  without a login → temp-password form → "Has login" chip. 1 integration
  test (role seeded w/ bundle, user linked, real password sign-in, 409 on
  second). E2E-verified: Anita Rao (EMP-001) login created in the portal,
  signed in via +919888811122/Anita@2026Pass — JWT carries exactly the
  classroom bundle; students.view 200, students.manage 403.
- B3 Leave management — DONE. LeaveRequest entity (RLS'd
  AddLeaveRequests migration; leave.view/leave.manage permissions,
  claims auto-backfilled). Parent app "Leave" card: history with
  status + a request form (YYYY-MM-DD inputs, en/te localized), via
  POST/GET parent/children/{id}/leave-requests (EnsureChildAsync
  guard). Staff: GET/POST leave/mine (any signed-in staff, self-only)
  and portal "Leave" page (pending inbox filter, approve/reject,
  "My leave" form). Approving a STUDENT request upserts every day in
  the range as AttendanceStatus.Leave ("Approved leave" remark) on the
  current-year enrollment; staff approvals touch nothing. Overlapping
  non-rejected requests 409; double decisions 409; max 31 days.
  Attendance % now excludes Leave days from the denominator (excused).
  3 integration tests + E2E: parent filed 12–13 Aug for Ananya,
  admin approved in portal, app shows Approved + Leave days at 100%.
- B4 School documents — DONE. IStudentDocumentRenderer +
  QuestPdfStudentDocumentRenderer: Transfer Certificate and bonafide
  (A4, school header w/ affiliation, ref no, formal body with
  gender-aware pronouns, signature blocks) and a CR80-ratio student ID
  card that embeds the stored photo via IFileStorage (placeholder when
  none). GET students/{id}/documents/{transfer-certificate|
  bonafide-certificate|id-card} (students.view). Portal profile has
  three download buttons (bytes → window.schoolErpDownload data-URI
  helper added to index.html). Fixtures now register IConfiguration
  (LocalDiskFileStorage resolves it). 1 integration test (all 3 types
  render real PDFs; unknown student 404); E2E-verified live — all
  three generated for Ananya (TC 29.8KB, bonafide 29.4KB, ID card
  25.2KB with photo) and the portal button downloads.
- B5 Fee polish — DONE. (1) Late fines: FeeHead.LateFineType
  (None/Flat/Percent) + LateFineValue; overdue lines accrue the fine
  once (percent rounds to the rupee); summary carries per-line
  LateFine + TotalLateFine; head-creation form has fine fields, chips
  show "(+₹200 late)". (2) Concessions: FeeConcession entity (RLS'd,
  optional per-head, flat INR); Grant/RevokeConcession commands +
  endpoints (fees.configure); profile ledger shows/grants/revokes;
  Balance = max(0, due − concessions − paid). (3) Receipt PDFs:
  IReceiptRenderer QuestPDF A5-landscape receipt (amount, mode/ref,
  balance-after); GET fees/payments/{id}/receipt; download icons per
  payment row. (4) FeeReminderJob nightly at 03:00 UTC via Hangfire:
  per-tenant scoped (RLS-safe), computes overdue balances, queues
  guardian SMS via outbox (credit metering applies), ≤1 reminder per
  guardian+child per 6 days (jsonb payload → client-side dedupe).
  Migration AddFeePolish. 1 big integration test (fines, concession
  math, receipt PDF, reminder queue + dedupe); portal E2E-verified.
  GOTCHA: outbox payload is jsonb — string Contains doesn't translate;
  fetch narrow window and filter client-side.
  GOTCHA: Blazor dev-server deep links can be HTTP-cached stale after
  index.html changes — cache-bust with a query string when verifying.
- B6 Push notifications — DONE. PushToken entity (RLS'd AddPushTokens
  migration): device Expo token + denormalized owner phone (so guardian
  events queued by phone inside tenant scope fan out without touching
  the identity store). Outbox "push" type + PushPayload; OutboxProcessor
  dispatches via IPushSender (pushes are NOT SMS-credit metered).
  DevPushSender logs; ExpoPushSender (exp.host, no creds) activates on
  Push:Provider=expo and throws on rejected tickets so the outbox
  retries. All four guardian events (absence, results published,
  payment received, bus board/drop) now queue SMS + one push per
  registered device through NotificationQueue.QueueGuardianAsync.
  POST/DELETE api/v1/push/tokens (any signed-in user; upsert by token,
  re-points on user change). Parent app registers after sign-in
  (expo-notifications + expo-device installed; best-effort, no-op on
  web/simulator). Integration test (absence → sms+push rows →
  dispatcher delivers to RecordingPushSender); E2E-verified live:
  parent OTP login → token registered → absence marked → API logged
  "[DEV PUSH] to ExponentPushToken[priya-phone-1]: Absence noted…".
- B7 Term report cards — DONE. TermReport + TermReportComponent
  (weighted exams, weights must sum to 100) + TermStudentInput
  (co-scholastic JSON + remarks), all RLS'd (AddTermReports migration).
  GetTermReportCardPdfQuery reuses the per-exam result pipeline per
  component: per-subject percent per exam, weighted total renormalized
  over components that carry the subject, same A1–E grade bands;
  ITermReportRenderer QuestPDF A4 card (per-component columns,
  weighted %, co-scholastic table, remarks, signatures). Staff API
  under exams/term-reports (view/manage/enter-marks split); parent
  endpoints require EVERY component exam published (and the family
  guard 404s non-guardians — verified live). Portal Exams page grew a
  "Term reports" panel (create with weights, per-student remarks +
  "Area:Grade" co-scholastic entry, PDF download). Integration test
  covers weights validation, draft-block for parents, publish-unblock,
  render. E2E live: Annual Report 2026-27 for Ananya (49KB PDF with
  remarks + Art/Sports/Music grades), parent PDF 200 after publish.

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
- Teacher: `DEMO01` + `+919888811122` / `Anita@2026Pass` (Anita Rao EMP-001,
  "Teacher" role — classroom permissions only).

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
