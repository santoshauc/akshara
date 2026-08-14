# Security Notes — Dependency Advisories

## Open advisories (accepted, tracked)

### ~~AutoMapper 13.0.1 — GHSA-rvv3-g6hj-g44x (High)~~ RESOLVED

- **Resolution:** AutoMapper was removed entirely. Mappings are hand-written
  (`*Mappings` static classes: EF-translatable expressions for query
  projection plus `ToDto()` extensions for in-memory maps). No replacement
  dependency was introduced.

### ~~SSH.NET 2023.0.0 — GHSA-q939-rpr3-3284 (High)~~ RESOLVED

- **What it is:** a malicious SSH *server* can write arbitrary files on the
  client during a recursive `ScpClient` download, via server-controlled
  filenames. Vulnerable through 2025.1.0; first patched in 2026.0.0.
- **How it got in:** transitively, via `Testcontainers` 3.10.0, which uses
  SSH.NET for its port-forwarding feature. TEST-ONLY — no `backend/src` project
  references it, so it has never shipped, and nothing here performs SCP at all.
- **Resolution:** direct `PackageReference` to 2026.0.0 in
  `SchoolErp.IntegrationTests`, lifting the transitive floor. Pinned forward
  rather than added to the accepted list, because the CI gate does not reason
  about blast radius and should not have to. Verified by running the full
  integration suite against the newer package (240 tests, all green) — a
  three-year version jump on a library Testcontainers calls at runtime is worth
  proving rather than assuming.
- **When to remove:** once Testcontainers' own floor moves past 2026.0.0.
- **Caught by:** the CI `vulnscan` job, on a commit that was green hours earlier
  — the advisory was published between the two runs.

### ~~Newtonsoft.Json 11.0.1 — GHSA-5crp-9r3c-p9vr (High)~~ RESOLVED

- **How it got in:** transitively, and invisibly. `Hangfire.Core` declares
  `Newtonsoft.Json >= 11.0.1`; NuGet resolves the FLOOR of an open range, so
  the build quietly took 11.0.1 — a version where a crafted payload can
  exhaust the stack. Nothing in this codebase calls Newtonsoft directly.
- **Resolution:** a direct `PackageReference` to 13.0.3 in
  `SchoolErp.Api` and `SchoolErp.Infrastructure`, which raises the resolved
  version above the floor. Both projects need their own reference — a
  transitive version is resolved per project, so pinning one does not lift
  the other. Newtonsoft.Json is MIT, so this adds no licensing obligation.
- **When to remove:** once Hangfire's own floor moves past 13.0.x.
- **Caught by:** the CI `vulnscan` job, on its first-ever real execution.

### OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.2 — GHSA-4625-4j76-fww9 (Moderate)

- **Status:** Monitoring for a patched 8.0-compatible release.
- **Exposure assessment:** The OTLP exporter only sends telemetry to a
  collector endpoint we configure; it does not process untrusted input.

## Licensing notes

### QuestPDF (Community license)

- Used for report-card PDF rendering. The Community tier is free for
  organisations under USD 1M annual gross revenue; the license type is set
  explicitly in `QuestPdfReportCardRenderer`. Revisit before the product's
  revenue crosses the threshold (Professional license or a swap).

### Hangfire 1.8.x (LGPL-3.0) + Hangfire.PostgreSql (MIT)

- Hangfire core is LGPL v3. SchoolErp is a hosted SaaS — the LGPL's
  distribution-triggered obligations do not apply to server-side use, and we
  do not modify Hangfire itself. Acceptable; revisit only if the product is
  ever shipped on-premises.

## Review cadence

Run `dotnet list package --vulnerable --include-transitive` in CI on every
build; fail the build on new High/Critical advisories not listed here.
