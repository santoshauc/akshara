# SchoolErp — instructions for AI sessions

Read `PROJECT_STATUS.md` first — it has the full state, credentials,
conventions, and gotchas. Resume from its "Remaining scope" list unless told
otherwise.

Hard rules for this repo:

- Stop the running dev servers before `dotnet build` (they lock DLLs).
- Every new tenant-scoped table gets `EnableTenantRls` in its migration.
- Every module ships with integration tests (Testcontainers) before its UI.
- Keep builds at zero warnings; don't bump MediatR/AutoMapper/FluentAssertions
  majors (licensing) — see `docs/security-notes.md`.
- Adding a permission constant? Backfill role claims for seeded/existing roles.
- Verify UI changes in the browser: portal at :5050, parent app (Expo web)
  at :8081, API at :5199 — launch configs live in `<workspace>/.claude/launch.json`.
