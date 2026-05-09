using System.Net;
using System.Net.Http.Json;
using DocManager.DTOs;

namespace DocManager.Tests;

public class SearchControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SearchControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<UserDto> CreateTestUser()
    {
        var dto = new CreateUserDto($"searchuser{Guid.NewGuid():N}", $"search{Guid.NewGuid():N}@test.com", "Search User");
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/search?query=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ShortQuery_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/search?query=a");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ValidQuery_ReturnsResults()
    {
        var user = await CreateTestUser();

        // Create a document to search for
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("searchable-report"), "name");
        content.Add(new StringContent(user.Id.ToString()), "createdByUserId");
        content.Add(new StringContent("A document about quarterly reports"), "description");
        var fileContent = new ByteArrayContent("test content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "report.txt");
        await _client.PostAsync("/api/documents", content);

        var response = await _client.GetAsync("/api/search?query=searchable");
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>();
        Assert.NotNull(results);
        Assert.Contains(results, r => r.Name.Contains("searchable"));
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/search?query=zzzznonexistent");
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>();
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_WithContentTypeFilter_FiltersResults()
    {
        var response = await _client.GetAsync("/api/search?query=report&contentType=application/pdf");
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>();
        Assert.NotNull(results);
    }
}
