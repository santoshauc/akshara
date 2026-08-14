# SchoolErp — Project Status & Session Handoff

> Purpose: lets any development session (human or AI) resume this build with
> zero conversation history. Keep this file updated at the end of every
> working session.

Last updated: 2026-08-09.

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

Remote: https://github.com/santoshauc/akshara (public) as of 11 Aug 2026 —
`origin`. The previous remote is kept as `oldorigin`
(github.com/vivian-richard/akshara) and its history diverges: every commit was
re-authored to `santoshauc <santoshauce@gmail.com>` before the first push here,
so the SHAs differ. Do not force-push between the two. Safety refs for that
rewrite: branch `backup-pre-santoshauc-rewrite` and tag `pre-santoshauc-rewrite`.

**CI RUNS NOW, and is green** (all five jobs: backend, vulnscan, mobile ×2,
docker). That closes Phase 0 item 1 of docs/roadmap-production.md — "273 green"
is no longer a claim about one machine. Push access is currently borrowed: the
local credential authenticates as `vivian-richard`, who is a collaborator here,
so pushes depend on that invite standing.

Test suite: 125 unit + 243 integration = **368 green** (`dotnet test` from `school-erp/`).

## Email — the fourth notification channel (feature/email-channel)

Closes Phase 1 item 1 of docs/roadmap-production.md. The outbox carried SMS and
push; a school ERP that cannot email is missing a channel.

- `IEmailSender` in Application/Abstractions, `DevEmailSender` (logs) and
  `SmtpEmailSender` in Infrastructure. Config-activated on `Email:Provider=smtp`
  exactly like MSG91, Meta and Expo — until then nothing leaves the machine.
- `OutboxMessageTypes.Email` + `EmailPayload(To, Subject, Body, Template)`.
  `Template` is the marker jobs match on, for the same reason SmsPayload has one:
  matching on rendered prose breaks the moment a reader switches to Telugu.
- `NotificationQueue` queues ONE email per guardian who has an address, rendered
  from the SAME `NotificationStrings` call as the SMS — subject from the
  template's `.title`, body from its `.body`. Channels cannot drift because
  rendering happens once, at queue time.
- Guardians with no address get no row. Most guardians here are reachable by
  phone only; queueing regardless would fill the outbox with rows that can only
  dead-letter.
- NOT SMS-credit metered, like push: an email costs the school's own mail
  provider, not a credit this platform sells.
- 3 integration tests on the existing localization fixture (no 32nd container):
  a Telugu guardian gets a Telugu subject AND body, the email text equals the SMS
  text, and a guardian without an address gets nothing.
- GOTCHA: `SmtpClient` is obsolete-flagged in favour of MailKit, but that
  guidance is about OAuth and protocol coverage. This sends plain text over
  authenticated submission to one relay, which SmtpClient does correctly without
  a new dependency. Revisit only if a provider needs XOAUTH2.
- STILL OPEN: attachments. Emailing a receipt or report-card PDF needs the
  payload to reference a document the dispatcher renders at send time. The
  renderers exist; the plumbing does not.
Integration tests use Testcontainers (needs Docker running).
Coverage is measurable again: `scripts/coverage.ps1` reports **90.3%** of the
backend. See "Test coverage" below — the old 7.2% was a broken measurement.

## Read this first if the goal is a shipped product

`docs/roadmap-production.md` is the production plan. The short version: the
architecture is production-grade, the OPERATIONS are a proof of concept —
never deployed, CI has never run, and `appsettings.json` carries working
credentials. Those gate everything below and are not on this feature list.

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
   data (names, subjects) intentionally stays as entered. (SMS/WhatsApp/push
   are localized too as of feature/notification-localization — see below.)
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
- B8 Parent–teacher messaging — DONE. Phase B COMPLETE. StudentMessage
  entity (thread = the student; RLS'd AddStudentMessages migration)
  with per-side read stamps (ReadByParentAt/ReadByStaffAt) and sender
  name snapshots. Reading a thread as one side marks the OTHER side's
  messages read. Staff API: messages/threads (unread-first inbox),
  GET/POST messages/students/{id} (communication.view/send). Parent:
  GET/POST parent/children/{id}/messages via the family guard;
  GetUnreadForParentQuery for badges. Bodies are audit-safe (commands
  audited without payloads). Portal "Messages" page (thread list with
  unread badges, chat bubbles w/ Seen state, reply box). Parent app
  MessagesCard (chat bubbles, composer, en/te). Integration test:
  full loop with unread counts both ways + whitespace-body rejection.
  E2E live: parent sent from the app → portal badge "1" → staff read +
  replied → app shows the reply from "Demo School Admin".
- C1 Portal dashboard — DONE. GetDashboardQuery (tenant-scoped):
  active students (current-year Active enrollments), attendance today
  (marked/present/% — Late+HalfDay count as attended), fees collected
  this calendar month, overdue loans, pending leave, unread parent
  messages, next 3 upcoming exams. GET /api/v1/dashboard
  (students.view; portal shows a platform-account fallback alert).
  Home.razor replaced its placeholder tiles with clickable real tiles
  + "Needs attention" chips. Integration test proves tenant scoping
  (School A admissions never move School B tiles). E2E-verified live
  (100% attendance tile, ₹30,000 fees month-to-date).
- C2 Period-wise attendance — DONE. AttendanceRecord.Period (null =
  the daily roll call) with two partial unique indexes (daily-unique
  filtered "period IS NULL"; period-unique filtered NOT NULL) —
  migration AddPeriodAttendance. MarkAttendanceCommand/queries take an
  optional Period; per-period absences NEVER queue guardian SMS; the
  month calendar, leave marking and dashboard only read daily rows.
  Portal attendance page grew a "Roll call" select fed by the
  PUBLISHED timetable for that weekday (class-wide slots merged with
  section-specific; section wins per period; Sunday maps to day 7).
  Integration test: daily+period rows coexist independently, period
  grid reads its own view, no SMS, calendar ignores period rows.
  E2E live: P1·Mathematics picked from the Monday timetable, marked
  Present, row persisted with period=1 and no daily side effects.
- C3 DPDP workflows — DONE. ExportStudentDataQuery: one JSON document
  (profile, guardians, enrollments, attendance, payments, concessions,
  loans, leave, messages) via GET students/{id}/data-export
  (students.manage). EraseStudentDataCommand (reason mandatory):
  deletes the photo file, anonymizes the student in place
  (Erased Student, PII nulled, Withdrawn, soft-deleted), anonymizes
  guardians with no other children (phone → "erased-…" placeholder,
  push tokens removed), redacts message bodies and leave reasons;
  the command lands on the audit trail automatically. Portal profile:
  "DPDP export" download + guarded "Erase data" panel with reason.
  FIXED REGRESSION: admission-number generation and duplicate checks
  now IgnoreQueryFilters + explicit tenant scope — soft-deleted
  (erased) students keep their numbers reserved; without this the next
  admission reused an erased number and hit the unique index.
  Integration test covers export content, anonymization, orphan
  guardian scrubbing, audit row; E2E live (export 200 → erase 204 →
  fetch 404 → DB shows Erased/soft-deleted + EraseStudentDataCommand
  audit row).
- C4 Timetable substitutions — DONE. TimetableSubstitution entity
  (date-specific overlay; base timetable untouched; RLS migration
  AddTimetableSubstitutions; unique per date+slot).
  GetSubstitutionPlanQuery: the absent teacher's published slots that
  weekday, each listing active teachers with NO overlapping class and
  NO overlapping cover that date. ApplySubstitutionsCommand upserts
  per slot (self-cover 409, inactive subs rejected);
  GetSubstitutionsQuery lists a date's covers with names. Endpoints
  under timetable/substitutions (manage for plan/apply, view for the
  list). Portal Teachers page grew a Substitutions panel (absent
  teacher + date → slots with "Cover by" selects → publish → cover
  list). Integration test: busy teacher excluded, free suggested,
  publish + re-plan shows covered, self-cover refused. E2E live:
  Vikram Sharma (EMP-002, new demo teacher) covers Anita's Monday
  P1+P2 Mathematics — plan suggested only him, both applied, list
  reads "Anita Rao → Vikram Sharma".
- C5 Observability — DONE. ROADMAP v2 COMPLETE (A1–A5, B1–B8, C1–C5).
  OpenTelemetry wired in Program.cs, dormant until Otlp:Endpoint is
  configured (same config-activation pattern as Razorpay/MSG91):
  traces (ASP.NET Core with /health filtered, HttpClient, Npgsql
  ActivitySource) + metrics (ASP.NET Core, HttpClient) to OTLP gRPC;
  resource schoolerp-api + assembly version. Packages aligned on 1.12
  (exporter's moderate protobuf advisory is transitive and below the
  CI High/Critical gate). docs/observability.md runbook: grafana/
  otel-lgtm two-command local stack, first dashboards + alert
  thresholds, log correlation via Serilog TraceId, and what is
  deliberately NOT exported. Verified live: API booted with
  Otlp__Endpoint set — clean start, health 200, no exporter errors.

## Roadmap v3 (insights & growth — see docs/roadmap-v3.md)

- D1 Sibling fee views — DONE. GetParentFamilyFeesQuery (children via
  ParentAccess, deduped) and GetStudentFamilyFeesQuery (siblings via
  shared guardians) compose per-child current-year summaries through
  the existing GetStudentFeeSummary pipeline (fines + concessions
  included); family balance = Σ child balances; children not enrolled
  this year are skipped. GET parent/family/fees (family guard) and
  GET fees/students/{id}/family (fees.view). Parent app: FamilyFeesCard
  (only with 2+ children; per-child balance rows + family total,
  en/te). Portal profile fees panel: "Family ledger" with balance
  chip + sibling profile links. Integration test: sibling admitted on
  the same guardian phone → both views list 2 unique children, family
  total = sum, parent-by-phone view matches. E2E live: Aarav Reddy
  (Grade 6 A, new demo sibling for Priya) — app card shows both
  children + total; portal shows the ledger with ₹25,000 balance.
  Demo data: Grade 6 fee plan (Tuition ₹25,000 due 06 Oct) added.
- D2 Admissions enquiries CRM — DONE. AdmissionEnquiry entity (RLS,
  AddAdmissionEnquiries migration): child name/DOB, applied class
  (free text), parent name/phone/email, source WalkIn/Phone/Website/
  Referral, pipeline New → Contacted → Visit → Admitted/Lost,
  follow-up date, notes, StudentId stamped on conversion. Permissions
  admissions.view/manage (claims backfilled at startup). API:
  GET/POST admissions/enquiries, PUT {id} (Admitted refused — the
  convert action owns it), POST {id}/convert. Board query: follow-ups
  due first (open statuses only), newest next, capped 500. Dashboard
  gains OpenEnquiries + EnquiryFollowUpsDueToday (both exclude
  Admitted/Lost). Portal "Admissions" page: open/status filters,
  due rows highlighted + warning banner, inline update panel
  (status/follow-up/notes), New-enquiry form, Convert → admit form
  pre-filled (child, DOB, guardian name/phone/email via query params;
  "From enquiry" chip) → on admit auto-calls convert → "View student"
  link on admitted rows. 2 integration tests (pipeline + conversion
  guards; dashboard tile deltas). E2E live: Sanvi Verma enquiry —
  registered, follow-up due highlight, Contacted, converted to
  ADM-2026-0004, dashboard tiles show counts.
- D3 Management insights — DONE. Permission insights.view (backfilled;
  SchoolAdmin only — the teacher bundle doesn't include it).
  GET insights/management (GetManagementInsightsQuery): 30-day daily
  roll-call % trend, 6-month fee series (quiet months zero-filled) +
  outstanding (base fees: structure − concessions − payments, floored
  per student; late fines excluded, captioned as such), per-class
  attendance % this month, per-published-exam average % (current
  year), enquiry funnel, substitutions count this month. Portal
  "Insights" page: MudChart line (trend), bars (fees, class
  attendance, exam averages), donut (funnel), substitutions tile —
  each with a plain-language caption ("Collections up X% vs last
  month", best/lowest class) and a friendly empty state. Integration
  test seeds attendance + fees + published exam + enquiries and
  asserts every series. E2E live: all six panels render with demo
  data (100% trend, ₹30,000 collected / ₹85,000 outstanding, Grade 5
  60%, Mid-Term 1 average, 50% funnel conversion donut, 2 subs).
  Also: cleared pre-existing CA1725/CA1862/CA1711 analyzer warnings
  (full-rebuild-only) — builds are zero-warning again apart from the
  accepted OTel NU1902 advisory.
- D4 Teacher performance insights — DONE. GET insights/teachers
  (GetTeacherInsightsQuery, insights.view): per active teacher —
  periods/week (published timetable slots), average % across the
  published papers of the (subject, class) pairs they teach, delta
  vs the school average over those same exams (so a hard exam
  doesn't read as a weak teacher), days absent (distinct substitution
  dates as absentee), marks backlog (their current-year papers with
  zero mark entries). Nulls until any of their papers publish.
  Insights page gains a "Teaching outcomes" section: grouped bar
  chart (teacher vs school average, only teachers with results) +
  full table (delta colored, backlog chip), captioned explicitly as
  correlation, not verdict. Integration test: teacher with a
  published Science paper (40/50 → 80%, delta 0), one covered
  absence, zero backlog; second teacher with no timetable → null
  averages. E2E live: Anita Rao 2 periods/week, 92%, +0 pts, 1 day
  absent; Vikram Sharma dashes.
- D5 Student peer comparison — DONE. GetStudentInsightsQuery: latest
  published current-year exam with marks for the child's class →
  per-subject child % vs SECTION average, rank/section size (total
  marks, same rule as report cards), plus this month's daily
  attendance % child vs section average. Aggregates only — no peer
  is ever named. Endpoints: GET parent/children/{id}/insights
  (family-guarded) and GET students/{id}/insights (students.view).
  Parent app "How is my child doing?" card: proportional flex-width
  bars (no chart lib) per subject + attendance, rank line, anonymity
  footnote, en/te (verified in both). Portal profile gains a "Peer
  comparison" panel beside the report card (MudProgressLinear bars,
  same aggregates-only caption). Integration test: two section
  peers 90%/70% → child 90 vs class 80, ranks 1 and 2, attendance
  100 vs 50; earlier insights tests made order-independent
  (guarded duplicate attendance insert, DB-derived expectations).
  E2E live on all three surfaces (parent EN, parent TE, portal).
  Gotcha: Metro on OneDrive missed file changes — restart the Expo
  server to pick up new modules; RN-web chip taps need dispatched
  pointer+mouse event sequences at the element's own coordinates.

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
- College admin: `COLL01` + `admin@demo.college` / `ChangeMe@12345` (the only
  tenant with departments and programmes — use it to see that side)
- Parent (OTP): school `DEMO01`, phone `+919876501234` (Priya Reddy — guardian
  of demo student Ananya Reddy, Grade 5 A). OTP code appears in API log as
  `[DEV SMS]`.
- Driver (OTP): school `DEMO01`, phone `+919888877766` (Ramesh Kumar, assigned
  to Route 1 — West; Ananya rides from stop "Jubilee Hills").
- Teacher: `DEMO01` + `+919888811122` / `Anita@2026Pass` (Anita Rao EMP-001,
  "Teacher" role — classroom permissions only).

## Demo dataset (DemoDataSeeder — runs at every dev API startup, idempotent)

`Persistence/Seeding/DemoDataSeeder.cs` enriches DEMO01 so demos look real
(each block checks before inserting; safe to re-run; invoked from DevSeeder,
never in production):

- 16 seeded students (ADM-2026-0101…0116, 8 per section in Grade 5 A and
  Grade 6 A) with one guardian each (phones `+9198765002xx` — all can OTP
  into the parent app), on top of the hand-made students.
- Attendance: daily roll-call for the last 30 days (Sundays skipped),
  deterministic ~90/4/5/1 present/late/absent/half-day mix.
- Exams: "Unit Test 1" (Jul, /50) and "Mid-Term 1" (Aug, /100), both
  Published, papers in Maths/Science/English for both grades, marks for
  every student (stable 42–97% ability distribution).
- Fees: structures ensured (G5 ₹30,000, G6 ₹25,000); a third of the cohort
  fully paid over Jun+Jul, a third part-paid in Aug, a third outstanding
  (receipts RCP-2026-1001+); 2 concessions.
- Timetable: Mon–Sat published class-wide slots for both grades across 4
  teachers (Anita, Vikram, + seeded Sunita Devi EMP-003, Ravi Prasad
  EMP-004) — drives periods/week and teaching-outcome insights.
- Substitutions (3 covers this month), admissions pipeline (7 enquiries
  across New/Contacted/Visit/Lost with due follow-ups), 3 notices, 3
  homework items, 5 library books with loans (1 overdue), 1 pending leave
  request, transport stop assignments for 4 students.
- A fresh database is demo-ready after one API start: the shell seed runs
  first and the enrichment runs right after it in the same startup.

## Excel bulk import (students)

- GET `students/import/template` (students.manage): .xlsx tailored per
  school — Students sheet (17 columns, dates/phone kept as text) plus an
  Instructions sheet with the school's real classes/sections and an example
  row. Built with ClosedXML (MIT) in Infrastructure behind
  `IStudentImportWorkbook` (Application stays format-agnostic).
- POST `students/import` (multipart, 5 MB / 1,000-row caps): parses,
  validates EVERY row (required fields, dates, gender/relation values,
  class+section resolved case-insensitively, phone shape, admission-number
  duplicates in-file and against the DB incl. erased students via
  IgnoreQueryFilters, and a same-name+DOB re-upload guard whose escape
  hatch is an explicit admission number). All-or-nothing: any bad row
  rejects the file with per-row errors; a clean file admits every row via
  AdmitStudentCommand — so guardian reuse by phone (sibling linking),
  generated admission numbers and audit all behave exactly like the UI.
- Portal Students page: "Import from Excel" panel — Download template +
  Upload buttons, success alert or per-row error table.
  MudBlazor 6 gotcha: MudFileUpload needs `ButtonTemplate` +
  `HtmlTag="label" for="@context.Id"` (ActivatorContent is 7.x and
  silently renders nothing).
- 4 integration tests (clean import + sibling link + case-insensitive
  placement, all-or-nothing with per-row messages, re-upload rejection,
  non-Excel rejection). E2E-verified live via API (3 imported incl.
  cross-grade siblings sharing one guardian → family ledger works;
  re-upload rejected 3/3) and portal UI (panel + template download).

## Grid features (sorting / paging / search / export)

- Students grid sorts SERVER-SIDE: GetStudentsQuery takes SortBy
  (whitelisted keys: name, admissionNumber, class [by DisplayOrder so
  Grade 10 > Grade 5, then section+roll], section, roll, status —
  unknown keys fall back to name) + SortDescending; wired through
  MudTableSortLabel SortLabel/TableState on the page. Postgres sorts
  NULLs first on desc (students without enrollment show "—" on top).
- GET students/export: the grid as students.xlsx honouring the same
  filters + sort (pages through GetStudentsQuery so filter logic lives
  once; 5,000-row cap) — "Export" button next to Import.
- Client-side grids got MudTableSortLabel + MudTablePager: Admissions
  (plus a child/parent/phone search box), Teachers, Leave inbox,
  Library books + loans, Users, and the Insights teaching-outcomes
  table (null averages sort to the bottom via ?? sentinels).
- Integration test covers server sort keys, injection-shaped SortBy
  falling back safely, and export honouring filter + sort.

## Product name + per-school branding

- Product display name is **Akshara** (అక్షర — "letters/first learning";
  the Aksharabhyasam nod fits the Telugu market). DISPLAY ONLY: page
  titles, appbar, login, PDF footers ("Generated by Akshara"), app
  titles en+te. Code identity (namespaces, csproj, DB, docker,
  window.schoolErpDownload) intentionally unchanged — renaming those
  is churn with no user value.
- Tenant branding was half-built (entity + UpdateTenantCommand had
  LogoUrl/ThemePrimaryColor/ThemeSecondaryColor; nothing surfaced).
  Now wired end-to-end:
  - TenantDto (API + portal) exposes the theme colours.
  - POST tenants/{id}/logo (tenants.manage, 1 MB, png/jpg/webp) →
    UploadTenantLogoCommand saves under the TARGET tenant via a new
    explicit-tenant IFileStorage.SaveAsync overload (Super Admin has
    no ambient tenant), stamps LogoUrl, deletes the previous file.
  - GET tenants/branding?code=X — ANONYMOUS (name, logo, colours
    only) so login screens/apps can theme before sign-in;
    case-insensitive code; 404 unknown.
  - Tenant editor "Branding" section: logo preview + upload,
    MudColorPicker ×2 (PickerVariant.Inline — 6.x has no Popover).
  - Portal chrome is school-branded: Login stores the school code in
    localStorage (akshara.schoolCode; cleared on platform sign-ins so
    Super Admin never wears a school's brand), MainLayout fetches
    branding anonymously and applies logo + school name ("on
    Akshara") + MudTheme primary/secondary. Survives hard refresh.
  - DemoDataSeeder gives DEMO01 teal/amber (#00695C/#FF8F00) when
    unset; demo logo uploaded via API.
- Integration test: logo upload lands under the target tenant, replace
  deletes the old file, anonymous lookup by lowercase code carries the
  theme, unknown code 404s. E2E: teal appbar + logo + school name as
  school admin; default blue "Akshara Admin" as Super Admin; editor
  section verified with save round-trip.

## Platform billing (invoices, SMS top-ups, usage)

- Invoice + InvoiceLine entities (platform-scoped, no RLS — see
  exception list): sequential per-year numbers ("INV-2026-0001",
  count-based, unique index arbitrates), Issued → Paid/Void lifecycle
  (paid stays paid; only issued can be voided), denormalized
  TotalAmount = Σ round(qty × unit). Migration AddInvoices.
- Application/Billing: CreateInvoice (validated lines), MarkInvoicePaid
  / VoidInvoice (409 on re-settle), RecordSmsTopUp (credits land on the
  tenant AND an issued invoice records the receivable in one command —
  they can't drift), GetInvoices (filter by school, names joined),
  GetInvoicePdf (QuestPDF A4, Akshara-branded, PAID/VOID stamp),
  GetTenantUsage — platform tables read directly (outbox SMS/push
  counts by TenantId, outstanding invoice sum), RLS tables (students,
  fee payments) read via a fresh scope pinned to the target tenant
  (ITenantContextSetter — same pattern as background jobs).
- API: /billing/* — usage, invoices CRUD-ish, pdf, sms-topup; all
  tenants.manage (operator-only).
- Portal: Schools menu → "Billing & usage" per school: usage cards
  (active students, SMS credits + 30-day SMS/push split, fees
  collected 30d, outstanding), SMS pack buttons (5k/10k/25k at an
  editable ₹/SMS), invoice builder with quick lines ("Annual licence
  (N students)" prefilled from live usage, onboarding fee, custom),
  sortable invoice ledger with Mark paid / Void / PDF download.
- 2 integration tests (lifecycle incl. PDF magic bytes; top-up +
  usage coherence). E2E live: top-up 5k → INV-2026-0001 ₹1,750;
  licence invoice 23 × ₹70 = INV-2026-0002 ₹1,610 → marked paid;
  PDF downloaded (52 KB).
- GOTCHA (bit us live): `dotnet ef migrations add` followed by
  `database update --no-build` applies NOTHING — the new migration
  isn't compiled yet, and update still prints "Done." Build (or drop
  --no-build) between add and update, and check `migrations list`
  for "(Pending)" when in doubt.

## Go-live batch (platform security, packages, S3, jobs, self-serve)

- SECURITY FIX (was a real cross-tenant escalation, verified live with
  a 200 before the fix): SchoolAdmin roles were backfilled with ALL
  permissions including tenants.view/manage, and authorization only
  checked the permission claim — so a school admin could list/edit
  every school and grant themselves SMS credits. Fixed in depth:
  - `Permissions.PlatformOnly` (tenants.*) vs `TenantAssignable`;
    school seeds/backfills use TenantAssignable, and the backfill now
    STRIPS platform claims from existing school roles on startup.
  - Role editing validators only accept TenantAssignable.
  - `[PlatformOnly]` policy (principal must have NO "tenant" claim) on
    TenantsController + BillingController — school tokens are refused
    even if a claim sneaks in. Verified: school admin 403, super 200.
- Package & modules control (tenant editor): choosing a Plan applies
  its module preset (PlanPresets in Domain — Basic: Core+Exams+Fees;
  Standard: +Transport/Library/Timetable/Homework; Premium:
  +Hostel/FrontOffice; Enterprise/Trial: everything) with per-plan
  ₹/student rates shown; checkboxes stay hand-tunable; subscription
  expiry is now an editable date picker (expired = logins blocked).
- S3-compatible IFileStorage (AWSSDK.S3, Apache-2.0): AWS/R2/MinIO via
  Storage:Provider=s3 + Storage:S3:{Bucket, Region|ServiceUrl,
  AccessKey/SecretKey or default chain}. Identical key shape to local
  disk, so stored URLs survive the switch. (MSG91 SMS sender already
  existed behind Sms:Provider=msg91 — no work needed.)
- BillingCycleJob (nightly 02:00 UTC): in Billing:RenewalMonth
  (default April) auto-invoices every active paid-plan school —
  students × plan rate, idempotent per season via the licence line
  tag; and (opt-in Billing:AutoSuspend, grace
  Billing:SuspendGraceDays=30) suspends schools with invoices unpaid
  past grace. Integration-tested (renewal idempotency + suspension).
- School self-serve: permission subscription.view (auto-backfilled),
  GET /subscription (+ own-invoice PDF with ownership check → 404 for
  foreign ids). Portal "Subscription" page: plan/credits/amount-due/
  modules cards, invoice list with PDF. Nav is principal-aware:
  platform users see Schools, school users see Subscription.
- Parent app pre-login branding: typing the school code fetches the
  anonymous branding endpoint (debounced) — school logo, name and
  primary colour appear on the login screen before sign-in.
- E2E verified across all of it; suite 157 green.

## Dashboard v2 (feature/dashboard-v2)

- The staff dashboard is now a command centre: quick-action buttons
  (mark attendance / admit / collect fee / import), six stat cards
  (attendance today, collected this month, OUTSTANDING fees — same
  floored per-student calc as insights, active students, overdue
  books, SMS credits with a low-balance warning under 500), a 14-day
  attendance line + 14-day daily-collections bars, TODAY'S BIRTHDAYS
  (name, class, age — assembly announcements), upcoming exams, the
  needs-attention list, and a subscription-expiry banner within 30
  days. All new strings localized en/te. Demo: the first child of
  each seeded cohort gets today's month/day as DOB so the birthday
  card always shows.
- GOTCHA: projecting into a positional record + OrderBy on the
  projected property inside the EF query didn't translate — fetch
  anonymous rows, compose records client-side.
- Branch workflow starts here: feature/<slug> per change, merge
  --no-ff into master when green (user directive 2026-08-09).

## Enterprise UI programme (design system, shell, list pattern, mobile)

Benchmarked against Dynamics 365, ServiceNow, Salesforce, Power BI, Fiori
and Atlassian/Linear. The reference-to-principle mapping and the per-screen
quality gate live in docs/design-system.md — read that before touching UI.

DONE and browser-verified:
- Portal design system: wwwroot/css/design-tokens.css (colour ramps,
  semantic aliases, 4px spacing, restrained type/radius scales, elevation,
  dark mode) mirrored by Theme/AksharaTheme.cs. BOTH layers must agree —
  MudBlazor renders colours into inline styles CSS variables never reach.
- Shared primitives in Shared/Ui: AkPageHeader, AkCard, AkMetric,
  AkStatusBadge, AkEmptyState, AkErrorState, AkTableSkeleton, AkFormSection,
  AkFilterChips, AkBulkBar, and AkConfirm.AskAsync (takes an action AND its
  consequence — never "Are you sure?").
- App shell: grouped collapsible sidebar driven by Layout/NavModel.cs
  (7 sections, platform/school aware) replacing a flat 22-item list; neutral
  app bar; skip link; focusable main landmark.
- Students is the REFERENCE list page. Copy its shape for the rest:
  command bar (primary action rightmost), toolbar, removable filter chips
  with live record count, selection + bulk bar, status badges, and four
  distinct states (loading skeleton / empty-new / empty-filtered / error).
- Mobile: shared tokens + primitives in BOTH apps' src/design (duplicated
  deliberately — separate Metro roots; change one, change the other).
  Parent app leads with TodayTripCard + TripTimeline. Driver app has
  StopBoarding Trip Mode (one stop expanded at a time, thumb-sized actions).

GOTCHAS (all cost real time this session):
- app.css loads LAST. Its template html/body rule silently beat the Inter
  stack. Anything added there outranks the design system.
- MudBlazor's .mud-navmenu selectors (0-5-0) out-specify plainer rules —
  design-system nav styling must be over-qualified to win.
- Setting --ak-brand inline from JS pins the light value; dark mode can then
  never override it. Publish --ak-brand-light/--ak-brand-dark instead.
- A school brand colour reused on dark surfaces measured 1.44:1. The dark
  variant is lifted 55% toward white; MEASURE, do not eyeball.
- The global :focus-visible ring doubled up inside MudBlazor inputs (they
  draw their own). Inputs get one indicator: their own outline, thickened.
- Razor parses a pattern variable named 'section' as the @section directive.
- MudBlazor inputs are width:100%; toolbar controls need flex: 0 0 auto.
- A table's ServerData fills parent state during its own async load — call
  StateHasChanged or counts/chips never render.
- Drawer state must be per-breakpoint. One flag defaulted true painted the
  nav over the whole page on phones before any breakpoint event fired.
- The Browser pane's "desktop preset" resets to NATIVE width (~618px here),
  which is below the compact breakpoint. Set an explicit 1440x900 to review
  desktop layouts.

REMAINING (in agreed order):
1. ~~Notification localization~~ DONE — see "Notification localization" below.
1b. College support is COMPLETE — campuses + institution type, the Super Admin
   dashboard, departments/programmes, programme enrollment and the college
   wording are all done (sections below). Still open from that thread: a
   stored contract price per school so ARR is contractual rather than
   list-rate, and scoped impersonation so support can see what a school sees.
2. Fee refunds (mid-year withdrawals).
3. Nine list pages onto the Students pattern: Teachers, Fees, Transport,
   Inventory, Front office, Library, Hostel, Users, Audit, Admissions.
4. Forms + record-detail pattern (header + tabs) for Student, Teacher,
   Route, Tenant.
5. Parent app bottom-tab nav + Children/Trips/Notifications/Profile; live
   tracking (map + bottom panel).
6. Driver app: drop-off, trip summary, exceptions, emergency, offline queue,
   GPS state.
7. Accessibility + responsive sweep. Check whether the input focus-ring
   conflict also affects selects, checkboxes and date pickers.
8. Optional: promote the duplicated mobile design system to a shared package
   (needs Metro watchFolders + extraNodeModules in both apps).

NOT DOING: redesign of the SCHOOL dashboard (user asked to leave Home.razor
as-is — the platform dashboard added later is a separate page); HR/payroll
(own product).

## Notification localization (feature/notification-localization)

- Closes the gap the app localization left open: the apps were bilingual but
  every SMS, WhatsApp and push message was English. `Guardian.PreferredLanguage`
  (migration 20260809121722_AddGuardianPreferredLanguage — column only, the
  table already has RLS; `defaultValue: "en"` backfills existing rows).
- `SchoolErp.Shared/Localization/NotificationStrings.cs` mirrors PortalStrings:
  En is the source of truth, Te coverage is unit-enforced, each template has a
  `.title` (push) and a `.body`. Templates: absence, results published, payment
  received, bus boarded/dropped, gate pass, fee reminder.
  `NotificationLanguages.Normalize` maps "TE"/"te-IN"/junk/null onto a language
  we actually have, so an unknown code degrades to English instead of throwing.
- **Callers pass a template key and args, never finished prose.**
  `NotificationQueue.QueueGuardianAsync(db, tenant, phone, templateKey, args, ct)`
  looks the language up by phone inside the tenant scope and renders there —
  so SMS, the WhatsApp route and push all carry the same localized copy with
  no per-channel work. Dates and amounts are formatted in the reader's own
  culture (te-IN → "09 ఆగ 2026"); school-entered data (child, school, receipt
  numbers) stays exactly as entered.
- `SmsPayload` gained `Template` (nullable, so old rows deserialize).
  FeeReminderJob's 6-day dedupe now matches on that marker instead of the
  English prefix "Fee reminder:" — matching message text would have stopped
  deduplicating the moment a guardian switched to Telugu.
- Setting it, two ways:
  - Office: `PUT students/guardians/{id}/language` (students.manage); the
    student profile's Guardians panel has a per-guardian select that saves on
    change. `GuardianInput`/`GuardianDto` carry the language too, so the
    admission form sets it at intake (optional param — the Excel importer and
    every existing caller are untouched). A sibling admission may correct an
    existing guardian's language; nothing else about the record is touched.
  - Parent: `GET/PUT parent/language`. At sign-in the two sides reconcile once
    and the reader wins — a device that has been switched pushes its choice, a
    fresh install adopts what the school recorded (so a reinstall can't clobber
    an office-entered Telugu preference back to English). Later toggles push
    directly from `setLang`.
- 5 integration tests (Telugu vs English side by side, localized push, the same
  text over WhatsApp, the parent toggle changing later alerts, an unsupported
  language refused) + 6 unit tests. Suite: 61 unit + 129 integration green.
- E2E live on DEMO01: profile select → snackbar → absence marked → API logged
  the Telugu WhatsApp *and* the Telugu push; then the parent app toggle →
  one PUT → next absence for the sibling also Telugu. Priya Reddy is left on
  Telugu so the feature is demoable without setup.
- GOTCHA: `useRef` guard on the sign-in reconciliation — without it, flipping
  the toggle re-entered the effect (its `chosenHere` dep flips) and sent the
  same PUT twice. Caught in the browser, not the tests.
- Spotted here, fixed in fix/validation-log-noise (below): expected exceptions
  were logged as faults.

## Expected exceptions stopped logging as faults (fix/validation-log-noise)

- Symptom: every rejected form field, every 404 lookup and every 409 wrote
  `ERR ... An unhandled exception has occurred` plus a full stack trace, while
  the caller correctly received 400/404/409. Real faults were buried.
- TWO causes, and the obvious half-fix is not enough:
  1. Serilog's request logging sat INSIDE `UseExceptionHandler`, so it saw the
     raw exception and recorded "responded 500". Swapping the two (Serilog
     outermost now) makes it record the status the client actually got.
  2. The one that actually mattered: ASP.NET's exception-handler middleware
     logs "An unhandled exception has occurred" at Error, with the stack,
     BEFORE it consults any `IExceptionHandler`. Reordering does nothing about
     that, and .NET 8 has no hook to suppress it (`SuppressDiagnosticsCallback`
     is .NET 9). The fix is to keep expected exceptions away from the
     middleware entirely.
- `ApplicationExceptionMapper` now holds the exception→ProblemDetails mapping,
  used by two entry points: `ApplicationExceptionFilter` (a global MVC
  `IExceptionFilter`, where essentially every one of these is thrown) and the
  existing `GlobalExceptionHandler` (`IExceptionHandler`), still registered as
  the net for exceptions raised outside MVC. Unmapped exceptions are left
  untouched by both, so a genuine fault still logs in full and 500s.
- ALSO FIXED, found while testing: validation responses had been dropping their
  per-field `errors`. The switch expression typed the variable as
  `ProblemDetails`, and `WriteAsJsonAsync<ProblemDetails>` serializes by the
  DECLARED type, so `ValidationProblemDetails.Errors` never reached the client
  — callers only ever saw the generic title. `ObjectResult` (filter path) and
  `WriteAsJsonAsync<object>` (handler path) both serialize by runtime type.
- 6 tests in `IntegrationTests/Api/ExceptionMappingTests.cs` (no database —
  they pin the pipeline contract): fields survive on a 400, 404/409 mapping,
  concurrency → 409, and an unmapped exception left unhandled so it still
  reaches the middleware.
- Verified live: validation → one `INF … responded 400` and a body carrying
  `errors`; 404 → one INF line; and a genuine fault (a platform account
  hitting a tenant-scoped query) still logs ERR with the whole stack.
- Suite: 61 unit + 135 integration green.

## Multiple board affiliations (feature/multiple-board-affiliations)

- A school can be affiliated to several boards at once — CBSE plus a State
  stream is ordinary in India, and each affiliation carries its own number —
  so `Tenant.AffiliationBoard`/`AffiliationNumber` became the child table
  `tenant_affiliations` (unique per tenant+board).
- Platform-scoped, no RLS: it hangs off `tenants`, which has none either.
  Added to the documented exception list. Callers scope by TenantId.
- MIGRATION ORDER MATTERS and is not what EF scaffolded. EF put the column
  DROPS first, which would have destroyed every school's existing
  affiliation. Rewritten to create → backfill (`gen_random_uuid()`, built in
  from PG13) → drop. `Down` copies the alphabetically-first board back, so
  reversing is deterministic rather than arbitrary.
- `ApplyAffiliations` (shared by create and update) replaces the set, matching
  by board so an existing row keeps its id when only its number changes; blank
  boards are dropped and duplicates collapse before the unique index sees them.
- Certificates print every board on the letterhead, joined — previously only
  the single one. Tenant editor gained repeating board+number rows with an
  "Add board" button; the affiliation NUMBER now has a UI at all, which it
  never did.
- GOTCHA that cost real time: `TenantAffiliation.Id` is deliberately NOT
  client-generated, unlike `AuditableEntity`. These rows are discovered
  through the `Tenant.Affiliations` navigation, and a key that already holds a
  value makes EF treat a brand-new child as an UPDATE of a row that does not
  exist — surfacing as `DbUpdateConcurrencyException: expected to affect 1
  row(s), but actually affected 0`.
- 1 integration test (create with one board, add a second, drop one keeping
  the survivor's row, blank boards ignored). Suite: 65 unit + 158 integration.

## Departments & programmes for colleges (feature/college-structure)

The platform could mark a tenant as a College and then hand it the
classes-and-sections shape of a K-12 school. Closed.

- THE DESIGN DECISION: a college does NOT get a parallel SIS. `Department` and
  `Programme` (both RLS'd) sit ABOVE the existing structure, and a cohort —
  "B.Tech CSE Semester 1" — is an ordinary `SchoolClass` with a nullable
  `ProgrammeId` pointing at its programme. That reuse is what keeps
  attendance, timetables, exams and fees working for a college without a
  second implementation of each. Schools leave `ProgrammeId` null and nothing
  about them changes.
- `Programme` carries Level (Certificate/Diploma/UG/PG), DurationYears and
  TermsPerYear (2 = semesters), so duration × terms is how many cohorts a full
  intake passes through.
- No new permission constants — this is academic structure, so it reuses
  `academics.view`/`academics.manage`. Nothing to backfill.
- Rules: department and programme codes are unique per institution and
  uppercased on write, department names too; a department cannot close while
  it still runs an active programme; a closed programme takes no new cohorts;
  a head of department must be somebody on the staff list. Closing, never
  deleting — FK deletes are Restrict so closing a programme can never take the
  cohorts (and their marks) with it.
- The portal learns the institution type from the BRANDING endpoint, which
  MainLayout already fetches to theme itself. A JWT claim would have been the
  other option, but it only reaches the UI after the next sign-in. Branding is
  anonymous and an institution's own website says which it is.
  `NavItem.CollegeOnly` hides the Departments page from schools; the class
  form's programme picker hides itself when the programme list comes back
  empty, which at a school it always does.
- DEMO COLLEGE: `COLL01` / `admin@demo.college` (same password), seeded
  idempotently at EVERY startup rather than only on a fresh database, so an
  environment seeded before colleges existed still gets one. Two departments,
  three programmes, two cohorts, a primary campus, and deliberately NO
  students — which makes the platform dashboard's "active but nobody enrolled"
  check fire, truthfully.
- GOTCHA: the seeder writes into RLS'd tables. Tenant and identity rows go in
  a tenant-less scope; everything tenant-scoped needs a SECOND scope bound to
  the new college, or the FORCEd policy's WITH CHECK rejects every row.
- 7 integration tests, each on its own college.
- Browser-verified: college admin sees Departments in the nav and the
  programme picker on the class form; creating "MCA Semester 1" against MCA
  moved its cohort count from — to 1; the school admin sees neither.
  Suite at that point: 65 unit + 176 integration.

### Enrolling into a programme (same branch line, feature/college-enrollment)

- `Enrollment.ProgrammeId`, STAMPED from the cohort at admission rather than
  asked for — the caller already chose the class, and a second field could
  only ever contradict it. Never re-read from the class afterwards either:
  re-pointing a cohort at another programme must not rewrite what past
  students were enrolled in.
- Promotion stamps the programme of the class they moved INTO, so a lateral
  move between programmes is recorded rather than inherited.
- MIGRATION: FORCE has to come OFF around the backfill. `app_current_tenant_id()`
  returns NULL during a migration, so `tenant_id = NULL` is NULL and the
  policy's USING clause hides every row from the owner — the UPDATE would
  report success having changed nothing. That is the quiet twin of the
  campuses INSERT problem and strictly worse, because nothing errors.
- Admission form for a college: a Programme select that narrows the cohort
  list, and "Semester / cohort" instead of "Class". With no programme picked
  the full list still shows, so a cohort that predates its programme is never
  hidden from someone trying to admit into it. A school sees neither — the
  programme list comes back empty and both the select and the relabel are
  keyed off that, not off a second institution-type lookup.
- Departments page gained a Students column, counted off the enrollment's own
  programme stamp. That count is the reason the column is denormalized.
- The demo college gained an academic year — without a current year nobody can
  be admitted, which made it unusable rather than merely empty. Seeded via a
  top-up path that runs even when the college already exists, since the
  creation guard would otherwise skip it forever.
- 2 more integration tests (admission stamps the programme and the count
  follows; promotion into a different programme records the move and the
  closed placement keeps its history).
- Browser-verified: admitted Rahul Sharma into MCA Semester 1, MCA went from
  1 cohort / — students to 1 / 1; the school's admit form still says "Class"
  with no Programme field.
- ~~STILL OPEN: the rest of the college UI still says "class"~~ DONE — see
  "College wording" below.
  Suite: 65 unit + 178 integration.

## Transcript PDF (feature/transcript-pdf)

- `QuestPdfTranscriptRenderer` (same Community licence as the other
  renderers): school header, student block, one table per semester with its
  SGPA, and the CGPA. `GET exams/students/{id}/grade-sheet/pdf`, with a
  Download button beside the CGPA on the profile panel.
- REFUSES to print when there is no CGPA, returning 409 with the reason. A
  consolidated grade sheet whose entire purpose is the CGPA is worse than no
  document at all if it goes out blank.
- The footer states WHICH ordinance produced the grades — the institution's
  own or the UGC scale. A transcript that does not say is unverifiable by
  whoever receives it.
- 1 integration test: refused before publication with the reason, then a real
  PDF (checked for the `%PDF-` magic bytes, not merely non-empty).
- HTTP-verified end to end: real login as the college admin → bearer token →
  `GET .../grade-sheet/pdf` returned a 32,824-byte PDF. That exercises auth,
  routing and permissions, which the integration test (which goes straight
  through MediatR) does not.
- STILL NOT VISUALLY VERIFIED, and it cannot be from here — see the gotcha
  below. Open one before showing it to a customer.
  Suite: 88 unit + 185 integration.

## Per-university grade scales (feature/grade-scales)

The UGC 10-point table is a RECOMMENDATION, not a rule. Universities differ on
where each grade starts and what it is worth, and a transcript printed against
the wrong scale is wrong in a way nobody notices until a student disputes it.

- `GradeBand` (RLS'd, `grade_bands`): MinPercent, Letter, Point. Unique per
  (tenant, MinPercent) — two bands starting at the same percentage would make
  the grade depend on row order.
- NO bands means the UGC default still applies, so nothing changed for anyone
  who has not looked. `GetGradeScaleQuery` returns `IsInstitutionDefined` so
  the caller can tell a chosen scale from an unexamined one.
- `CbcsGradeCalculator` gained band-taking overloads and SORTS the bands
  itself: a list entered out of order in a settings screen would otherwise
  award the first band that matched rather than the highest one that did.
- Whole-set replacement, not per-band edits — a scale is only coherent as a
  set, and editing one band at a time leaves gaps and overlaps live between
  saves.
- Published results are NOT recomputed when the scale changes. A transcript
  that silently changes after the fact is worse than one that is out of date;
  reissuing has to be deliberate.
- The grade sheet loads the scale ONCE per request and threads it through
  every paper, so a mid-request change cannot split one transcript across two
  ordinances.
- Portal: a Grading scale editor on the Exams page (college only), pre-filled
  with whatever is in force — the UGC fallback included, so an institution
  edits a real table instead of starting from an empty one.
- 1 integration test proving the point: 75% is an A worth 8 under the UGC
  scale and a B worth 6 under a stricter ordinance, same marks, same student.
- STILL MISSING: relative/percentile grading (some IITs curve rather than band
  absolute marks), and per-programme scales — the ordinance is institution-
  wide.

## CBCS credits, SGPA and CGPA (feature/cbcs-credits)

- `CbcsGradeCalculator` (Domain): the UGC 10-point scale — O 10, A+ 9, A 8,
  B+ 7, B 6, C 5, P 4, F 0, Ab 0 — plus credit-weighted GPA. Deliberately
  SEPARATE from `GradeCalculator`, which maps CBSE school bands (A1…E) and has
  no notion of a credit; sharing one band table would make both wrong.
- Failed and absent papers stay in the DENOMINATOR. Dropping them would
  quietly reward failure — five O's and one F is 8.33, not 10.00.
- No credited paper returns NULL, not 0.00: a school's exam, or a semester
  nobody set credits on, is "not measured", not "everyone failed".
- `ExamSubject.Credits` (nullable) — on the PAPER, not the subject, because
  the same subject is worth different credits in different programmes.
  Validated 1–12; a wrong credit silently skews every GPA it touches.
- `GetStudentGradeSheetQuery` → per-semester SGPA + cumulative CGPA + credits
  earned vs attempted, from PUBLISHED exams only (draft marks are not
  results). `GET exams/students/{id}/grade-sheet`. When there is no GPA it
  says WHY — "no published results yet" and "no paper carries credits" are
  different problems and the caller should not have to guess from a null.
- Portal: a Credits field on the schedule-paper form, college only.
- ~~CAVEAT: no per-institution scale~~ CLOSED — see "Per-university grade
  scales" below. The UGC table is now the FALLBACK, not the only option.
- 12 unit tests on the arithmetic (band boundaries, credit weighting, failure
  in the denominator, absence, zero-credit papers, null vs zero) and 2
  integration tests (a published MCA semester scoring 6.67 across a 4-credit A
  and a 2-credit P; a school exam reporting no GPA with the reason).
- Grade sheet UI: a panel on the student profile (college only) — CGPA,
  credits earned vs attempted, then a card per semester with its SGPA and
  paper table (failed grades in red). When there is no GPA it renders the
  reason, not a blank.
- Demo college now carries a PUBLISHED semester: 3 students, Engineering
  Maths (4 cr) and Programming in C (3 cr), marks spread so one student fails
  a paper. Verified live — Aditya 9.57 (4×10 + 3×9)/7, Rohit 2.57
  (4×0 + 3×6)/7 with "3 of 7 credits earned".
- SEEDER GOTCHA: the top-up guard was `Students.AnyAsync()`, and the college
  already had one student admitted by hand, so it skipped forever. Guard a
  top-up on the thing it creates (the exam), not on a general precondition.
- STILL MISSING: cumulative transcript PDF, arrear/supplementary exams,
  per-student electives, student logins, per-university grade scales.
  Suite: 88 unit + 183 integration.

## Semester promotion (feature/semester-promotion)

A college could be set up but could not run its year. Indian higher education
puts an odd and an even semester INSIDE one academic year (Jul–Dec, Jan–May),
and two things refused that.

- `PromoteClassCommandValidator` demanded a different target academic year.
  Replaced with "must move students somewhere": same year is fine as long as
  the class or section differs; only an identical placement is refused.
- THE REAL BLOCKER was a database constraint, not the validator:
  `ix_enrollments_tenant_id_student_id_academic_year_id` was a plain unique
  index — one enrollment per student per year. Two semesters in one year are
  two enrollments in that year, so every same-year advance died on a 23505.
  Narrowed to a PARTIAL unique index filtered on `status = 1 AND
  is_deleted = false`: one ACTIVE placement per student per year, which is the
  invariant that actually matters, with the closed rows left as history.
- The re-run guard needed two changes once the target year can be the source
  year: exclude the source rows (or every student looks "already enrolled" in
  the year they are being moved within), and count only ACTIVE rows as a
  placement (after Sem 1 → 2 the student still holds a Promoted Sem 1 row in
  that same year, and treating it as a placement refused Sem 3).
- Portal: same-year is allowed; the panel reads "Promote students" with the
  odd-to-even explanation for a college, "Year-end promotion" for a school.
- 3 integration tests: Sem 1 → 2 → 3 inside one year (3 rows of history, one
  Active, all stamped with the programme), re-running an advance changes
  nothing, and an identical-placement move is refused.
- STILL MISSING for a college: credits/GPA/CGPA (CBCS 10-point, SGPA/CGPA),
  cumulative transcripts, arrear/supplementary exams, per-student electives,
  and student logins (roles are SuperAdmin/SchoolAdmin/Teacher; the app
  authenticates guardians by phone).
  Suite: 65 unit + 181 integration.

## College wording (feature/college-wording)

- `Services/InstitutionContext.cs` holds the institution type and the words
  that follow from it (`Cohort`, `CohortPlural`, `CohortAndSection`,
  `CohortNameHint`). One place, not a conditional on every page that names the
  concept.
- It loads itself once from the branding endpoint, so a page never depends on
  the layout having finished first; MainLayout `Adopt()`s the branding it
  already fetched, so the common path costs no extra request. A failure falls
  back to School — wording is cosmetic and must not take a page down.
- Applied to Academics (heading, add button, name hint, empty state, promotion
  selects), Attendance (filter + hint), Timetable, Exams (filter + table
  header), Fees, Homework, Teachers (table header) and the admission form.
- Browser-verified both ways: the college sees "Semesters & sections",
  "Add semester" and "Pick a semester, section and date…"; the school still
  sees "Classes & sections", "Add class" and "Class".
- NOT renamed: the underlying entity is still `SchoolClass`/`school_classes`.
  Renaming the table would touch every module for a vocabulary difference the
  UI already absorbs.
- GOTCHA: portal .razor files are CRLF, so a `perl -0pi` substitution anchored
  on `\n` silently matches nothing. Check the grep after a bulk edit.

## Super Admin dashboard (feature/platform-dashboard)

A platform operator used to land on "Sign in with a school account to see the
dashboard". They now land on the platform's own command centre.

- THE HARD PART was reading across tenants at all. Every people table is RLS'd
  with FORCE, so a cross-tenant COUNT returns zero rows — not an error, which
  is what makes it dangerous. Solved with `app_platform_tenant_counts()`, a
  SECURITY DEFINER function (pinned `search_path`, EXECUTE revoked from PUBLIC
  and granted only to the runtime role) that returns COUNTS GROUPED BY TENANT
  and nothing else, so no row can escape through it. The alternative — looping
  the catalog and re-running every query per tenant — is N round trips on a
  page that loads at every sign-in.
- `IPlatformMetrics` (Application) is the port for the two things a handler
  cannot reach: those counts, and the identity store for staff-account totals.
  Implemented in Infrastructure with raw ADO (the source is a set-returning
  function, not an entity — mapping a keyless type would put a phantom table
  in the model snapshot).
- Everything else comes from tables with no RLS: tenants, invoices,
  audit_events, outbox.
- WHAT IS REAL: institutions by state and kind, students/teachers/guardians/
  campuses/staff accounts, invoiced + collected in the window, outstanding,
  overdue, plan mix, per-institution table, attention centre, activity feed,
  outbox health, onboardings per month.
- WHAT IS NOT, and is SAID so on the page rather than estimated: feature
  adoption, DAU/MAU, mobile adoption, email deliverability, storage per
  school, and contractual MRR/ARR. There is no stored recurring price per
  school, so ARR cannot be computed; what IS shown is "annualised licence
  value" = list rate for each plan × that school's enrolled students, labelled
  in the UI as exactly that.
- Attention centre fires on: expired subscription, renewal ≤30 days, unpaid
  invoices, suspended school, SMS credits <500, active school with no
  students, and onboarding stalled in Provisioning >14 days. Sorted most
  severe first so the top row is the next thing to do.
- `/` branches on the principal: no `tenant` claim means the platform page.
  The school dashboard's old fallback alert became a real empty state for the
  only case that reaches it (a school account without `students.view`).
- 6 integration tests over three seeded schools — busy, empty college,
  suspended — including withdrawn students, an inactive teacher and a closed
  campus, none of which may be counted. The first test asserts the totals from
  a scope with NO tenant bound, which is the RLS trap it exists to catch.
- Browser-verified as Super Admin: real figures (22 students, 4 teachers,
  20 guardians, 3 campuses across 2 schools), no console errors on a clean
  tab, and a mobile viewport with no horizontal overflow.
  Suite: 65 unit + 169 integration.

## Campuses + institution type (feature/campuses)

Groundwork for the Super Admin dashboard: two facts the platform could not
state about its own customers — where an institution operates, and whether it
is a school or a college.

- `Campus` (RLS'd, `campuses`): name, short code, address, phone, `IsPrimary`,
  `IsActive`. Unique `(TenantId, Code)`, codes uppercased on write. Full CQRS
  module + `api/v1/campuses`, gated by new `campuses.view`/`campuses.manage`
  (claims backfill confirmed in the log: "2 added").
- Rules the handlers enforce: the FIRST campus becomes primary automatically
  (an institution always has a head location); the primary campus cannot be
  closed; a closed campus cannot be promoted; promoting one steps the previous
  head down in the same transaction, so there is always exactly one.
- Campuses are closed, never deleted — students admitted there and fees
  collected there have to stay readable.
- Not module-gated: a multi-campus trust needs this on every plan.
- `Tenant.InstitutionType` (School=1 / College=2) with a picker in the school
  editor and a Type column in the Schools list. On `UpdateTenantCommand` the
  parameter is NULLABLE and only applied when present — a defaulted value
  would silently demote a college to a school whenever a client posted a body
  that predates the field.
- MIGRATION: `defaultValue: 1`, not the scaffolded `0` (not a member of the
  enum — the documented gotcha). It also backfills a "Main Campus" per
  existing tenant from that tenant's own address, because every school does
  operate somewhere and zero campuses would be wrong rather than unknown.
  The backfill INSERT must run BEFORE `EnableTenantRls("campuses")`: the
  policy is FORCEd, so it applies to the owner role the migration runs as,
  and the WITH CHECK would reject every row (`app.tenant_id` is unset there).
- Portal: Campuses page under Administration (loading / error / empty triad,
  add + edit inline, "Make head", show-closed toggle). Telugu nav key added.
- 5 integration tests, each on its OWN freshly-minted school — "the first
  campus becomes primary" is meaningless in a school that already has one,
  and xUnit gives no ordering guarantee inside a class.
- Browser-verified end to end as school admin (create Kompally Branch, move
  the head flag and move it back, duplicate code refused with
  "A campus with code 'MAIN' already exists.") and as Super Admin (Type
  column, School→College→School round-trip persisting).
  Suite: 65 unit + 163 integration.

## Platform hardening (feature/platform-hardening)

The most powerful account in the system was the least controlled: one shared
login, MFA optional, and its actions recorded but unreadable. Three fixes.

- **Operator log.** `GET /platform/audit` ([PlatformOnly]). CORRECTION to an
  earlier note: the events were never mis-stamped — the audit QUERY already
  handled a tenant-less caller. They became unreadable when the tenant guard
  started refusing platform accounts at `/audit`. Two changes beyond adding
  the endpoint:
  - The old platform default was EVERY school's rows at once. That firehose
    buries the handful of rows that hold an operator to account, so the
    default is now operator actions and a school is opt-in via `schoolId`.
  - "Operator action" means tenant-less AND `UserId != null`. Anonymous public
    traffic (the website enquiry form) also lands without a tenant and is not
    something an operator did — spotted in the live log, not in a test.
  - A school admin passing `schoolId` still gets only their own school.
- **MFA for operators.** Refusing the sign-in outright would brick an operator
  who has not enrolled — including the first one, who has nowhere to enrol
  from. So an un-enrolled platform account still signs in, its token carries
  `platform_mfa_setup_required`, and the `platform-only` POLICY refuses on that
  claim. They get the Security page and nothing else. Verified with real TOTP
  codes: before enrolling, /platform/*, /tenants and /billing all 403 while
  /auth/mfa and /auth/sessions stay 200; after enrolling, all 200.
- **Operator accounts.** `IPlatformOperatorService` — deliberately NOT part of
  the tenant-scoped user service, since everything here is `TenantId == null`
  and sharing that code is how an operator eventually appears in a school's
  staff list. Create (12-char minimum temp password), enable/disable with
  session revocation, password reset. Refuses to disable yourself, and refuses
  to disable the last active operator.
- Portal: `/platform/operators` and `/platform/audit` pages, plus a banner
  explaining the 403s to an un-enrolled operator.
- NAV CHANGE: platform accounts were shown Academics, Students, Fees and the
  rest, all of which now answer "Select a school first". Those items are
  `SchoolOnly` now, so an operator sees Dashboard, Schools, Operators and
  Operator log.
- GOTCHA: unknown Blazor component PARAMETERS are a runtime failure, not a
  build error — `AkPageHeader.Description` (the real one is `Subtitle`) built
  clean and blew up as an ErrorBoundary in the browser. Component APIs must be
  checked, not guessed.
- 9 integration tests (platform vs school audit scoping, the anonymous-row
  exclusion, MFA claim present/absent, operator create/duplicate/disable/
  last-operator). Suite: 61 unit + 157 integration green.
- `Jwt:RequirePlatformMfa` — TRUE by default (in `JwtOptions`), set FALSE in
  appsettings.Development.json only. Without that, the seeded demo operator
  could sign in but every platform screen 403'd, which made Schools,
  onboarding, branding and billing look like they had vanished. Same
  config-activation pattern as the dev SMS and payment gateways: production
  inherits the hard default. 4 unit tests pin it (default on, marked when on,
  unmarked when off, school accounts never marked).
- STILL OPEN: no scoped impersonation for support (an operator cannot see a
  school's data at all), and no cross-school platform overview.

## Login without a school code (feature/login-without-school-code)

- Nobody types a school code any more. Staff sign in with email/phone +
  password; parents with their mobile number. The school is inferred.
- It could NOT be a lookup: email and phone are unique only WITHIN a school
  (that is why `ApplicationUser.UserName` is an opaque key). So login gathers
  every account matching the identity, **authenticates first**, and decides
  after: one match signs in, several return a school picker.
  - Order is the security property. Asking "which school?" before the password
    is proven would turn the form into a directory of where an email has an
    account. A wrong password returns plain InvalidCredentials with an EMPTY
    school list — pinned by a test.
  - Candidates are capped at 5: a wrong password costs one hash per candidate.
- Parent OTP: one code issued per candidate school (same hash), throttled per
  PHONE rather than per (tenant, phone). The SMS drops the school name when
  there is more than one instead of guessing. On an ambiguous verify the codes
  are left UNCONSUMED, so choosing a school finishes the same sign-in without
  a second SMS.
- `AuthError.ChooseSchool` + `AuthResult.Schools`; the API answers 200 with
  `{chooseSchool, schools}` (like the MFA challenge — the credentials were
  right). `schoolCode` is now optional on all three auth payloads.
- The JWT carries a `school_code` claim, DISPLAY ONLY (the tenant GUID stays
  the authority), because the portal chrome needs to know which school's
  branding to fetch and nobody types it any more.
- REGRESSION ACCEPTED: the parent app's pre-login branding is gone — it was
  driven by the typed code. Branding applies after sign-in. One fewer field
  beat a logo on the login screen; revisit via subdomain if that is wrong.
- Password RESET still takes a school code: it is deliberately silent, so an
  ambiguous identity has no safe way to ask which school was meant.
- GOTCHA (caught in the browser): `Blazored.LocalStorage.SetItemAsync`
  JSON-ENCODES, so the school code was stored as `"DEMO01"` with quotes while
  MainLayout reads it with a raw `localStorage.getItem` — branding silently
  vanished. Use `SetItemAsStringAsync` for anything JS reads directly.
- 6 integration tests (no code for staff/platform/parent, two-school pickers
  for both password and OTP, wrong password leaks no school list).
  Suite: 61 unit + 149 integration green. E2E: portal and parent app both
  sign in with no code; branding follows; platform sign-in stays unbranded.
- NOT DONE: one parent session spanning children at DIFFERENT schools. The
  JWT carries one tenant and RLS binds one tenant per request, so that is a
  separate architectural piece, not a login tweak.

## ID card back (feature/id-card-back)

- The student ID card is now two CR80 pages — front then back — so a school
  prints duplex and cuts one card. `IdCard()` composes two
  `document.Page(...)` calls; the front is unchanged.
- The back answers the question nobody can ask a hurt child: **blood group**
  (set in the largest type on the card, because a hospital reads it), the
  emergency contact to call, then the school name, postal address and phone,
  and an "if found, please return" line. `Tenant.PostalCode` joined into
  `SchoolAddress` — a finder needs something postable — and `ContactPhone`
  added to the document data alongside `Student.BloodGroup`.
- DELIBERATELY OMITTED: the student's home address. Indian school cards often
  carry it, but a lost card would then hand a stranger a child's photo, name,
  home address and a parent's phone. The school's address serves the
  "return it" purpose without that. Say so if you want it added.
- The integration test now asserts PAGE COUNT: 2 for the ID card, 1 for the
  transfer and bonafide certificates, read from the PDF catalog's `/Count`
  (no PDF library needed; QuestPDF writes the page tree uncompressed).
  QuestPDF also throws on layout overflow, so a card that renders at all is a
  card whose content fits.
- Demo data: DEMO01 gained a street address, postal code and phone (without
  them the back and both certificate letterheads render half-empty), and
  students get rotated blood groups. Existing demo students are BACKFILLED —
  the cohort seeder skips students that already exist, so otherwise the card's
  headline field stayed a dash on every database seeded before today.
- GOTCHA: the in-app Browser pane will not render a PDF (no plugin) and
  `pdftoppm` is not installed, so PDFs cannot be eyeballed in-session. Verify
  structurally (page count, byte size, QuestPDF's own overflow exception) and
  send the file to the user to look at.

## Timetable breaks — recess and lunch (feature/timetable-breaks)

- An Indian school day is periods broken up by a recess and a lunch break, and
  a parent reading the timetable expects to see them. `TimetableSlotKind`
  (Lesson / Break / Lunch) on `TimetableEntry`, with `SubjectId` and `Period`
  now NULLABLE and an optional `Label` (migration AddTimetableBreaks; the enum
  column takes `defaultValue: 1` so existing rows read as Lesson — the same
  gotcha the report-card template column hit).
- **Breaks carry no period number, on purpose.** Numbering recess would
  renumber every lesson after it, and period-wise attendance already stores
  those numbers. Slots are therefore ordered by START TIME everywhere, not by
  period — that single change is what lets a break sit between P2 and P3.
- Validation per kind: a lesson needs a period and a subject; a break must
  have none of period/subject/teacher (a taught lunch is refused rather than
  silently stripped).
- New rule: two slots of one scope may not overlap on a day — the mistake it
  catches is lunch laid over a period. It lives in the HANDLER, after the
  teacher-clash check, not in the validator. As a validator rule it ran first
  and replaced "Ravi Teja is scheduled twice at overlapping times (day 2,
  periods 1 and 2)" with a generic overlap message — caught by an existing
  test. Specific messages win; this one only speaks when nothing better can.
- Teacher-facing queries are explicitly lesson-only (my-day, teacher schedule,
  substitution plan). They filtered on TeacherId already, so breaks could not
  leak in, but the filter is now stated rather than implied. The portal's
  roll-call period picker likewise offers lessons only — you cannot take
  attendance during lunch.
- Portal: "Add break" button, a Type select per row (Period / Recess / Lunch)
  that swaps subject+teacher for a name field, and a week view that
  interleaves break bands among the periods by the clock (`.ak-break-row`,
  shaded in both themes). Parent app: breaks render italic and greyed with no
  P-number, in clock order, en/te.
- Naming: a school's own label ("Tiffin break") is school-entered data and
  shows verbatim; with no label each app supplies its own translation, so a
  Telugu parent reads విరామం / భోజన విరామం. The demo deliberately leaves
  labels unset so that path is what a demo shows.
- DemoDataSeeder now builds a realistic day — P1, P2, recess 09:30–09:50,
  P3, P4, lunch 11:20–12:00, with Saturday a short two-period day and no
  breaks. It also CORRECTS its own older rows: demo databases seeded before
  breaks existed had periods running back to back, which the new recess would
  have overlapped.
- 2 integration tests (ordering with breaks interleaved and lesson numbering
  intact; taught/numbered/overlapping breaks all refused). E2E-verified in the
  portal week view, the roll-call picker (P1–P4 only) and the parent app in
  both languages.
- GOTCHA: two of the demo timetable rows for one slot looked like duplicates
  in psql but are soft-deleted (`is_deleted`), which EF filters out. Query
  demo data through EF or remember the flag before concluding the data is
  broken.
- Suite: 61 unit + 143 integration green.

## Tenant guard: no school bound, no school data (fix/tenant-guard)

- A platform (Super Admin) token carries no tenant, and the codebase handled
  that in two different wrong ways. Handlers reading `ITenantContext.TenantId`
  directly (15 files) threw inside an EF query → 500. Handlers relying on the
  EF query filter got its `Guid.Empty` fallback
  (`AppDbContext.CurrentTenantId`) → **200 with an empty result**, so a Super
  Admin was shown "0 students, ₹0 collected" as if the school were empty.
  The silent half was the dangerous one.
- `TenantGuardFilter` (global, registered before `ModuleGateFilter`) now
  refuses any tenant-scoped action with no school bound: 400 with
  "Select a school before using this feature." and a Detail that says what to
  do. Both failure modes collapse into that one answer.
- The rule is opt-OUT by design: skip when the endpoint is `[AllowAnonymous]`,
  `[PlatformOnly]` (which demands the opposite), `[NoTenantRequired]`, or
  needs no sign-in at all. Forgetting to opt out fails loudly the first time a
  platform account touches the endpoint; an opt-in scheme that is forgotten
  goes back to serving empty data in production — which is how the
  inconsistency arose.
- `AuthController` is `[NoTenantRequired]`: password change, MFA and device
  sessions belong to the ACCOUNT, and a Super Admin must keep them.
- FOUND WHILE AUDITING, also fixed: `PushController` had no `[Authorize]` at
  all (the doc comment claimed "any signed-in user"). `Register` happened to
  self-check and 401, but `Unregister` did not — an unauthenticated caller who
  knew an Expo token could delete it and silence that parent's notifications.
  Now `[Authorize]` on the controller and the delete is scoped to the caller's
  own tokens.
- 6 filter tests (`IntegrationTests/Api/TenantGuardTests.cs`, no database).
  Verified live: all four probe endpoints 400 for a platform account with the
  guidance body; Schools, sessions and MFA still 200; eighteen school-admin
  endpoints and the parent app's endpoints all unchanged; anonymous push
  delete now 401 and the owner's delete still 204; portal shows its existing
  platform-account fallback alert and the Schools page is unaffected.
- Suite: 61 unit + 141 integration green.
- GOTCHA (cost time): one integration run reported 6 failures and an inflated
  total (138 for 135 tests) right after the dev API had been hammering Docker;
  it took 14 minutes instead of the usual 7. A clean re-run of the identical
  commit was 135/135. Testcontainers fixtures that lose the race to start a
  container surface as per-class error rows, so a total that does not match
  the test count is the tell — re-run before believing the failures. And do
  not pipe `dotnet test` through `tail`: it discards the failure names, which
  is what made this cost a second 7-minute run.

## Inventory module + honest Enterprise preset (feature/inventory)

- Closes the second sold-but-unbuilt module. Two RLS tables (migration
  20260809*_AddInventory): `inventory_items` (name unique per tenant,
  category, unit, reorder level, unit cost, retire flag, running
  `QuantityOnHand`) and the append-only `stock_movements` register
  (Receipt / Issue / WriteOff / Adjustment, quantity always positive,
  `BalanceAfter` stamped on every line).
- The kind decides direction; an Adjustment SETS the balance (physical
  count) rather than adding to it. Issues and write-offs are refused when
  they would take stock negative, naming what is actually left, and the
  refused movement leaves the balance untouched. Retired items take no
  new movements.
- Permissions inventory.view/manage, [RequiresModule(Inventory)] gate,
  portal page `/inventory` (catalogue + register + low-stock filter),
  fully en/te.
- **HumanResources is no longer bundled in the Enterprise preset.**
  Payroll (PF, ESI, TDS, gratuity) is its own product and nothing
  implements the flag; shipping it in a preset was selling an empty
  module. The enum member stays so existing hand-tuned schools keep
  their flag, but no plan grants it.
- 3 integration tests: balance arithmetic across all four movement kinds,
  the negative-stock and duplicate-name refusals, and the low-stock view
  including retirement. E2E on DEMO01: received 120, issued 18, balance
  102, an attempted issue of 500 refused with "Only 102 piece … in stock".
  Suite: 50 unit + 124 integration green.

## Front office module (feature/front-office)

- Closes the first of three "sold but unbuilt" modules: FrontOffice ships in
  the Premium preset and previously had no code behind the flag.
- Two RLS tables (migration 20260809095033_AddFrontOffice):
  `visitor_entries` (gate register — name, phone, purpose, whom to meet,
  optional student, badge `V-yyyyMMdd-nnn` restarting daily, check-in/out)
  and `gate_passes` (early release — `GP-yyyy-nnnn` sequential per year,
  reason, released-to name + phone, approver, returned-at).
- Issuing a gate pass queues a guardian notification through the normal
  outbox, so it inherited WhatsApp routing and push for free — verified
  in the dev log firing on both channels at once.
- New permissions frontoffice.view/manage (DevSeeder backfills SchoolAdmin
  at startup; re-login needed for the JWT to carry them),
  [RequiresModule(FrontOffice)] gate, portal page `/front-office` with
  Visitors and Gate passes tabs, fully en/te.
- Check-out and mark-returned are idempotent: a double click keeps the
  first recorded time rather than moving it.
- GOTCHA (browser-caught): the guardian message printed `now.UtcDateTime`,
  so an Indian parent read 10:02 for a 15:32 release. It now converts via
  the tenant's `TimeZoneId` with an IST fallback. Any future parent-facing
  timestamp needs the same treatment — UTC is never what a parent reads.
- GOTCHA: the page asked for students with pageSize=200 but GetStudents
  caps at 100 → 400, and a single try/catch around all three loads let
  that blank the whole register. Loads are now independent.
- 3 integration tests (badge + idempotent checkout, sequential passes +
  guardian alert + class resolution, unknown student/blank name rejected).
  Suite: 50 unit + 121 integration green.
- DEMO01 has the module switched on so the page is demoable.

## Report card templates (feature/report-card-templates)

- Four per-school settings on Tenant (migration
  20260809091105_AddReportCardSettings): `ReportCardTemplate`
  (MarksOnly / MarksAndGrades / GradesOnly), `ReportCardShowAttendance`,
  `ReportCardShowRemarks` and `ReportCardSignatories` (CSV, max 4).
  GradesOnly never prints raw marks — including the totals block, which
  would otherwise leak what the template hides; it shows attendance there
  instead. Absent students show "AB" in the grade column.
- `GET/PUT exams/report-card-settings` (view / manage permission). Edited
  from the Exams page → "Report card layout"; applies to staff proofs and
  the copies parents download alike. Blank signature lines fall back to
  class teacher / principal / guardian rather than printing an empty row.
- ShowAttendance counts daily roll-call for the year and is only queried
  when the school has the toggle on.
- GOTCHA: `AddColumn<int>` for an enum defaults to 0, which is NOT a member
  of ReportCardTemplate — existing schools rendered an unknown layout and
  the portal select showed a bare "0". The migration now backfills
  `defaultValue: 2` (MarksAndGrades). Any future enum column needs the same
  treatment: the C# property initialiser does not touch existing rows.
- The portal reuses the DOMAIN enum (already in _Imports); defining a
  mirror copy in Models made every reference ambiguous.
- Integration test drives all three templates through a real render plus
  the blank-signatories fallback and the >4 validation. E2E: the four
  setting combinations produced four distinct PDF byte lengths.
  Suite: 50 unit + 118 integration green.

## Teacher dashboard — "My day" (feature/teacher-dashboard)

- `GET teachers/me/day` → `GetMyTeacherDayQuery`. Resolves the CALLER's
  teacher record via `Teacher.UserId == currentUser.UserId`; there is no
  teacherId parameter, so one teacher cannot request another's day. Carries
  no `[HasPermission]` (it reads only the caller's own slots) and 404s when
  the account has no linked teacher record — the portal renders that as a
  friendly "ask your admin for a teacher login" note.
- Shows today's periods (subject, class/section, time, attendance chip),
  sections still awaiting roll-call, students taught, pending leave from
  their own students, exam papers with no marks entered, and homework due
  within 7 days. Substitutions overlay the base timetable: periods they
  cover are added and flagged "Cover"; periods covered FOR them drop off.
- Portal page `/my-day`, nav item for non-platform sign-ins, fully en/te.
- Class-wide slots (`SectionId == null`) fan out to every section of the
  class for both the attendance chase and the student roll.
- GOTCHA (caught in the browser, not the tests): name lookups and the
  student roll were first derived from TODAY's slots only, so homework for
  a class the teacher meets on other days rendered "—" and "students you
  teach" read 0 on a day with no periods. Lookups now cover the school and
  the roll covers the teacher's whole week; only the attendance chase is
  scoped to today.
- 4 integration tests on a fixture with two teachers, a pinned Wednesday
  clock (FixedClock) and a SwitchableCurrentUser: ownership isolation,
  attendance transition, substitution hand-over, and 404 for a non-teacher.
  The substitution test deletes its own rows in a finally block — shared
  fixtures make test order matter otherwise. Suite: 50 unit + 117 integration.

## WhatsApp notifications (feature/whatsapp-notifications)

- `IWhatsAppSender` mirrors `ISmsSender`; `MetaWhatsAppSender` talks to the
  Meta Cloud API (graph.facebook.com/v21.0/{phoneNumberId}/messages) and goes
  live on `WhatsApp:Provider=meta`. Default `dev` logs `[DEV WHATSAPP]`.
  Business-initiated messages need an approved template, so set
  `WhatsApp:TemplateName` (one body variable) — leaving it empty sends free
  text, which only works inside the 24-hour service window.
- `Tenant.WhatsAppEnabled` (migration 20260809082915_AddTenantWhatsAppEnabled)
  is the per-school switch, editable in the tenant editor under Package &
  modules. Off by default: WhatsApp conversation pricing is separate from
  SMS credits and each school opts in.
- OutboxProcessor routes `sms` rows for WhatsApp-enabled tenants to WhatsApp
  first — no SMS credit spent. A WhatsApp failure logs and FALLS BACK to SMS
  in the same pass (parent still notified, that one message metered). One
  tenant-flag lookup per batch, not per message. Every existing producer
  (absence alerts, fee reminders, receipts, notices) inherits this for free.
- Auth OTP still goes direct through `ISmsSender`, deliberately: sign-in
  must not depend on a channel a school can toggle. Dev OTP stays `[DEV SMS]`.
- 3 integration tests (WhatsApp-first no credit spent, outage → SMS fallback
  with credit spent, SMS-only school untouched). E2E on DEMO01: flag toggled
  in the portal, queued outbox row delivered as `[DEV WHATSAPP]`, credits
  unchanged. Suite: 50 unit + 113 integration green.
- GOTCHA: fixture-shared credit assertions must be DELTAS — the first draft
  asserted an absolute 10 and failed depending on test order.

## Fee installments (feature/fee-installments)

- FeeStructureItem gained a nullable `Label` (max 50, "Term 1") —
  migration 20260809080941_AddFeeInstallmentLabels. The unique index
  (TenantId, YearId, ClassId, HeadId, DueDate) already allowed many
  lines per head, so installments were latent; labels make them real.
- Define handler trims labels (blank → null); GetFeeStructure and
  GetStudentFeeSummary surface them, so the label reaches the portal
  student profile ("Tuition — Term 1"), and the parent app for free
  (parent children/{id}/fees reuses GetStudentFeeSummaryQuery;
  types.ts FeeDueLine.label + FeesCard render it).
- Fees.razor plan editor: per-row Installment textbox + a split
  helper (head, total, count 2–12, first due, months apart 1–6) that
  appends "Term k" rows — equal amounts, rupee remainder on the
  first term, dates spaced by the chosen months.
- GOTCHA: the plan-row `@foreach` needed `@key="row"` — without it,
  deleting a middle row left MudDatePicker instances bound to their
  old positions and due dates silently shifted rows. Caught in E2E,
  verified fixed in the browser.
- Integration test: labeled 3-term plan → structure and student
  summary both carry ["Term 1", "Term 2", null] (blank normalizes),
  per-line dues intact. Suite: 50 unit + 110 integration green.

## Portal localization (en/te) + public enquiry form

- Portal i18n mirrors the mobile apps: flat key → text dictionaries in
  `SchoolErp.Shared.Localization.PortalStrings` (Shared so the
  coverage unit test doesn't reference the WASM project), a scoped
  `LocalizationService` (localStorage "akshara.lang", instant apply),
  an EN/తెలుగు toggle in the appbar AND on the login page. LOCALIZED
  SO FAR: chrome + all nav, Login, Home dashboard, Students (incl.
  the import panel). Remaining pages stay English until keys are
  added — the coverage test forces te to match en exactly.
  PATTERN for new pages: add keys to both dictionaries, `@inject
  LocalizationService L`, use `@L["key"]`, and subscribe
  `L.Changed += ...InvokeAsync(StateHasChanged)` (the layout
  re-rendering does NOT re-render @Body pages — each localized page
  must subscribe itself; this bit us in verification).
- Public website enquiry: POST admissions/enquiries/public —
  anonymous, "auth" rate limit, body carries schoolCode (resolved
  like the OTP flow via a pinned scope). Lands in the CRM as
  New/Website; an open enquiry with the same phone is silently
  deduplicated (public forms get resubmitted); unknown school codes
  return the same 202 as success so the endpoint can't probe codes.
  Integration-tested + verified live (Meghana Rao on the board).

## Conventions (follow these when adding modules)

- Every business entity extends `TenantEntity`; every migration creating a
  tenant table MUST call `migrationBuilder.EnableTenantRls("table")` (and
  `DisableTenantRls` in Down). Platform-scoped exceptions (documented in each
  entity): `outbox_messages`, `payment_orders`, `refresh_tokens`, `otp_codes`,
  `invoices`, `invoice_lines` (platform billing — the school is the subject,
  the operator is the audience; every endpoint demands tenants.manage).
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
- A migration cannot INSERT into an RLS'd table after `EnableTenantRls`:
  the policy is FORCEd, so it binds the owner role too, and `app.tenant_id`
  is unset during migrations. Backfill first, enable RLS last.
- Integration tests in one class share a fixture AND have no guaranteed
  order. Anything that asserts on "the first X" must mint its own tenant.
- A cross-tenant aggregate over an RLS'd table returns ZERO ROWS, not an
  error. Nothing fails; the number is just quietly wrong. Platform-wide counts
  must go through `app_platform_tenant_counts()`.
- Same trap, worse: a migration that UPDATEs an RLS'd table matches NO rows,
  because `app_current_tenant_id()` is NULL there and the policy's USING
  clause hides everything from the owner. Wrap data backfills in
  `NO FORCE ROW LEVEL SECURITY` … `FORCE ROW LEVEL SECURITY`.
- MudBlazor popovers render on Blazor's async loop: reading `.mud-list-item`
  in the same JS tick as the click that opens the select returns an empty
  list. Read in a second call, or you will "confirm" a bug that isn't there.
- MudChart bar charts pick their own y-axis: a max of 2 gets drawn against
  0–20 and reads as "nothing happened". Pass ChartOptions for small integers.
- A rule can live in a UNIQUE INDEX as easily as in a validator. Relaxing
  "one enrollment per student per year" in the validator only moved the
  failure to a 23505 at SaveChanges. Grep the EF configuration before
  concluding a constraint is gone.
- PDFs CANNOT be visually checked from this environment. Three routes all
  fail: `Read` needs poppler (not installed), the Browser pane downloads a
  PDF rather than displaying it (and pops a save dialog at the user), and
  embedding a blob in a Blazor page trips its ErrorBoundary. Verify PDF bytes
  and the HTTP path in code; the rendered page needs a human.
- `dotnet test -v q` prints the Failed! total but NOT the failing test names.
  Re-run without `-v q` (or grep `^  Failed `) — this has now cost two
  separate sessions a wasted full run.
- Windows PowerShell 5.1 (this box has no `pwsh`) reads a BOM-less file as
  CP1252, so a UTF-8 em dash in a `.ps1` arrives as three characters ending in a
  curly quote — which 5.1 treats as a STRING DELIMITER. The parser desynchronises
  and blames innocent lines further down. Keep `.ps1` files ASCII-only.
- NEVER clean up Testcontainers leftovers with a filter on the image alone:
  `docker ps -aq --filter ancestor=postgres:16-alpine` also matches the
  long-running `schoolerp-postgres` dev container, and `rm -f` will take it out.
  (Recovered intact — the data is in the named volume `deployment_postgres-data`,
  so `docker compose up -d` restores it — but nothing about that was deliberate.)
  Testcontainers runs a ryuk reaper that cleans its own containers; leave it alone.
- VSTest writes each coverage attachment TWICE (the GUID run folder and the trx
  staging folder `In\<MACHINE>\`), so anything globbing for `coverage.cobertura.xml`
  sees double. `scripts/coverage.ps1` filters the staging copies out.

## Test coverage — FIXED and measured (10 Aug 2026)

Run it yourself: `powershell -ExecutionPolicy Bypass -File scripts/coverage.ps1`
(CI runs the same file as `pwsh scripts/coverage.ps1`; ~4 min, needs Docker).

**Backend: 5,641 / 6,181 lines = 91.3%**, branches 68.4%. Per assembly:

| Assembly | Line rate |
|---|---|
| SchoolErp.Api | 62.0% (186/300) |
| SchoolErp.Application | 92.0% |
| SchoolErp.Domain | **100.0%** (77/77) |
| SchoolErp.Infrastructure | 91.8% |
| SchoolErp.Shared | 99.5% |

Api is 62% and the rest is mostly controller CONSTRUCTORS — one line each, coverable
only by invoking every controller with valid data and permissions, which re-tests
handlers the integration suite already covers at 92%. Chasing it would buy a number,
not evidence. What was worth covering (the pipeline, the anonymous surface, the
account endpoints, checkout) is covered.

DENOMINATOR, so the number is never misquoted: the five `backend/src` projects,
excluding EF migrations and the model snapshot. The Blazor portal is NOT in it —
no test project references it and bUnit is not in the solution. This figure means
"backend", never "the product". That was the open question in the old note; it is
answered by exclusion, not by measurement, and the ~60 `.razor` files remain
untested.

### The old 0.6% / 7.2% figures were measurement artifacts, not coverage

The previous note blamed the collector `friendlyName` casing. That was wrong, and
so was the second suspect (whitespace in `ExcludeByFile`). Both were disproved by
running them: friendlyName matching is case-INSENSITIVE, and the exclusion list
worked fine as written.

The real fault: **the warning comment inside `coverlet.runsettings` contained a
doubled hyphen**, because it spelled out the `--settings` and `--collect`
switches. XML forbids `--` inside a comment. VSTest rejected the entire settings
file with `Settings file provided does not conform to required format`, printed
it as ONE line in the middle of ordinary test output, and carried on with no
collector — so the run went green and measured nothing. The note documenting the
breakage was itself the breakage. Command-line switches now live in
`scripts/coverage.ps1`, never in that file's comments.

With the file parsing, one run produces two cobertura files (unit: 5,881
coverable lines, no `SchoolErp.Api`; integration: 6,181 including it) and
ReportGenerator merges them. `UseSourceLink` is now off: it rewrote every path to
a `raw.githubusercontent` URL pinned to the last COMMITTED sha, which 404s on a
private repo and leaves the HTML report with no source.

CI publishes it: the backend job installs ReportGenerator, runs the same script,
writes the figure to the job summary and uploads the HTML report as an artifact.
The script FAILS the run if zero coverage files are produced, so the silent-green
failure mode cannot recur.

## API-layer HTTP tests (feature/coverage-and-api-tests)

`SchoolErp.Api` was the least-covered assembly at 20% with every controller at
0%, because all 185 integration tests call handlers directly and the two files in
`IntegrationTests/Api/` instantiate filters rather than issue requests. Nothing
exercised routing, JWT validation, the permission policies, `[PlatformOnly]`, the
module gate, the security headers or the health split as a wired pipeline. Adding
36 tests took that assembly to 48.3%. (It is not higher because most of the
remaining Api lines are controller actions that only delegate to a handler the
integration suite already covers; the pipeline itself is now covered.)

- `ApiFixture` boots the REAL API over a throwaway container via
  `WebApplicationFactory<Program>` (the `public partial class Program` hook at the
  bottom of Program.cs had been sitting there unused). Production shape: migrations
  and Hangfire on the OWNER connection, the API itself on a restricted role, so RLS
  is not silently inert.
- Files: `AuthorizationPipelineTests` (401 vs 403, permission policies, platform
  policy, tenant guard), `PipelineContractTests` (security headers, problem-body
  shapes, 201 + a Location that is FOLLOWED, 409 conflict, routing, health, CORS),
  `SubscriptionAndFamilyBoundaryTests` (module gate, family guard 404-not-403),
  `AuthEndpointTests` (real login round trip, rate limiter), `PublicSurfaceTests`
  (the anonymous internet-facing surface: unsigned and forged payment webhooks,
  file-key traversal, root service info, Swagger absent outside Development).
- The fixture seeds a year and a class through the REAL commands so admission has
  somewhere valid to place a student — which is what makes the 201/409 wire tests
  possible. `ApiCollectionDefinition` is named that way, not `ApiCollection`,
  because CA1711 refuses a type name ending in "Collection" (full-rebuild-only
  warning — it will not appear on an incremental build).
- **GOTCHA that cost the most time, and would have been expensive to miss:**
  `AddInfrastructure` reads `ConnectionStrings:Postgres` EAGERLY while services are
  being registered, and `WebApplicationFactory` applies `ConfigureAppConfiguration`
  LATER than that. The in-memory override was therefore ignored and the test host
  quietly ran against the real dev database on localhost:5432 (`tenants visible =
  [COLL01, DEMO01, GRWD01]`). Only reads happened, but writes were one test away.
  Eagerly-read config MUST arrive as environment variables (`ConnectionStrings__Postgres`),
  which `CreateBuilder` adds after the appsettings files so they outrank them.
  Hangfire was unaffected because its lambda runs lazily — that contrast is the tell.
  The fixture now asserts the DbContext's ACTUAL connection string before seeding;
  the first version of that guard checked configuration, passed, and proved nothing.
- Tokens are MINTED via `JwtTokenService` rather than obtained by logging in: the
  credential endpoints allow 10 requests a minute, and signing six principals in
  would spend most of that budget before the first test ran. `AuthEndpointTests`
  covers the real login round trip on its own host.
- The `TestPrincipal` enum has a `SchoolAdminWithPlatformPermission` deliberately
  carrying `tenants.view`/`tenants.manage`. It exists to prove the platform
  endpoints refuse school tokens on the POLICY, not the permission — the exact
  cross-tenant escalation this product shipped once.

### SECOND SECURITY FIX: `leave/mine` required no sign-in at all

Found by `EndpointExposureTests`, which audits the whole routing table rather
than endpoints someone thought to test. `GET/POST leave/mine` carried NEITHER
`[HasPermission]` nor `[Authorize]` — and this API has no fallback authorization
policy, so an action with no attribute is served to anybody. The XML doc said
"any signed-in staff"; the attribute enforcing it was never written.

Measured before fixing: `GET /api/v1/leave/mine` answered **200 OK** to an
anonymous caller. It returned `[]` only because the handler had no user to match
on — the boundary was an accident of the query, not a control, and one change to
that filter turns it into a leak. `POST` reached the handler too (404 "User
'(anonymous)' was not found"). Fixed with `[Authorize]` on both.

The audit is the durable part: it enumerates endpoints from the RUNNING host, so
a controller added next year is covered the day it is mapped. It asserts three
things — every endpoint states an intention (authorize or explicit
`[AllowAnonymous]`), the anonymous surface matches an expected list exactly, and
every protected endpoint really does challenge an anonymous request.

### SECURITY FIX found by those tests: the credential rate limit was inert

`[EnableRateLimiting("auth")]` (10/min, brute-force defence) was being shadowed by
`app.MapControllers().RequireRateLimiting("global")` (300/min). `RequireRateLimiting`
stamps its policy onto every controller endpoint AFTER the controller's own
attribute, and the later metadata wins — so password login was limited at 300 a
minute, 30x the intended budget. Measured, not inferred: `/auth/otp/request`
returned its first 429 at request #300, not #10.

Fixed by making the baseline a `GlobalLimiter` (applied IN ADDITION to whatever
policy an endpoint names) and dropping `RequireRateLimiting` from `MapControllers`,
so the two compose instead of overwriting. Identity lockout was always the primary
defence, so this was defence-in-depth that wasn't there. Guarded by
`Credential_stuffing_runs_out_of_budget_before_it_runs_out_of_guesses`.
