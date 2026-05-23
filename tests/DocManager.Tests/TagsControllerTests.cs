using System.Net;
using System.Net.Http.Json;
using DocManager.DTOs;

namespace DocManager.Tests;

public class TagsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TagsControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/tags");
        response.EnsureSuccessStatusCode();
        var tags = await response.Content.ReadFromJsonAsync<List<TagWithCountDto>>();
        Assert.NotNull(tags);
    }

    [Fact]
    public async Task Create_ValidTag_ReturnsCreated()
    {
        var dto = new CreateTagDto("important", "#ff0000");
        var response = await _client.PostAsJsonAsync("/api/tags", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var tag = await response.Content.ReadFromJsonAsync<TagDto>();
        Assert.NotNull(tag);
        Assert.Equal("important", tag.Name);
        Assert.Equal("#ff0000", tag.Color);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        var dto = new CreateTagDto("", null);
        var response = await _client.PostAsJsonAsync("/api/tags", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        var dto = new CreateTagDto("unique-tag", null);
        await _client.PostAsJsonAsync("/api/tags", dto);

        var response = await _client.PostAsJsonAsync("/api/tags", dto);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidTag_ReturnsOk()
    {
        var createDto = new CreateTagDto("update-tag", "#000");
        var createResponse = await _client.PostAsJsonAsync("/api/tags", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TagDto>();

        var updateDto = new UpdateTagDto("updated-tag", "#fff");
        var response = await _client.PutAsJsonAsync($"/api/tags/{created!.Id}", updateDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<TagDto>();
        Assert.Equal("updated-tag", updated!.Name);
    }

    [Fact]
    public async Task Delete_ExistingTag_ReturnsNoContent()
    {
        var dto = new CreateTagDto("delete-tag", null);
        var createResponse = await _client.PostAsJsonAsync("/api/tags", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<TagDto>();

        var response = await _client.DeleteAsync($"/api/tags/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/tags/9999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPopular_ReturnsTagsOrderedByDocumentCountDesc()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var tagA = (await (await _client.PostAsJsonAsync("/api/tags", new CreateTagDto($"pop-a-{suffix}", null))).Content.ReadFromJsonAsync<TagDto>())!;
        var tagB = (await (await _client.PostAsJsonAsync("/api/tags", new CreateTagDto($"pop-b-{suffix}", null))).Content.ReadFromJsonAsync<TagDto>())!;

        var user = (await (await _client.PostAsJsonAsync("/api/users",
            new CreateUserDto($"popuser{suffix}", $"pop{suffix}@test.com", "Pop User"))).Content.ReadFromJsonAsync<UserDto>())!;

        var doc1 = await UploadDocument("pop-doc1.txt", user.Id);
        var doc2 = await UploadDocument("pop-doc2.txt", user.Id);

        // tagA used by 2 documents, tagB used by 1
        await _client.PostAsync($"/api/documents/{doc1.Id}/tags/{tagA.Id}", null);
        await _client.PostAsync($"/api/documents/{doc2.Id}/tags/{tagA.Id}", null);
        await _client.PostAsync($"/api/documents/{doc1.Id}/tags/{tagB.Id}", null);

        var response = await _client.GetAsync("/api/tags/popular");
        response.EnsureSuccessStatusCode();
        var popular = await response.Content.ReadFromJsonAsync<List<PopularTagDto>>();

        Assert.NotNull(popular);
        Assert.True(popular.Count <= 10);

        var resultA = popular.FirstOrDefault(t => t.Id == tagA.Id);
        var resultB = popular.FirstOrDefault(t => t.Id == tagB.Id);

        Assert.NotNull(resultA);
        Assert.Equal(2, resultA.DocumentCount);
        Assert.NotNull(resultB);
        Assert.Equal(1, resultB.DocumentCount);
        Assert.True(popular.IndexOf(resultA) < popular.IndexOf(resultB));
    }

    private async Task<DocumentDto> UploadDocument(string name, int userId)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(userId.ToString()), "createdByUserId");
        var fileContent = new ByteArrayContent("test content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", name);
        var response = await _client.PostAsync("/api/documents", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }
}
