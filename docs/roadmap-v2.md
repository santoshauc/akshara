# SchoolErp — Roadmap v2 (gap closure)

The original spec is delivered (see PROJECT_STATUS.md). This roadmap closes
the gaps between "spec complete" and "a real school can run on this".
Work top-to-bottom; every item ships with integration tests, portal/app UI
where relevant, browser verification, and a commit. Update the Status column
as items land.

## Phase A — go-live blockers

| # | Item | Acceptance criteria | Status |
|---|---|---|---|
| A1 | Users & roles administration | Staff users CRUD (create with temp password, edit, deactivate blocks login, admin reset-password); roles CRUD as permission bundles with a permission-picker; self-service change-password; forgot-password via phone OTP (reuses OTP infra, silent on unknown login); portal "Users & roles" page; everything audited. | DONE |
| A2 | Production SMS adapter | MSG91-style adapter behind ISmsSender, config-activated like Razorpay (`Sms:Provider`), with DLT template-id support; DevSmsSender remains the fallback; unit tests over a fake HTTP handler. | DONE |
| A3 | In-app fee payment | Parent app "Pay now": parent-scoped order endpoint → Razorpay Checkout (web) / payment link (device); dev gateway simulates capture end-to-end; balance refreshes after webhook. | DONE |
| A4 | File storage + student photos | IFileStorage abstraction + local-disk implementation (path-safe, size/type-limited); student photo upload in portal; photos served and shown in portal + parent app. | DONE |
| A5 | Plan enforcement | EnabledModules gates module endpoints (403 with a clear message); SmsCredits decremented by the outbox dispatcher and sends blocked at 0 (logged); expired subscription blocks tenant login (423-style message). Super Admin editor already manages the fields. | DONE |

## Phase B — real school operations

| # | Item | Acceptance criteria | Status |
|---|---|---|---|
| B1 | Year-end promotion | Bulk promote a class's active enrollments into the next year/class (per-student opt-out list); creates new enrollments, marks old ones Promoted; portal action on Academics. | DONE |
| B2 | Teacher logins | "Teacher" seeded role (attendance.mark, exams.enter-marks, homework.manage, timetable.view, students.view); one-click "create login" from the Teachers page linking Teacher→user; teacher can sign in and do those tasks. | DONE |
| B3 | Leave management | Parent submits a student leave request from the app; staff approve/reject in portal (approval marks attendance Leave for the range); staff leave requests with the same flow. | DONE |
| B4 | School documents | Transfer Certificate + bonafide certificate + student ID card as PDFs (extend the QuestPDF renderer); buttons on the student profile. | DONE |
| B5 | Fee polish | Late fine rule per fee head (flat/percent after due date); per-student concessions; receipt PDF download; nightly Hangfire fee-due reminder SMS. | DONE |
| B6 | Push notifications | Expo push tokens registered per device; outbox gains a "push" type; absence/result/fee/trip events push as well as SMS; apps register tokens on login. | DONE |
| B7 | Term-aggregated report cards | Final report card combining selected exams (weighted), co-scholastic grades + teacher remarks entry, rendered on the existing PDF. | DONE |
| B8 | Parent–teacher messaging | Per-student conversation thread (parent app ↔ portal), unread counts, audit-safe. | DONE |

## Phase C — maturity

| # | Item | Acceptance criteria | Status |
|---|---|---|---|
| C1 | Portal dashboard | Real tiles: attendance today, fees collected this month, overdue loans, upcoming exams; per-school scoping. | DONE |
| C2 | Period-wise attendance | Optional per-period marking wired to the timetable; daily view stays the default. | DONE |
| C3 | DPDP workflows | Per-student data export (JSON), erasure request flow with audit trail. | DONE |
| C4 | Timetable substitutions | Mark a teacher absent for a day → suggest free teachers per clashing slot → publish substitutions. | DONE |
| C5 | Observability | OTLP exporter wired by config; dashboards runbook. | DONE |

## Standing rules (unchanged)

Module pattern, RLS on every tenant table, permission constants + startup
claims backfill, outbox for side effects, zero-warning builds, tests before
UI, browser-verify everything, update PROJECT_STATUS.md + this file's Status
column, one commit per item.
