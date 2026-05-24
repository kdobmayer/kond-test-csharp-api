using System.Net;
using System.Net.Http.Json;
using DocManager.DTOs;

namespace DocManager.Tests;

public class DocumentShareAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentShareAuthorizationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<UserDto> CreateTestUser(string suffix = "")
    {
        var dto = new CreateUserDto(
            $"authuser{suffix}{Guid.NewGuid():N}",
            $"auth{suffix}{Guid.NewGuid():N}@test.com",
            "Auth User");
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    private async Task<DocumentDto> UploadDocument(int userId, string name = "doc.txt")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(userId.ToString()), "createdByUserId");
        var fileContent = new ByteArrayContent("test file content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", name);
        var response = await _client.PostAsync("/api/documents", content);
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    private async Task CreateShare(int docId, int ownerId, int recipientId, string permission = "read")
    {
        var dto = new CreateShareRequestDto(recipientId, permission);
        var response = await _client.PostAsJsonAsync(
            $"/api/documents/{docId}/shares?requestingUserId={ownerId}", dto);
        response.EnsureSuccessStatusCode();
    }

    private static MultipartFormDataContent CreateUpdateContent(int requestingUserId, string? name = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(requestingUserId.ToString()), "requestingUserId");
        if (name != null)
            content.Add(new StringContent(name), "name");
        return content;
    }

    // --- GET /api/documents/{id} ---

    [Fact]
    public async Task GetById_Owner_ReturnsOk()
    {
        var owner = await CreateTestUser("gbo");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}?requestingUserId={owner.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReadShareUser_ReturnsOk()
    {
        var owner = await CreateTestUser("gbr");
        var reader = await CreateTestUser("gbr2");
        var doc = await UploadDocument(owner.Id);
        await CreateShare(doc.Id, owner.Id, reader.Id, "read");

        var response = await _client.GetAsync($"/api/documents/{doc.Id}?requestingUserId={reader.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WriteShareUser_ReturnsOk()
    {
        var owner = await CreateTestUser("gbw");
        var writer = await CreateTestUser("gbw2");
        var doc = await UploadDocument(owner.Id);
        await CreateShare(doc.Id, owner.Id, writer.Id, "write");

        var response = await _client.GetAsync($"/api/documents/{doc.Id}?requestingUserId={writer.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NoShareUser_ReturnsForbidden()
    {
        var owner = await CreateTestUser("gbn");
        var stranger = await CreateTestUser("gbn2");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}?requestingUserId={stranger.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistentUser_ReturnsBadRequest()
    {
        var owner = await CreateTestUser("gbne");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}?requestingUserId=999999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NoRequestingUser_ReturnsBadRequest()
    {
        var owner = await CreateTestUser("gbnu");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PUT /api/documents/{id} ---

    [Fact]
    public async Task Update_Owner_ReturnsOk()
    {
        var owner = await CreateTestUser("uo");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.PutAsync(
            $"/api/documents/{doc.Id}",
            CreateUpdateContent(owner.Id, "updated-name.txt"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WriteShareUser_ReturnsOk()
    {
        var owner = await CreateTestUser("uw");
        var writer = await CreateTestUser("uw2");
        var doc = await UploadDocument(owner.Id);
        await CreateShare(doc.Id, owner.Id, writer.Id, "write");

        var response = await _client.PutAsync(
            $"/api/documents/{doc.Id}",
            CreateUpdateContent(writer.Id, "writer-updated.txt"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReadShareUser_ReturnsForbidden()
    {
        var owner = await CreateTestUser("ur");
        var reader = await CreateTestUser("ur2");
        var doc = await UploadDocument(owner.Id);
        await CreateShare(doc.Id, owner.Id, reader.Id, "read");

        var response = await _client.PutAsync(
            $"/api/documents/{doc.Id}",
            CreateUpdateContent(reader.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_NoShareUser_ReturnsForbidden()
    {
        var owner = await CreateTestUser("un");
        var stranger = await CreateTestUser("un2");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.PutAsync(
            $"/api/documents/{doc.Id}",
            CreateUpdateContent(stranger.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- GET /api/documents/{id}/download ---

    [Fact]
    public async Task Download_Owner_ReturnsOk()
    {
        var owner = await CreateTestUser("dlo");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/download?requestingUserId={owner.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_ReadShareUser_ReturnsOk()
    {
        var owner = await CreateTestUser("dlr");
        var reader = await CreateTestUser("dlr2");
        var doc = await UploadDocument(owner.Id);
        await CreateShare(doc.Id, owner.Id, reader.Id, "read");

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/download?requestingUserId={reader.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_WriteShareUser_ReturnsOk()
    {
        var owner = await CreateTestUser("dlw");
        var writer = await CreateTestUser("dlw2");
        var doc = await UploadDocument(owner.Id);
        await CreateShare(doc.Id, owner.Id, writer.Id, "write");

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/download?requestingUserId={writer.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_NoShareUser_ReturnsForbidden()
    {
        var owner = await CreateTestUser("dln");
        var stranger = await CreateTestUser("dln2");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/download?requestingUserId={stranger.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Download_NoRequestingUser_ReturnsBadRequest()
    {
        var owner = await CreateTestUser("dlnu");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/download");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
