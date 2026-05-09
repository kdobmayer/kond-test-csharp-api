using System.Net;
using System.Net.Http.Json;
using DocManager.DTOs;

namespace DocManager.Tests;

public class UsersControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);
    }

    [Fact]
    public async Task Create_ValidUser_ReturnsCreated()
    {
        var dto = new CreateUserDto("testuser", "test@example.com", "Test User");
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public async Task Create_EmptyUsername_ReturnsBadRequest()
    {
        var dto = new CreateUserDto("", "test@example.com", "Test");
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateUsername_ReturnsConflict()
    {
        var dto = new CreateUserDto("dupuser", "dup1@example.com", "Dup 1");
        await _client.PostAsJsonAsync("/api/users", dto);

        var dto2 = new CreateUserDto("dupuser", "dup2@example.com", "Dup 2");
        var response = await _client.PostAsJsonAsync("/api/users", dto2);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/users/9999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidUser_ReturnsOk()
    {
        var createDto = new CreateUserDto("updateuser", "update@example.com", "Update User");
        var createResponse = await _client.PostAsJsonAsync("/api/users", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var updateDto = new UpdateUserDto("updatedname", null, null, null);
        var response = await _client.PutAsJsonAsync($"/api/users/{created!.Id}", updateDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("updatedname", updated!.Username);
    }

    [Fact]
    public async Task Delete_UserWithNoContent_ReturnsNoContent()
    {
        var dto = new CreateUserDto("deleteuser", "delete@example.com", "Delete User");
        var createResponse = await _client.PostAsJsonAsync("/api/users", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var response = await _client.DeleteAsync($"/api/users/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
