using System.Net;
using System.Net.Http.Json;
using DocManager.DTOs;

namespace DocManager.Tests;

public class DocumentsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentsControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<UserDto> CreateTestUser(string suffix = "")
    {
        var dto = new CreateUserDto($"docuser{suffix}{Guid.NewGuid():N}", $"doc{suffix}{Guid.NewGuid():N}@test.com", "Doc User");
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    private MultipartFormDataContent CreateUploadContent(string name, int userId, string? description = null, int? folderId = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(userId.ToString()), "createdByUserId");
        if (description != null)
            content.Add(new StringContent(description), "description");
        if (folderId.HasValue)
            content.Add(new StringContent(folderId.Value.ToString()), "folderId");

        var fileContent = new ByteArrayContent("Hello, World! This is test content."u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        return content;
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/documents");
        response.EnsureSuccessStatusCode();
        var docs = await response.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.NotNull(docs);
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsCreated()
    {
        var user = await CreateTestUser("upload");
        var content = CreateUploadContent("test-doc.txt", user.Id, "A test document");

        var response = await _client.PostAsync("/api/documents", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.NotNull(doc);
        Assert.Equal("test-doc.txt", doc.Name);
        Assert.Equal("text/plain", doc.ContentType);
        Assert.Equal(1, doc.Version);
    }

    [Fact]
    public async Task Upload_InvalidUser_ReturnsBadRequest()
    {
        var content = CreateUploadContent("test.txt", 9999);
        var response = await _client.PostAsync("/api/documents", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingDocument_ReturnsOk()
    {
        var user = await CreateTestUser("getbyid");
        var content = CreateUploadContent("getbyid.txt", user.Id);
        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        var response = await _client.GetAsync($"/api/documents/{uploaded!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/documents/9999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_ExistingDocument_ReturnsFile()
    {
        var user = await CreateTestUser("download");
        var content = CreateUploadContent("download.txt", user.Id);
        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        var response = await _client.GetAsync($"/api/documents/{uploaded!.Id}/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_ExistingDocument_ReturnsNoContent()
    {
        var user = await CreateTestUser("delete");
        var content = CreateUploadContent("delete.txt", user.Id);
        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        var response = await _client.DeleteAsync($"/api/documents/{uploaded!.Id}?requestingUserId={user.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AddTag_ValidDocAndTag_ReturnsNoContent()
    {
        var user = await CreateTestUser("tag");
        var content = CreateUploadContent("tagged.txt", user.Id);
        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        var tagDto = new CreateTagDto($"doc-tag-{Guid.NewGuid():N}", null);
        var tagResponse = await _client.PostAsJsonAsync("/api/tags", tagDto);
        var tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>();

        var response = await _client.PostAsync($"/api/documents/{uploaded!.Id}/tags/{tag!.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AddTag_Duplicate_ReturnsConflict()
    {
        var user = await CreateTestUser("tagdup");
        var content = CreateUploadContent("tagdup.txt", user.Id);
        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        var tagDto = new CreateTagDto($"dup-tag-{Guid.NewGuid():N}", null);
        var tagResponse = await _client.PostAsJsonAsync("/api/tags", tagDto);
        var tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>();

        await _client.PostAsync($"/api/documents/{uploaded!.Id}/tags/{tag!.Id}", null);
        var response = await _client.PostAsync($"/api/documents/{uploaded.Id}/tags/{tag.Id}", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RemoveTag_Existing_ReturnsNoContent()
    {
        var user = await CreateTestUser("rmtag");
        var content = CreateUploadContent("rmtag.txt", user.Id);
        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        var tagDto = new CreateTagDto($"rm-tag-{Guid.NewGuid():N}", null);
        var tagResponse = await _client.PostAsJsonAsync("/api/tags", tagDto);
        var tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>();

        await _client.PostAsync($"/api/documents/{uploaded!.Id}/tags/{tag!.Id}", null);
        var response = await _client.DeleteAsync($"/api/documents/{uploaded.Id}/tags/{tag.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetVersions_AfterUpload_ReturnsInitialVersion()
    {
        var user = await CreateTestUser("ver");
        var content = CreateUploadContent("versioned.txt", user.Id);
        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        var response = await _client.GetAsync($"/api/documents/{uploaded!.Id}/versions");
        response.EnsureSuccessStatusCode();
        var versions = await response.Content.ReadFromJsonAsync<List<DocumentVersionDto>>();
        Assert.NotNull(versions);
        Assert.Single(versions);
        Assert.Equal(1, versions[0].VersionNumber);
    }
}
