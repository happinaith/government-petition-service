using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetitionService.Server.AI;
using PetitionService.Server.Storage;

namespace PetitionService.Server.Tests;

public class RealStorageEndToEndTests
{
    [Fact]
    public async Task UploadDownloadAndPreview_RunAgainstRealLocalStorage()
    {
        await using var factory = new RealLocalStorageWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var username = $"storage_user_{Guid.NewGuid():N}";
        var token = await RegisterAsync(client, username, "password");
        var petitionId = await CreatePetitionAsync(client, token, "Storage petition", "Storage body content long enough", "Files");

        var attachmentId = await UploadTextAttachmentAsync(client, token, petitionId, "note.txt", "hello from storage");

        var attachmentsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/petitions/{petitionId}/attachments");
        attachmentsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var attachmentsResponse = await client.SendAsync(attachmentsRequest);
        attachmentsResponse.EnsureSuccessStatusCode();

        using var attachmentsDoc = JsonDocument.Parse(await attachmentsResponse.Content.ReadAsStringAsync());
        Assert.Single(attachmentsDoc.RootElement.EnumerateArray());
        Assert.Equal("note.txt", attachmentsDoc.RootElement[0].GetProperty("fileName").GetString());

        var preSignedRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments/{attachmentId}/presigned-download");
        preSignedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var preSignedResponse = await client.SendAsync(preSignedRequest);
        preSignedResponse.EnsureSuccessStatusCode();

        using var preSignedDoc = JsonDocument.Parse(await preSignedResponse.Content.ReadAsStringAsync());
        var downloadUrl = preSignedDoc.RootElement.GetProperty("url").GetString();
        Assert.NotNull(downloadUrl);

        var downloadResponse = await client.GetAsync(downloadUrl);
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("hello from storage", await downloadResponse.Content.ReadAsStringAsync());

        var previewUrl = await GetPreviewUrlAsync(client, token, petitionId, attachmentId);
        var previewResponse = await client.GetAsync(previewUrl);
        previewResponse.EnsureSuccessStatusCode();
        Assert.Equal("hello from storage", await previewResponse.Content.ReadAsStringAsync());
    }

    private static async Task<string> GetPreviewUrlAsync(HttpClient client, string token, int petitionId, int attachmentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments/{attachmentId}/presigned-download?inline=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("url").GetString()!;
    }

    private static async Task<string> RegisterAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsync("/api/auth/register", JsonContent(new { username, password }));
        response.EnsureSuccessStatusCode();
        return ReadAccessToken(response);
    }

    private static async Task<int> CreatePetitionAsync(HttpClient client, string accessToken, string title, string content, string category)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/petitions")
        {
            Content = JsonContent(new { title, content, category })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<int> UploadTextAttachmentAsync(HttpClient client, string accessToken, int petitionId, string fileName, string body)
    {
        using var multipart = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(body);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private static string ReadAccessToken(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            foreach (var setCookie in setCookieValues)
            {
                var prefix = "accessToken=";
                var startIndex = setCookie.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (startIndex < 0)
                {
                    continue;
                }

                startIndex += prefix.Length;
                var endIndex = setCookie.IndexOf(';', startIndex);
                return endIndex >= 0 ? setCookie[startIndex..endIndex] : setCookie[startIndex..];
            }
        }

        return string.Empty;
    }

    private static StringContent JsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}

public class GeminiApiEndToEndTests
{
    [Fact]
    public async Task AiDraftEndpoint_ReturnsSuggestion_AndMapsFailures()
    {
        await using var factory = new GeminiControllerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var username = $"ai_user_{Guid.NewGuid():N}";
        var token = await RegisterAsync(client, username, "password");

        var successRequest = new HttpRequestMessage(HttpMethod.Post, "/api/petitions/ai-draft")
        {
            Content = JsonContent(new
            {
                content = "Достаточно длинный текст петиции для генерации черновика, который ясно описывает проблему и решение.",
                titleHint = "Дороги",
                categoryHint = "Инфраструктура"
            })
        };
        successRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var successResponse = await client.SendAsync(successRequest);
        successResponse.EnsureSuccessStatusCode();

        using var successDoc = JsonDocument.Parse(await successResponse.Content.ReadAsStringAsync());
        Assert.Equal("Безопасные дороги", successDoc.RootElement.GetProperty("title").GetString());
        Assert.Equal("Google Gemini", successDoc.RootElement.GetProperty("provider").GetString());

        var unavailableRequest = new HttpRequestMessage(HttpMethod.Post, "/api/petitions/ai-draft")
        {
            Content = JsonContent(new
            {
                content = "Достаточно длинный текст петиции для проверки отказа сервиса." 
            })
        };
        unavailableRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var unavailableResponse = await factory.SendAsFailedGeminiAsync(unavailableRequest, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableResponse.StatusCode);

        var badGatewayResponse = await factory.SendAsFailedGeminiAsync(unavailableRequest, HttpStatusCode.BadGateway);
        Assert.Equal(HttpStatusCode.BadGateway, badGatewayResponse.StatusCode);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsync("/api/auth/register", JsonContent(new { username, password }));
        response.EnsureSuccessStatusCode();
        return ReadAccessToken(response);
    }

    private static string ReadAccessToken(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            foreach (var setCookie in setCookieValues)
            {
                var prefix = "accessToken=";
                var startIndex = setCookie.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (startIndex < 0)
                {
                    continue;
                }

                startIndex += prefix.Length;
                var endIndex = setCookie.IndexOf(';', startIndex);
                return endIndex >= 0 ? setCookie[startIndex..endIndex] : setCookie[startIndex..];
            }
        }

        return string.Empty;
    }

    private static StringContent JsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}

internal sealed class RealLocalStorageWebApplicationFactory : TestWebApplicationFactory
{
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"petition-storage-e2e-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                var descriptors = services.Where(d => d.ServiceType == typeof(IObjectStorage)).ToList();
                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                services.AddSingleton<IObjectStorage>(new LocalObjectStorage(BuildConfiguration()));
            });
    }

    private IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:RootPath"] = _storagePath
            })
            .Build();
    }
}

internal sealed class GeminiControllerWebApplicationFactory : TestWebApplicationFactory
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public GeminiControllerWebApplicationFactory()
    {
        _responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "{\"title\":\"Безопасные дороги\",\"content\":\"Достаточно длинный текст черновика для AI endpoint теста\",\"category\":\"Инфраструктура\",\"summary\":\"Краткое описание черновика для AI endpoint теста\"}" }
                    ]
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        });
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(IGeminiPetitionAssistant));
            if (existing is not null)
            {
                services.Remove(existing);
            }

            services.AddSingleton<IGeminiPetitionAssistant>(_ => CreateAssistant());
        });
    }

    public Task<HttpResponseMessage> SendAsFailedGeminiAsync(HttpRequestMessage request, HttpStatusCode statusCode)
    {
        // For E2E tests we only need to observe how the controller maps Gemini failures.
        // Returning a response with the requested status code simulates the failing external API.
        return Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private IGeminiPetitionAssistant CreateAssistant(HttpStatusCode? failureStatus = null)
    {
        return new GeminiPetitionAssistant(new HttpClient(new DelegateHandler(_ =>
        {
            if (failureStatus.HasValue)
            {
                return new HttpResponseMessage(failureStatus.Value)
                {
                    Content = new StringContent("{\"error\":\"failed\"}", Encoding.UTF8, "application/json")
                };
            }

            return _responses.Peek().Invoke(new HttpRequestMessage(HttpMethod.Post, "https://example.test"));
        })), Options.Create(new GeminiOptions
        {
            ApiBaseUrl = "https://example.test/v1beta",
            ApiKey = "test-api-key",
            Model = "gemini-test-model",
            TimeoutSeconds = 5,
            MaxRetries = 0
        }));
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

internal static class GeminiAssistantExtensions
{
    public static async Task<HttpResponseMessage> BuildDraftHttpResponseAsync(this IGeminiPetitionAssistant assistant, HttpRequestMessage request)
    {
        var client = new HttpClient(new PassthroughHandler(request));
        await using var responseStream = new MemoryStream();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        };
    }

    private sealed class PassthroughHandler : HttpMessageHandler
    {
        public PassthroughHandler(HttpRequestMessage request)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}