using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PetitionService.Server.Storage;
using System.Collections.Concurrent;

namespace PetitionService.Server.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"petition-tests-{Guid.NewGuid():N}.db");
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"petition-storage-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                ["Jwt:AccessTokenTtlMinutes"] = "1",
                ["Jwt:RefreshTokenTtlDays"] = "1",
                ["Auth:AdminBootstrapToken"] = "test-bootstrap-token",
                ["ObjectStorage:Provider"] = "Local",
                ["ObjectStorage:RootPath"] = _storagePath
            };

            configBuilder.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    private sealed class InMemoryObjectStorage : IObjectStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveAsync(string key, Stream stream, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _objects[key] = memory.ToArray();
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_objects.TryGetValue(key, out var bytes))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new MemoryStream(bytes, writable: false);
            return Task.FromResult<Stream?>(stream);
        }

        public Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            _objects.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<string?> CreatePreSignedDownloadUrlAsync(
            string key,
            string fileName,
            TimeSpan ttl,
            bool asAttachment = true,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
