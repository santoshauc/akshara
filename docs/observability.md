# Observability

The API ships with OpenTelemetry wired but **dormant**. Set one configuration
value and traces + metrics flow to any OTLP collector; unset, nothing is
exported and there is zero overhead beyond the disabled no-op providers.

## Turning it on

```jsonc
// appsettings.Production.json (or the environment)
{
  "Otlp": {
    "Endpoint": "http://otel-collector:4317" // gRPC OTLP
  }
}
```

Environment-variable form: `Otlp__Endpoint=http://otel-collector:4317`.

What gets exported once enabled (see `Program.cs`):

| Signal  | Source | Notes |
|---|---|---|
| Traces  | ASP.NET Core | one span per request; `/health/*` filtered out |
| Traces  | HttpClient | outbound calls: Razorpay, MSG91, Expo push |
| Traces  | `Npgsql` ActivitySource | every SQL command with statement text |
| Metrics | ASP.NET Core | request duration/count histograms |
| Metrics | HttpClient | outbound latency/count |

Resource attributes: `service.name=schoolerp-api`,
`service.version=<assembly version>`.

## Local stack in two commands

```bash
docker run -d --name otel-lgtm -p 3000:3000 -p 4317:4317 -p 4318:4318 \
  grafana/otel-lgtm
```

Then run the API with `Otlp__Endpoint=http://localhost:4317` and open
Grafana at http://localhost:3000 (anonymous admin in this image):

- **Traces**: Explore → Tempo → search `service.name = schoolerp-api`.
- **Metrics**: Explore → Prometheus → `http_server_request_duration_seconds_bucket`.

## Dashboards worth building first

1. **API health**: p50/p95/p99 of `http.server.request.duration` split by
   route; 4xx/5xx rate. Alert: p95 > 1s for 5 min, or 5xx ratio > 2%.
2. **Database**: span duration by `db.statement` (Npgsql source). Alert on
   any statement p95 > 500 ms — the usual first sign of a missing index.
3. **Outbound**: HttpClient spans by host (razorpay.com, msg91.com,
   exp.host). Alert on error ratio > 10% per host — a gateway outage shows
   here before parents phone the school.
4. **Jobs**: Hangfire's dashboard (`/jobs`, dev only) covers recurring-job
   health; failed `outbox-dispatch` or `fee-due-reminders` runs also
   surface as error logs (Serilog) — ship those to Loki alongside.

## Logs

Structured logs are Serilog (console by default). In the LGTM stack, add
`Serilog.Sinks.OpenTelemetry` or scrape container stdout with Promtail —
either way, correlate on `TraceId` which Serilog enriches automatically
when a span is active.

## Things deliberately NOT exported

- No SQL parameter values leave the process (Npgsql redacts by default).
- No request/response bodies, no JWTs, no personal data — spans carry
  routes and timings only.
- Health-check requests are excluded from tracing to keep noise down.
