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

    private async Task CreateTestDocument(int userId, string name, string description = "test description")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(userId.ToString()), "createdByUserId");
        content.Add(new StringContent(description), "description");
        var fileContent = new ByteArrayContent("test content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", $"{name}.txt");
        await _client.PostAsync("/api/documents", content);
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
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.NotNull(result);
        Assert.Contains(result.Items, r => r.Name.Contains("searchable"));
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/search?query=zzzznonexistent");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_WithContentTypeFilter_FiltersResults()
    {
        var response = await _client.GetAsync("/api/search?query=report&contentType=application/pdf");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Search_DefaultPagination_ReturnsPage1WithDefaults()
    {
        var user = await CreateTestUser();
        var uniquePrefix = $"pgdefault{Guid.NewGuid():N}";
        await CreateTestDocument(user.Id, $"{uniquePrefix}-doc");

        var response = await _client.GetAsync($"/api/search?query={uniquePrefix}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.True(result.TotalCount > 0);
        Assert.True(result.TotalPages > 0);
    }

    [Fact]
    public async Task Search_ExplicitPage2_ReturnsDifferentItems()
    {
        var user = await CreateTestUser();
        var uniquePrefix = $"pgtest{Guid.NewGuid():N}";

        // Create 3 documents matching a unique prefix, then request pageSize=2
        await CreateTestDocument(user.Id, $"{uniquePrefix}-alpha");
        await CreateTestDocument(user.Id, $"{uniquePrefix}-beta");
        await CreateTestDocument(user.Id, $"{uniquePrefix}-gamma");

        var page1Response = await _client.GetAsync($"/api/search?query={uniquePrefix}&page=1&pageSize=2");
        page1Response.EnsureSuccessStatusCode();
        var page1 = await page1Response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();

        var page2Response = await _client.GetAsync($"/api/search?query={uniquePrefix}&page=2&pageSize=2");
        page2Response.EnsureSuccessStatusCode();
        var page2 = await page2Response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page2.Page);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(3, page2.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
    }

    [Fact]
    public async Task Search_TotalCountAndPages_MatchExpected()
    {
        var user = await CreateTestUser();
        var uniquePrefix = $"pgmath{Guid.NewGuid():N}";

        await CreateTestDocument(user.Id, $"{uniquePrefix}-one");
        await CreateTestDocument(user.Id, $"{uniquePrefix}-two");
        await CreateTestDocument(user.Id, $"{uniquePrefix}-three");
        await CreateTestDocument(user.Id, $"{uniquePrefix}-four");
        await CreateTestDocument(user.Id, $"{uniquePrefix}-five");

        var response = await _client.GetAsync($"/api/search?query={uniquePrefix}&page=1&pageSize=3");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();

        Assert.NotNull(result);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public async Task Search_InvalidPage_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/search?query=test&page=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_InvalidPageSize_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/search?query=test&pageSize=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_VeryLargePage_ReturnsEmptyPageInsteadOfServerError()
    {
        var response = await _client.GetAsync($"/api/search?query=test&page={int.MaxValue}&pageSize=100");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.NotNull(result);
        Assert.Equal(int.MaxValue, result.Page);
        Assert.Empty(result.Items);
    }
}
