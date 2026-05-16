using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetitionService.Server.AI;
using PetitionService.Server.Storage;

namespace PetitionService.Server.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"petition-tests-{Guid.NewGuid():N}.db");
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"petition-storage-tests-{Guid.NewGuid():N}");
    private readonly TestObjectStorage _objectStorage = new();
    private readonly TestGeminiPetitionAssistant _geminiAssistant = new();

    public TestObjectStorage ObjectStorage => _objectStorage;

    public TestGeminiPetitionAssistant GeminiAssistant => _geminiAssistant;

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
                ["Gemini:ApiKey"] = "test-api-key",
                ["ObjectStorage:Provider"] = "Testing",
                ["ObjectStorage:RootPath"] = _storagePath,
                ["ObjectStorage:MaxFileSizeBytes"] = "5242880",
                ["ObjectStorage:PreSignedUrlTtlMinutes"] = "5"
            };

            configBuilder.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(_objectStorage);

            services.RemoveAll<IGeminiPetitionAssistant>();
            services.AddSingleton<IGeminiPetitionAssistant>(_geminiAssistant);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _objectStorage.Dispose();
        TryDeleteDirectory(_storagePath);
        TryDeleteFile(_dbPath);
    }

    private static void TryDeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
