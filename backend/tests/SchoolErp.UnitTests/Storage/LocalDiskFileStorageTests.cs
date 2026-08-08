using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SchoolErp.Application.Abstractions;
using SchoolErp.Infrastructure.Storage;

namespace SchoolErp.UnitTests.Storage;

/// <summary>Path-safety and roundtrip behavior of the disk store.</summary>
public sealed class LocalDiskFileStorageTests : IDisposable
{
    private sealed class FixedTenant : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();

        public bool HasTenant => true;
    }

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"schoolerp-files-{Guid.NewGuid():N}");

    private readonly LocalDiskFileStorage _storage;

    public LocalDiskFileStorageTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = _root })
            .Build();
        _storage = new LocalDiskFileStorage(configuration, new FixedTenant());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_open_delete_roundtrip()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));
        var key = await _storage.SaveAsync("student-photos", ".png", content);
        key.Should().MatchRegex(@"^[0-9a-f]{32}/student-photos/[0-9a-f]{32}\.png$");

        var opened = await _storage.OpenAsync(key);
        opened.Should().NotBeNull();
        opened!.Value.ContentType.Should().Be("image/png");
        using (var reader = new StreamReader(opened.Value.Content))
        {
            (await reader.ReadToEndAsync()).Should().Be("fake-image-bytes");
        }

        await _storage.DeleteAsync(key);
        (await _storage.OpenAsync(key)).Should().BeNull();
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\secrets.txt")]
    [InlineData("00000000000000000000000000000000/x/../../appsettings.json")]
    [InlineData("plain-name.png")]
    public async Task Malformed_or_traversal_keys_resolve_to_nothing(string key) =>
        (await _storage.OpenAsync(key)).Should().BeNull();

    [Fact]
    public async Task Disallowed_extensions_and_categories_are_rejected()
    {
        using var content = new MemoryStream([1]);
        var exe = () => _storage.SaveAsync("student-photos", ".exe", content);
        await exe.Should().ThrowAsync<ArgumentException>();

        var badCategory = () => _storage.SaveAsync("Bad/../Category", ".png", content);
        await badCategory.Should().ThrowAsync<ArgumentException>();
    }
}
