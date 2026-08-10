using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.Infrastructure.Platform;

/// <summary>
/// Reads the two platform-wide figures that ordinary queries cannot: counts
/// inside RLS'd tenant tables (via the SECURITY DEFINER function installed by
/// AddPlatformTenantCountsFunction) and the size of the identity store.
/// </summary>
public sealed class PlatformMetrics : IPlatformMetrics
{
    private readonly AppDbContext _db;

    public PlatformMetrics(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TenantCounts>> GetTenantCountsAsync(
        CancellationToken cancellationToken)
    {
        // Raw ADO rather than EF: the source is a set-returning function, not
        // a mapped entity, and mapping a keyless type for four counts would
        // put a phantom table in the model snapshot.
        var connection = _db.Database.GetDbConnection();
        var opened = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            opened = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT tenant_id, active_students, active_teachers, guardians, open_campuses
                FROM app_platform_tenant_counts();
                """;

            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);

            var results = new List<TenantCounts>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new TenantCounts(
                    reader.GetGuid(0),
                    Count(reader, 1),
                    Count(reader, 2),
                    Count(reader, 3),
                    Count(reader, 4)));
            }

            return results;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<int> CountSchoolUsersAsync(CancellationToken cancellationToken) =>
        await _db.Users
            .Where(u => u.TenantId != null)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>count() is bigint; every realistic total fits an int.</summary>
    private static int Count(DbDataReader reader, int ordinal) =>
        (int)Math.Min(reader.GetInt64(ordinal), int.MaxValue);
}
