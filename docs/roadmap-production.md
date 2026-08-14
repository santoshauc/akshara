# From working software to a shipped product

The codebase is architecturally sound: clean architecture + CQRS, RLS with
`FORCE` behind EF query filters, a transactional outbox, MFA on platform
accounts, an audit trail, 273 tests including Testcontainers integration runs,
and zero-warning builds.

It is also, operationally, a proof of concept. It has never been deployed, CI
has never executed once, and `appsettings.json` carries working credentials.
Those are different problems from the feature backlog and they gate everything
else, so they come first.

This document is the production plan. The feature backlog stays in
`PROJECT_STATUS.md`.

---

## Phase 0 — make it shippable at all

Nothing below this line matters until these are done. None of it is
interesting work; all of it is the difference between a demo and a product.

1. **Git remote + CI actually running.** `.github/workflows/ci.yml` exists and
   has never executed. 126 commits sit on a local master. Until CI runs, "273
   green" is a claim about one machine.
2. **Secrets out of source control.** `appsettings.json` contains the Postgres
   password, and the JWT signing key, Razorpay keys and S3 credentials are all
   configured the same way. Production needs environment variables or a secret
   store, and the dev values need to stop being valid anywhere real.
3. **A deployment target.** `deployment/Dockerfile.api` builds and the compose
   file runs locally. There is no staging environment, no managed Postgres, no
   TLS termination, no domain.
4. **Backups and restore.** There is no backup, and — more to the point — no
   rehearsed restore. An untested backup is not a backup.
5. **Migrations in the deploy pipeline.** They are applied by hand today, with
   a separate owner-role connection string. That needs to be a deliberate,
   logged step in the release, not something a developer remembers.

## Phase 1 — gaps that block real use

Functional holes that a paying customer hits in the first month.

1. ~~**Email.**~~ DONE — the outbox now carries a fourth channel. `IEmailSender`
   behind `Email:Provider=smtp` (dev logs otherwise), an `email` outbox type,
   and `NotificationQueue` queueing one email per guardian who has an address
   on file, rendered from the SAME localized template as the SMS. Not
   SMS-credit metered — an email costs the school's mail provider, not a credit
   this platform sells. STILL OPEN: attaching documents. Emailing a fee receipt
   or report-card PDF needs the payload to reference a document the dispatcher
   renders at send time; the renderers exist, the plumbing does not.
2. **Arrear / supplementary exams.** A backlog cannot be recorded or cleared.
   Every Indian college needs this every cycle.
3. **Per-student electives.** Subjects attach to a cohort, so everyone sits
   the same papers. CBCS is "choice based"; this undercuts the model.
4. **Student logins.** Roles are SuperAdmin/SchoolAdmin/Teacher and the app
   authenticates guardians by phone. College students are adults.
5. **Fee refunds** for mid-year withdrawals. (Currently parked by request.)
6. ~~**Rate limiting on auth.**~~ DONE — the `auth` policy existed but was
   inert: `RequireRateLimiting("global")` on `MapControllers` stamped its policy
   onto every controller endpoint AFTER the controller's own attribute, and the
   later metadata won. Credential endpoints were limited at 300/min instead of
   10. The baseline is now a global limiter, which composes with per-endpoint
   policies instead of overwriting them. Guarded by `AuthEndpointTests`.

## Phase 2 — finish the product surface

The app currently looks finished in places and unfinished in others, which
reads worse than being uniformly plain.

1. **Ten list pages onto the Students pattern** — Teachers, Fees, Transport,
   Inventory, Front office, Library, Hostel, Users, Audit, Admissions.
2. **Forms + record-detail pattern** (header + tabs) for Student, Teacher,
   Route, Tenant.
3. **Localization coverage.** `PortalStrings` covers navigation, login and the
   dashboard. Most pages — including everything built recently — are
   hardcoded English. The product claims to be bilingual; today the apps are
   and the portal is not.
4. **Parent app** bottom-tab navigation and live tracking.
5. **Driver app** drop-off, exceptions, emergency and offline queue.

## Phase 3 — hardening

1. **Accessibility + responsive sweep**, including whether the input
   focus-ring conflict extends to selects, checkboxes and date pickers.
2. **Load testing.** RLS sets a session GUC per request and the platform
   dashboard aggregates across every tenant. Neither has been measured.
3. **Observability that someone watches.** OpenTelemetry is wired to an OTLP
   exporter; there are no dashboards, no alerts and no error tracking.
4. **Security review.** No penetration test. The multi-tenant boundary is the
   thing to attack: RLS, the `SECURITY DEFINER` counts function, and the
   platform endpoints.
5. **DPDP compliance beyond the export button.** Consent records and retention
   policies, not just subject access and erasure.
6. **Mobile store releases.** Neither Expo app has ever been built for a store.

---

## What is deliberately not being built

- School dashboard redesign (`Home.razor` stays as it is, by request).
- HR/payroll — its own product.

## How to judge progress

Phase 0 is binary: either CI runs against a remote and a staging environment
serves traffic, or the product cannot ship. Everything after that is
incremental and can be sequenced against whichever customer is closest.
