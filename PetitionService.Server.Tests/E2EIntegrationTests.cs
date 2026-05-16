using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using PetitionService.Server.AI;

namespace PetitionService.Server.Tests;

public class AttachmentAndAiIntegrationTests
{
    [Fact]
    public async Task UploadDownloadAndPreview_RunAgainstTestStorage()
    {
        await using var factory = new TestWebApplicationFactory();
        var clientOptions = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        };
        using var httpClient = factory.CreateClient(clientOptions);

        var username = $"storage_user_{Guid.NewGuid():N}";
        var token = await RegisterAsync(httpClient, username, "password");
        var petitionId = await CreatePetitionAsync(httpClient, token, "Storage petition", "Storage body content long enough", "Files");

        var attachmentId = await UploadTextAttachmentAsync(httpClient, token, petitionId, "note.txt", "hello from storage");

        var attachmentsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/petitions/{petitionId}/attachments");
        attachmentsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var attachmentsResponse = await httpClient.SendAsync(attachmentsRequest);
        attachmentsResponse.EnsureSuccessStatusCode();

        using var attachmentsDoc = JsonDocument.Parse(await attachmentsResponse.Content.ReadAsStringAsync());
        Assert.Single(attachmentsDoc.RootElement.EnumerateArray());
        Assert.Equal("note.txt", attachmentsDoc.RootElement[0].GetProperty("fileName").GetString());

        var preSignedRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments/{attachmentId}/presigned-download");
        preSignedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var preSignedResponse = await httpClient.SendAsync(preSignedRequest);
        preSignedResponse.EnsureSuccessStatusCode();

        using var preSignedDoc = JsonDocument.Parse(await preSignedResponse.Content.ReadAsStringAsync());
        var downloadUrl = preSignedDoc.RootElement.GetProperty("url").GetString();
        Assert.NotNull(downloadUrl);

        var downloadResponse = await httpClient.GetAsync(downloadUrl);
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("hello from storage", await downloadResponse.Content.ReadAsStringAsync());

        var previewUrl = await GetPreviewUrlAsync(httpClient, token, petitionId, attachmentId);
        var previewResponse = await httpClient.GetAsync(previewUrl);
        previewResponse.EnsureSuccessStatusCode();
        Assert.Equal("hello from storage", await previewResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AiDraftEndpoint_ReturnsSuggestion_AndMapsFailures()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.GeminiAssistant.ConfigureSuccess(new PetitionAiDraftSuggestion(
            "Безопасные дороги",
            "Достаточно длинный текст черновика для AI endpoint теста",
            "Инфраструктура",
            "Краткое описание черновика для AI endpoint теста",
            "Google Gemini",
            "gemini-test-model"));

        using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var username = $"ai_user_{Guid.NewGuid():N}";
        var token = await RegisterAsync(httpClient, username, "password");

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

        var successResponse = await httpClient.SendAsync(successRequest);
        successResponse.EnsureSuccessStatusCode();

        using var successDoc = JsonDocument.Parse(await successResponse.Content.ReadAsStringAsync());
        Assert.Equal("Безопасные дороги", successDoc.RootElement.GetProperty("title").GetString());
        Assert.Equal("Google Gemini", successDoc.RootElement.GetProperty("provider").GetString());

        var unavailablePayload = new
        {
            content = "Достаточно длинный текст петиции для проверки отказа сервиса."
        };

        factory.GeminiAssistant.ConfigureFailure(StatusCodes.Status503ServiceUnavailable);
        var unavailableRequest1 = new HttpRequestMessage(HttpMethod.Post, "/api/petitions/ai-draft")
        {
            Content = JsonContent(unavailablePayload)
        };
        unavailableRequest1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var unavailableResponse = await httpClient.SendAsync(unavailableRequest1);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableResponse.StatusCode);

        factory.GeminiAssistant.ConfigureFailure(StatusCodes.Status502BadGateway);
        var unavailableRequest2 = new HttpRequestMessage(HttpMethod.Post, "/api/petitions/ai-draft")
        {
            Content = JsonContent(unavailablePayload)
        };
        unavailableRequest2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var badGatewayResponse = await httpClient.SendAsync(unavailableRequest2);
        Assert.Equal(HttpStatusCode.BadGateway, badGatewayResponse.StatusCode);
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
