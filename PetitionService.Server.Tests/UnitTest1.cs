using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PetitionService.Server.AI;
using PetitionService.Server.Storage;

namespace PetitionService.Server.Tests;

public class GeminiPetitionAssistantTests
{
    [Fact]
    public async Task BuildDraftAsync_Throws503_WhenApiKeyMissing()
    {
        var assistant = CreateAssistant(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }, apiKey: string.Empty);

        var ex = await Assert.ThrowsAsync<GeminiIntegrationException>(() =>
            assistant.BuildDraftAsync(new PetitionAiDraftRequest("Достаточно длинный текст заявки", "Подсказка", "Категория")));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ex.StatusCode);
    }

    [Fact]
    public async Task BuildDraftAsync_NormalizesValidGeminiResponse()
    {
        var responseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  { "text": "```json\n{\"title\":\"  Безопасные дороги  \",\"content\":\"Достаточно длинный текст черновика для проверки\",\"category\":\"  Инфраструктура  \",\"summary\":\"Краткое описание петиции, которого достаточно по длине\"}\n```" }
                ]
              }
            }
          ]
        }
        """;

        var assistant = CreateAssistant(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var result = await assistant.BuildDraftAsync(new PetitionAiDraftRequest(
            "Исходный текст петиции для черновика",
            "Дороги",
            "Инфраструктура"));

        Assert.Equal("Безопасные дороги", result.Title);
        Assert.Equal("Достаточно длинный текст черновика для проверки", result.Content);
        Assert.Equal("Инфраструктура", result.Category);
        Assert.Equal("Краткое описание петиции, которого достаточно по длине", result.Summary);
        Assert.Equal("Google Gemini", result.Provider);
        Assert.Equal("gemini-test-model", result.Model);
    }

    [Fact]
    public async Task BuildDraftAsync_Throws424_OnNonSuccessClientResponse()
    {
        var assistant = CreateAssistant(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad request\"}", Encoding.UTF8, "application/json")
            });

        var ex = await Assert.ThrowsAsync<GeminiIntegrationException>(() =>
            assistant.BuildDraftAsync(new PetitionAiDraftRequest("Достаточно длинный текст заявки", null, null)));

        Assert.Equal(StatusCodes.Status424FailedDependency, ex.StatusCode);
    }

    [Fact]
    public async Task BuildDraftAsync_RetriesTransientFailure_ThenSucceeds()
    {
        var attempts = 0;
        var responseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  { "text": "{\"title\":\"Городское освещение\",\"content\":\"Достаточно длинный текст для теста\",\"category\":\"Город\",\"summary\":\"Достаточно длинное краткое описание предложения\"}" }
                ]
              }
            }
          ]
        }
        """;

        var assistant = CreateAssistant(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":\"temporary\"}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }, maxRetries: 1);

        var result = await assistant.BuildDraftAsync(new PetitionAiDraftRequest("Достаточно длинный текст заявки", null, null));

        Assert.Equal(2, attempts);
        Assert.Equal("Городское освещение", result.Title);
    }

    [Fact]
    public async Task BuildDraftAsync_Throws502_WhenGeneratedPayloadIsInvalidJson()
    {
        var responseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  { "text": "не json" }
                ]
              }
            }
          ]
        }
        """;

        var assistant = CreateAssistant(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var ex = await Assert.ThrowsAsync<GeminiIntegrationException>(() =>
            assistant.BuildDraftAsync(new PetitionAiDraftRequest("Достаточно длинный текст заявки", null, null)));

        Assert.Equal(StatusCodes.Status502BadGateway, ex.StatusCode);
    }

    private static GeminiPetitionAssistant CreateAssistant(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string apiKey = "test-api-key",
        int maxRetries = 0)
    {
        var options = Options.Create(new GeminiOptions
        {
            ApiBaseUrl = "https://example.test/v1beta",
            ApiKey = apiKey,
            Model = "gemini-test-model",
            TimeoutSeconds = 5,
            MaxRetries = maxRetries
        });

        return new GeminiPetitionAssistant(new HttpClient(new DelegateHandler(handler)), options);
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}

public class LocalObjectStorageTests
{
    [Fact]
    public async Task SaveAndOpenRead_RoundTripsData()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"storage-tests-{Guid.NewGuid():N}");
        try
        {
            var storage = CreateStorage(rootPath);
            var payload = Encoding.UTF8.GetBytes("hello object storage");

            await storage.SaveAsync("petitions/1/file.txt", new MemoryStream(payload));

            await using var stream = await storage.OpenReadAsync("petitions/1/file.txt");
            Assert.NotNull(stream);

            using var reader = new StreamReader(stream!, Encoding.UTF8);
            Assert.Equal("hello object storage", await reader.ReadToEndAsync());
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_Throws_ForTraversalKey()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"storage-tests-{Guid.NewGuid():N}");
        try
        {
            var storage = CreateStorage(rootPath);
            var payload = Encoding.UTF8.GetBytes("data");

            await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(() =>
                storage.SaveAsync("../escape.txt", new MemoryStream(payload)));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteIfExistsAsync_DoesNotThrow_WhenObjectIsMissing()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"storage-tests-{Guid.NewGuid():N}");
        try
        {
            var storage = CreateStorage(rootPath);
            await storage.DeleteIfExistsAsync("petitions/404/missing.txt");
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static LocalObjectStorage CreateStorage(string rootPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:RootPath"] = rootPath
            })
            .Build();

        return new LocalObjectStorage(configuration);
    }
}
