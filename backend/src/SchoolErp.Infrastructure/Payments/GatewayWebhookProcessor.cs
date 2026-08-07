using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Fees.Commands;
using SchoolErp.Domain.Fees;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.Infrastructure.Payments;

/// <summary>
/// Handles gateway webhooks. Two-phase by necessity: the order lookup runs
/// tenant-less against the RLS-free <c>payment_orders</c> table; the payment
/// recording then runs in a fresh scope bound to the order's tenant so the
/// RLS-protected <c>fee_payments</c> insert is legal. Idempotent per order.
/// </summary>
public sealed partial class GatewayWebhookProcessor
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IPaymentGateway _gateway;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<GatewayWebhookProcessor> _logger;

    public GatewayWebhookProcessor(
        IDbContextFactory<AppDbContext> contextFactory,
        IPaymentGateway gateway,
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<GatewayWebhookProcessor> logger)
    {
        _contextFactory = contextFactory;
        _gateway = gateway;
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Returns false only for invalid signatures/unknown orders (→ 400).</summary>
    public async Task<bool> ProcessAsync(string body, string signature, CancellationToken ct = default)
    {
        var gatewayEvent = _gateway.VerifyWebhook(body, signature);
        if (gatewayEvent is null)
        {
            LogInvalidSignature(_logger);
            return false;
        }

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var order = await db.PaymentOrders
            .FirstOrDefaultAsync(o => o.GatewayOrderId == gatewayEvent.GatewayOrderId, ct)
            .ConfigureAwait(false);
        if (order is null)
        {
            LogUnknownOrder(_logger, gatewayEvent.GatewayOrderId);
            return false;
        }

        if (order.Status != PaymentOrderStatus.Created)
        {
            return true; // duplicate delivery — idempotent success
        }

        if (!gatewayEvent.Succeeded)
        {
            order.Status = PaymentOrderStatus.Failed;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }

        order.Status = PaymentOrderStatus.Paid;
        order.GatewayPaymentId = gatewayEvent.GatewayPaymentId;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Record the ledger payment inside the order's tenant scope.
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(order.TenantId);
        await PaymentRecorder.RecordAsync(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<ITenantContext>(),
            scope.ServiceProvider.GetRequiredService<ITenantLookup>(),
            order.StudentId,
            order.AcademicYearId,
            order.Amount,
            DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime),
            PaymentMode.Online,
            gatewayEvent.GatewayPaymentId,
            "Online payment",
            ct).ConfigureAwait(false);

        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected gateway webhook: invalid signature")]
    private static partial void LogInvalidSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway webhook for unknown order {OrderId}")]
    private static partial void LogUnknownOrder(ILogger logger, string orderId);
}
