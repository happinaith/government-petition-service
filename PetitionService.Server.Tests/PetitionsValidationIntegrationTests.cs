using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PetitionService.Server.Tests;

public class PetitionsValidationIntegrationTests
{
    [Fact]
    public async Task GetAll_Returns400_ForInvalidSortBy_AndValidationShape()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await RegisterAsync(client, $"sort_user_{Guid.NewGuid():N}", "password");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/petitions?sortBy=invalidField");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("SortBy", out var sortByErrors));
        Assert.True(sortByErrors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetAll_Returns400_WhenMinSignaturesGreaterThanMaxSignatures()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await RegisterAsync(client, $"range_user_{Guid.NewGuid():N}", "password");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/petitions?minSignatures=10&maxSignatures=3");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("MinSignatures", out var minErrors));
        Assert.Contains("cannot be greater", minErrors[0].GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Returns400_ForTooShortContent_AndValidationErrors()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await RegisterAsync(client, $"invalid_create_{Guid.NewGuid():N}", "password");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/petitions")
        {
            Content = JsonContent(new
            {
                title = "Ok title",
                content = "short"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Content", out var contentErrors));
        Assert.True(contentErrors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Create_Returns409_WithProblemDetails_ForDuplicateTitleBySameAuthor()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await RegisterAsync(client, $"dup_user_{Guid.NewGuid():N}", "password");

        await CreatePetitionAsync(client, token, "City Park", "Body text long enough", "Urban");

        var duplicateRequest = new HttpRequestMessage(HttpMethod.Post, "/api/petitions")
        {
            Content = JsonContent(new
            {
                title = " City Park ",
                content = "Another body text that is long enough",
                category = "Urban"
            })
        };
        duplicateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var duplicateResponse = await client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var json = await duplicateResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(409, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Petition conflict", doc.RootElement.GetProperty("title").GetString());
        Assert.Contains("already exists", doc.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_Returns403_ForNonAuthor()
    {
        using var factory = new TestWebApplicationFactory();
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        using var strangerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var ownerToken = await RegisterAsync(ownerClient, $"owner_{Guid.NewGuid():N}", "password");
        var strangerToken = await RegisterAsync(strangerClient, $"stranger_{Guid.NewGuid():N}", "password");

        var petitionId = await CreatePetitionAsync(ownerClient, ownerToken, "Owner Petition", "Owner body content long enough", "Infra");

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/petitions/{petitionId}")
        {
            Content = JsonContent(new
            {
                title = "New title",
                content = "Updated body content long enough",
                category = "Infra"
            })
        };
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", strangerToken);

        var updateResponse = await strangerClient.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_Returns400_WhenFileMissing()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await RegisterAsync(client, $"attach_missing_{Guid.NewGuid():N}", "password");
        var petitionId = await CreatePetitionAsync(client, token, "Attach Missing", "Attachment body content long enough", "Infra");

        using var multipart = new MultipartFormDataContent();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("One or more validation errors occurred.", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task UploadAttachment_Returns409_ForDuplicateFilename()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await RegisterAsync(client, $"attach_dup_{Guid.NewGuid():N}", "password");
        var petitionId = await CreatePetitionAsync(client, token, "Attach Dup", "Attachment body content long enough", "Infra");

        await UploadTextAttachmentAsync(client, token, petitionId, "same-name.txt", "first body");

        var duplicateResponse = await UploadTextAttachmentRawAsync(client, token, petitionId, "same-name.txt", "second body");
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var json = await duplicateResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(409, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Attachment conflict", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task PetitionList_ResponseStructure_ContainsExpectedFields()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await RegisterAsync(client, $"shape_user_{Guid.NewGuid():N}", "password");
        await CreatePetitionAsync(client, token, "Shape Title", "Shape content long enough", "Utilities");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/petitions?page=1&pageSize=5");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("items", out var items));
        Assert.True(doc.RootElement.TryGetProperty("totalCount", out _));
        Assert.True(doc.RootElement.TryGetProperty("page", out _));
        Assert.True(doc.RootElement.TryGetProperty("pageSize", out _));
        Assert.True(doc.RootElement.TryGetProperty("sortBy", out _));
        Assert.True(doc.RootElement.TryGetProperty("sortDir", out _));

        Assert.True(items.GetArrayLength() >= 1);
        var first = items[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("title", out _));
        Assert.True(first.TryGetProperty("author", out _));
        Assert.True(first.TryGetProperty("signatures", out _));
        Assert.True(first.TryGetProperty("createdAt", out _));
    }

    private static async Task<string> RegisterAsync(HttpClient client, string username, string password)
    {
        var registerResponse = await client.PostAsync(
            "/api/auth/register",
            JsonContent(new { username, password }));

        registerResponse.EnsureSuccessStatusCode();
        var payload = await registerResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.TryGetProperty("accessToken", out var tokenProperty)
            ? tokenProperty.GetString() ?? string.Empty
            : string.Empty;
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

    private static async Task UploadTextAttachmentAsync(HttpClient client, string accessToken, int petitionId, string fileName, string body)
    {
        var response = await UploadTextAttachmentRawAsync(client, accessToken, petitionId, fileName, body);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> UploadTextAttachmentRawAsync(HttpClient client, string accessToken, int petitionId, string fileName, string body)
    {
        var multipart = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(body);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(fileContent, "file", fileName);

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments")
        {
            Content = multipart
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(uploadRequest);
        multipart.Dispose();
        return response;
    }

    private static StringContent JsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}