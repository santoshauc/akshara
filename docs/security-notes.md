# Security Notes — Dependency Advisories

## Open advisories (accepted, tracked)

### AutoMapper 13.0.1 — GHSA-rvv3-g6hj-g44x (High)

- **Status:** Accepted risk, tracked.
- **Why not fixed:** The patched release line is AutoMapper 15.x, which moved
  to commercial licensing. 13.0.1 is the newest MIT-licensed release.
- **Exposure assessment:** AutoMapper is used exclusively to map trusted,
  server-side domain entities to DTOs. No untrusted input reaches mapping
  configuration or expression compilation.
- **Planned remediation:** Replace AutoMapper with Mapster (MIT) or
  hand-written mapping extensions before GA. Mapping usage is confined to the
  Application layer, so the swap is mechanical.

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
