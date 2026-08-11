namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// Every HTTP test class shares ONE app host and ONE container. Booting the API
/// costs a PostgreSQL container plus the full migration set, and this repo
/// already pays that price 31 times over because each module fixture spins its
/// own. There is no reason for the API tests to add several more: they only read
/// seeded data and assert on the pipeline, so they can share.
///
/// Sharing has a price, and it is the reason for two rules in these files:
/// tests must be order-independent, and a test that exhausts a rate limiter has
/// to take its own host (see <see cref="ApiFixture.CreateIsolatedHost"/>) rather
/// than spend a budget the others are relying on.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollectionDefinition : ICollectionFixture<ApiFixture>
{
    public const string Name = "api-http";
}
