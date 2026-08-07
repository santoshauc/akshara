namespace SchoolErp.Infrastructure.Notifications;

/// <summary>
/// Hangfire entry point for outbox delivery. A thin wrapper so the job graph
/// stays serializable and the processor keeps its testable shape. Hangfire
/// creates a DI scope per execution; the scope carries no tenant — which is
/// exactly why the outbox table has no RLS (see <c>OutboxMessage</c> docs).
/// Delivery retries are the processor's own (Attempts/MaxAttempts), so the
/// job itself always reports success to Hangfire.
/// </summary>
public sealed class OutboxDispatchJob
{
    private readonly OutboxProcessor _processor;

    public OutboxDispatchJob(OutboxProcessor processor) => _processor = processor;

    public Task RunAsync(CancellationToken ct) => _processor.ProcessPendingAsync(ct);
}
