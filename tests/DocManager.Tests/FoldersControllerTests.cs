using System.Net;
using System.Net.Http.Json;
using DocManager.DTOs;

namespace DocManager.Tests;

public class FoldersControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FoldersControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<UserDto> CreateTestUser(string suffix = "")
    {
        var dto = new CreateUserDto($"folderuser{suffix}{Guid.NewGuid():N}", $"folder{suffix}{Guid.NewGuid():N}@test.com", "Folder User");
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    [Fact]
    public async Task GetAll_ReturnsRootFolders()
    {
        var response = await _client.GetAsync("/api/folders");
        response.EnsureSuccessStatusCode();
        var folders = await response.Content.ReadFromJsonAsync<List<FolderDto>>();
        Assert.NotNull(folders);
    }

    [Fact]
    public async Task Create_ValidFolder_ReturnsCreated()
    {
        var user = await CreateTestUser("create");
        var dto = new CreateFolderDto("Test Folder", "A test folder", null, user.Id);
        var response = await _client.PostAsJsonAsync("/api/folders", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var folder = await response.Content.ReadFromJsonAsync<FolderDto>();
        Assert.NotNull(folder);
        Assert.Equal("Test Folder", folder.Name);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        var user = await CreateTestUser("empty");
        var dto = new CreateFolderDto("", null, null, user.Id);
        var response = await _client.PostAsJsonAsync("/api/folders", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NestedFolder_ReturnsCreated()
    {
        var user = await CreateTestUser("nested");
        var parentDto = new CreateFolderDto($"Parent{Guid.NewGuid():N}", null, null, user.Id);
        var parentResponse = await _client.PostAsJsonAsync("/api/folders", parentDto);
        var parent = await parentResponse.Content.ReadFromJsonAsync<FolderDto>();

        var childDto = new CreateFolderDto("Child", null, parent!.Id, user.Id);
        var response = await _client.PostAsJsonAsync("/api/folders", childDto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var child = await response.Content.ReadFromJsonAsync<FolderDto>();
        Assert.Equal(parent.Id, child!.ParentFolderId);
    }

    [Fact]
    public async Task Create_DuplicateNameInSameParent_ReturnsConflict()
    {
        var user = await CreateTestUser("dup");
        var name = $"DupFolder{Guid.NewGuid():N}";
        var dto = new CreateFolderDto(name, null, null, user.Id);
        await _client.PostAsJsonAsync("/api/folders", dto);

        var response = await _client.PostAsJsonAsync("/api/folders", dto);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetTree_ReturnsHierarchy()
    {
        var response = await _client.GetAsync("/api/folders/tree");
        response.EnsureSuccessStatusCode();
        var tree = await response.Content.ReadFromJsonAsync<List<FolderTreeDto>>();
        Assert.NotNull(tree);
    }

    [Fact]
    public async Task Update_ValidFolder_ReturnsOk()
    {
        var user = await CreateTestUser("upd");
        var createDto = new CreateFolderDto($"UpdateMe{Guid.NewGuid():N}", null, null, user.Id);
        var createResponse = await _client.PostAsJsonAsync("/api/folders", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<FolderDto>();

        var updateDto = new UpdateFolderDto("Updated Name", "New desc", null);
        var response = await _client.PutAsJsonAsync($"/api/folders/{created!.Id}?requestingUserId={user.Id}", updateDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_EmptyFolder_ReturnsNoContent()
    {
        var user = await CreateTestUser("del");
        var dto = new CreateFolderDto($"DeleteMe{Guid.NewGuid():N}", null, null, user.Id);
        var createResponse = await _client.PostAsJsonAsync("/api/folders", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<FolderDto>();

        var response = await _client.DeleteAsync($"/api/folders/{created!.Id}?requestingUserId={user.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetPath_ReturnsAncestors()
    {
        var user = await CreateTestUser("path");
        var rootDto = new CreateFolderDto($"Root{Guid.NewGuid():N}", null, null, user.Id);
        var rootResponse = await _client.PostAsJsonAsync("/api/folders", rootDto);
        var root = await rootResponse.Content.ReadFromJsonAsync<FolderDto>();

        var childDto = new CreateFolderDto("PathChild", null, root!.Id, user.Id);
        var childResponse = await _client.PostAsJsonAsync("/api/folders", childDto);
        var child = await childResponse.Content.ReadFromJsonAsync<FolderDto>();

        var response = await _client.GetAsync($"/api/folders/{child!.Id}/path");
        response.EnsureSuccessStatusCode();
        var path = await response.Content.ReadFromJsonAsync<List<FolderDto>>();
        Assert.Equal(2, path!.Count);
    }
}
