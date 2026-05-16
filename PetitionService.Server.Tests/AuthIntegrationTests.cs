using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PetitionService.Server.Tests;

public class AuthIntegrationTests
{
    [Fact]
    public async Task PetitionList_Filter_Search_Sort_Pagination_Works()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var username = $"user_{Guid.NewGuid():N}";
        var accessToken = await RegisterAsync(client, username, "password");

        var p1 = await CreatePetitionAsync(client, accessToken, "Road Repair", "Road repair proposal body", "Infrastructure");
        var p2 = await CreatePetitionAsync(client, accessToken, "School Meals", "School meals proposal body", "Education");
        var p3 = await CreatePetitionAsync(client, accessToken, "Road Safety", "Road safety proposal body", "Infrastructure");

        await SignPetitionAsync(client, p1);
        await SignPetitionAsync(client, p2);
        await SignPetitionAsync(client, p2);

        var responsePage1 = await client.GetAsync("/api/petitions?category=Infrastructure&q=Road&sortBy=title&sortDir=asc&page=1&pageSize=1");
        responsePage1.EnsureSuccessStatusCode();
        var jsonPage1 = await responsePage1.Content.ReadAsStringAsync();

        Assert.Equal(2, ReadIntProperty(jsonPage1, "totalCount"));
        Assert.Equal(1, ReadIntProperty(jsonPage1, "page"));
        Assert.Equal(1, CountArrayItems(jsonPage1, "items"));
        Assert.Equal("Road Repair", ReadArrayStringProperty(jsonPage1, "items", 0, "title"));

        var responsePage2 = await client.GetAsync("/api/petitions?category=Infrastructure&q=Road&sortBy=title&sortDir=asc&page=2&pageSize=1");
        responsePage2.EnsureSuccessStatusCode();
        var jsonPage2 = await responsePage2.Content.ReadAsStringAsync();
        Assert.Equal("Road Safety", ReadArrayStringProperty(jsonPage2, "items", 0, "title"));
    }

    [Fact]
    public async Task PetitionCrud_Create_View_Update_Delete_Works_ForAuthor()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var username = $"user_{Guid.NewGuid():N}";
        var accessToken = await RegisterAsync(client, username, "password");

        var petitionId = await CreatePetitionAsync(client, accessToken, "Original Title", "Original body content", "Original");

        var getResponse = await client.GetAsync($"/api/petitions/{petitionId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/petitions/{petitionId}")
        {
            Content = JsonContent(new { title = "Updated Title", content = "Updated body content", category = "Updated" })
        };
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var updateResponse = await client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updatedGet = await client.GetAsync($"/api/petitions/{petitionId}");
        var updatedJson = await updatedGet.Content.ReadAsStringAsync();
        Assert.Equal("Updated Title", ReadStringProperty(updatedJson, "title"));
        Assert.Equal("Updated", ReadStringProperty(updatedJson, "category"));

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/petitions/{petitionId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDelete = await client.GetAsync($"/api/petitions/{petitionId}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task Attachment_Access_Is_Forbidden_For_NonOwner_NonAdmin()
    {
        using var factory = new TestWebApplicationFactory();
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        using var intruderClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var owner = $"owner_{Guid.NewGuid():N}";
        var intruder = $"intruder_{Guid.NewGuid():N}";
        var ownerToken = await RegisterAsync(ownerClient, owner, "password");
        var intruderToken = await RegisterAsync(intruderClient, intruder, "password");

        var petitionId = await CreatePetitionAsync(ownerClient, ownerToken, "Attachment Target", "Attachment target content", "Infra");
        var attachmentId = await UploadTextAttachmentAsync(ownerClient, ownerToken, petitionId, "owned.txt", "owner file body");

        var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/petitions/{petitionId}/attachments");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", intruderToken);
        var listResponse = await intruderClient.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        var signRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments/{attachmentId}/presigned-download");
        signRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", intruderToken);
        var signResponse = await intruderClient.SendAsync(signRequest);
        Assert.Equal(HttpStatusCode.Forbidden, signResponse.StatusCode);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/petitions/{petitionId}/attachments/{attachmentId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", intruderToken);
        var deleteResponse = await intruderClient.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_Works_AfterRegister_WhenCookiePresent()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var username = $"user_{Guid.NewGuid():N}";
        await RegisterAsync(client, username, "password");

        var refreshResponse = await client.PostAsync(
            "/api/auth/refresh",
            content: null);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_Invalidates_Session_And_RefreshFails()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var username = $"user_{Guid.NewGuid():N}";
        var accessToken = await RegisterAsync(client, username, "password");

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await client.PostAsync(
            "/api/auth/refresh",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task DeletePetition_IsAllowed_ForAuthor()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var username = $"user_{Guid.NewGuid():N}";
        var accessToken = await RegisterAsync(client, username, "password");

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/petitions")
        {
            Content = JsonContent(new { title = "Test", content = "Body content" })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var petitionJson = await createResponse.Content.ReadAsStringAsync();
        var petitionId = ReadPetitionId(petitionJson);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/petitions/{petitionId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task BootstrapAdmin_Enables_AdminOnly_Actions()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var adminUsername = $"admin_{Guid.NewGuid():N}";
        var userUsername = $"user_{Guid.NewGuid():N}";

        await RegisterAsync(client, adminUsername, "password");
        var userToken = await RegisterAsync(client, userUsername, "password");

        var bootstrapResponse = await client.PostAsync(
            "/api/auth/bootstrap-admin",
            JsonContent(new { username = adminUsername, bootstrapToken = "test-bootstrap-token" }));
        Assert.Equal(HttpStatusCode.NoContent, bootstrapResponse.StatusCode);

        var loginResponse = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new { username = adminUsername, password = "password" }));
        var adminToken = ReadAccessToken(loginResponse);

        var grantRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/grant-admin")
        {
            Content = JsonContent(new { username = userUsername })
        };
        grantRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var grantResponse = await client.SendAsync(grantRequest);
        Assert.Equal(HttpStatusCode.NoContent, grantResponse.StatusCode);

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/petitions")
        {
            Content = JsonContent(new { title = "For Delete", content = "Body content" })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var createResponse = await client.SendAsync(createRequest);
        var petitionId = ReadPetitionId(await createResponse.Content.ReadAsStringAsync());

        var userLoginResponse = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new { username = userUsername, password = "password" }));
        var newUserToken = ReadAccessToken(userLoginResponse);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/petitions/{petitionId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newUserToken);

        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Attachment_Upload_Download_Delete_Works_With_PreSignedUrl()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var username = $"user_{Guid.NewGuid():N}";
        var accessToken = await RegisterAsync(client, username, "password");

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/petitions")
        {
            Content = JsonContent(new { title = "Attachment Petition", content = "Long enough body" })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.SendAsync(createRequest);
        var petitionId = ReadPetitionId(await createResponse.Content.ReadAsStringAsync());

        using var multipart = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("hello from attachment");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(fileContent, "file", "note.txt");

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments")
        {
            Content = multipart
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var uploadResponse = await client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var attachmentId = ReadIntProperty(await uploadResponse.Content.ReadAsStringAsync(), "id");

        var signedUrlRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments/{attachmentId}/presigned-download");
        signedUrlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var signedUrlResponse = await client.SendAsync(signedUrlRequest);
        Assert.Equal(HttpStatusCode.OK, signedUrlResponse.StatusCode);
        var downloadUrl = ReadStringProperty(await signedUrlResponse.Content.ReadAsStringAsync(), "url");

        var downloadResponse = await client.GetAsync(downloadUrl);
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloaded = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal("hello from attachment", downloaded);

        var deleteAttachmentRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/petitions/{petitionId}/attachments/{attachmentId}");
        deleteAttachmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var deleteAttachmentResponse = await client.SendAsync(deleteAttachmentRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteAttachmentResponse.StatusCode);
    }

    [Fact]
    public async Task Attachment_Upload_Rejects_Unsupported_Type()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var username = $"user_{Guid.NewGuid():N}";
        var accessToken = await RegisterAsync(client, username, "password");

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/petitions")
        {
            Content = JsonContent(new { title = "Attachment Validation", content = "Long enough body" })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.SendAsync(createRequest);
        var petitionId = ReadPetitionId(await createResponse.Content.ReadAsStringAsync());

        using var multipart = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("MZ fake exe");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(fileContent, "file", "payload.exe");

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments")
        {
            Content = multipart
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var uploadResponse = await client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string username, string password)
    {
        var registerResponse = await client.PostAsync(
            "/api/auth/register",
            JsonContent(new { username, password }));

        registerResponse.EnsureSuccessStatusCode();
        return ReadAccessToken(registerResponse);
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
        return ReadPetitionId(await response.Content.ReadAsStringAsync());
    }

    private static async Task SignPetitionAsync(HttpClient client, int petitionId)
    {
        var response = await client.PostAsync($"/api/petitions/{petitionId}/sign", content: null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<int> UploadTextAttachmentAsync(HttpClient client, string accessToken, int petitionId, string fileName, string body)
    {
        using var multipart = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(body);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(fileContent, "file", fileName);

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/petitions/{petitionId}/attachments")
        {
            Content = multipart
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();
        return ReadIntProperty(await uploadResponse.Content.ReadAsStringAsync(), "id");
    }

    private static int ReadPetitionId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private static string ReadAccessToken(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            foreach (var setCookie in setCookieValues)
            {
                var accessTokenPrefix = "accessToken=";
                var startIndex = setCookie.IndexOf(accessTokenPrefix, StringComparison.OrdinalIgnoreCase);
                if (startIndex < 0)
                {
                    continue;
                }

                startIndex += accessTokenPrefix.Length;
                var endIndex = setCookie.IndexOf(';', startIndex);
                return endIndex >= 0
                    ? setCookie[startIndex..endIndex]
                    : setCookie[startIndex..];
            }
        }

        return string.Empty;
    }

    private static int ReadIntProperty(string json, string propertyName)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(propertyName).GetInt32();
    }

    private static string ReadStringProperty(string json, string propertyName)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(propertyName).GetString()!;
    }

    private static int CountArrayItems(string json, string propertyName)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(propertyName).GetArrayLength();
    }

    private static string ReadArrayStringProperty(string json, string arrayPropertyName, int index, string propertyName)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(arrayPropertyName)[index].GetProperty(propertyName).GetString()!;
    }

    private static StringContent JsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}
