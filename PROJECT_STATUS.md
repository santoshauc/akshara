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

Remote: https://github.com/vivian-richard/akshara (private). CI runs on push
once the GitHub account clears the Actions hold (see below).

Test suite: 61 unit + 129 integration = **190 green** (`dotnet test` from `school-erp/`).
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

NOT DOING: dashboard redesign (user asked to leave Home.razor as-is);
HR/payroll (own product).

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
- PRE-EXISTING, not introduced here: every FluentValidation failure logs an
  `ERR ... responded 500` with a stack trace in Serilog while the client
  correctly receives 400 — the exception handler sits outside
  `UseSerilogRequestLogging`. Verified identical on an untouched endpoint.

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
