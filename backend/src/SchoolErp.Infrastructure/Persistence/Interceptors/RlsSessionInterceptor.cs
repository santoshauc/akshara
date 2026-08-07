using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Second line of tenant defense. Whenever a connection is opened for a scope
/// that has a resolved tenant, this interceptor sets the PostgreSQL session
/// variable <c>app.tenant_id</c>, which the row-level-security policies compare
/// against. Even if a query somehow bypassed the EF global filters, RLS would
/// still return zero foreign rows.
/// </summary>
public sealed class RlsSessionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public RlsSessionInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSessionTenant(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSessionTenantAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    private void SetSessionTenant(DbConnection connection)
    {
        using var command = BuildCommand(connection);
        if (command is not null)
        {
            command.ExecuteNonQuery();
        }
    }

    private async Task SetSessionTenantAsync(DbConnection connection, CancellationToken ct)
    {
        var command = BuildCommand(connection);
        if (command is not null)
        {
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    private DbCommand? BuildCommand(DbConnection connection)
    {
        // Catalog-only scopes (e.g. tenant resolution itself, Super Admin) have
        // no tenant; RLS policies treat a missing setting as "no business rows".
        if (!_tenantContext.HasTenant)
        {
            return null;
        }

        var command = connection.CreateCommand();
        // set_config with is_local=false → survives for the session (connection
        // lease); Npgsql resets session state on pool return, so a pooled
        // connection can never leak the previous tenant.
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, false);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenantId";
        parameter.Value = _tenantContext.TenantId.ToString();
        command.Parameters.Add(parameter);
        return command;
    }
}
