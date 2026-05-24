using System.Net;
using System.Net.Http.Json;
using DocManager.DTOs;

namespace DocManager.Tests;

public class DocumentSharesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentSharesControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<UserDto> CreateTestUser(string suffix = "")
    {
        var dto = new CreateUserDto(
            $"shareuser{suffix}{Guid.NewGuid():N}",
            $"share{suffix}{Guid.NewGuid():N}@test.com",
            "Share User");
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    private async Task<DocumentDto> UploadDocument(int userId, string name = "doc.txt")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(userId.ToString()), "createdByUserId");
        var fileContent = new ByteArrayContent("test content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", name);
        var response = await _client.PostAsync("/api/documents", content);
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    private async Task ShareDocument(int docId, int ownerId, int recipientId, string permission = "read")
    {
        var dto = new CreateShareRequestDto(recipientId, permission);
        await _client.PostAsJsonAsync($"/api/documents/{docId}/shares?requestingUserId={ownerId}", dto);
    }

    // POST /api/documents/{id}/shares

    [Fact]
    public async Task CreateShare_OwnerSharesWithReadPermission_ReturnsCreated()
    {
        var owner = await CreateTestUser("own1");
        var recipient = await CreateTestUser("rec1");
        var doc = await UploadDocument(owner.Id);

        var dto = new CreateShareRequestDto(recipient.Id, "read");
        var response = await _client.PostAsJsonAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var share = await response.Content.ReadFromJsonAsync<DocumentShareDto>();
        Assert.NotNull(share);
        Assert.Equal(doc.Id, share.DocumentId);
        Assert.Equal(recipient.Id, share.SharedWithUserId);
        Assert.Equal(owner.Id, share.SharedByUserId);
        Assert.Equal("read", share.Permission);
    }

    [Fact]
    public async Task CreateShare_OwnerSharesWithWritePermission_ReturnsCreated()
    {
        var owner = await CreateTestUser("own2");
        var recipient = await CreateTestUser("rec2");
        var doc = await UploadDocument(owner.Id);

        var dto = new CreateShareRequestDto(recipient.Id, "write");
        var response = await _client.PostAsJsonAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var share = await response.Content.ReadFromJsonAsync<DocumentShareDto>();
        Assert.Equal("write", share!.Permission);
    }

    [Fact]
    public async Task CreateShare_NonOwner_ReturnsForbidden()
    {
        var owner = await CreateTestUser("own3");
        var nonOwner = await CreateTestUser("non3");
        var recipient = await CreateTestUser("rec3");
        var doc = await UploadDocument(owner.Id);

        var dto = new CreateShareRequestDto(recipient.Id, "read");
        var response = await _client.PostAsJsonAsync($"/api/documents/{doc.Id}/shares?requestingUserId={nonOwner.Id}", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateShare_DuplicateShare_ReturnsConflict()
    {
        var owner = await CreateTestUser("own4");
        var recipient = await CreateTestUser("rec4");
        var doc = await UploadDocument(owner.Id);

        var dto = new CreateShareRequestDto(recipient.Id, "read");
        await _client.PostAsJsonAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}", dto);
        var response = await _client.PostAsJsonAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}", dto);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateShare_InvalidPermission_ReturnsBadRequest()
    {
        var owner = await CreateTestUser("own5");
        var recipient = await CreateTestUser("rec5");
        var doc = await UploadDocument(owner.Id);

        var dto = new CreateShareRequestDto(recipient.Id, "admin");
        var response = await _client.PostAsJsonAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateShare_NonExistentRecipient_ReturnsNotFound()
    {
        var owner = await CreateTestUser("own6");
        var doc = await UploadDocument(owner.Id);

        var dto = new CreateShareRequestDto(99999, "read");
        var response = await _client.PostAsJsonAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateShare_NonExistentDocument_ReturnsNotFound()
    {
        var owner = await CreateTestUser("own7");
        var recipient = await CreateTestUser("rec7");

        var dto = new CreateShareRequestDto(recipient.Id, "read");
        var response = await _client.PostAsJsonAsync($"/api/documents/99999/shares?requestingUserId={owner.Id}", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/documents/{id}/shares

    [Fact]
    public async Task GetShares_Owner_ReturnsShareList()
    {
        var owner = await CreateTestUser("own8");
        var recipient = await CreateTestUser("rec8");
        var doc = await UploadDocument(owner.Id);
        await ShareDocument(doc.Id, owner.Id, recipient.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shares = await response.Content.ReadFromJsonAsync<List<DocumentShareDto>>();
        Assert.NotNull(shares);
        Assert.Single(shares);
        Assert.Equal(recipient.Id, shares[0].SharedWithUserId);
        Assert.Equal(owner.Id, shares[0].SharedByUserId);
    }

    [Fact]
    public async Task GetShares_Owner_NoShares_ReturnsEmptyList()
    {
        var owner = await CreateTestUser("own9");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shares = await response.Content.ReadFromJsonAsync<List<DocumentShareDto>>();
        Assert.NotNull(shares);
        Assert.Empty(shares);
    }

    [Fact]
    public async Task GetShares_NonOwner_ReturnsForbidden()
    {
        var owner = await CreateTestUser("own10");
        var nonOwner = await CreateTestUser("non10");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/shares?requestingUserId={nonOwner.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetShares_NonExistentDocument_ReturnsNotFound()
    {
        var owner = await CreateTestUser("own11");

        var response = await _client.GetAsync($"/api/documents/99999/shares?requestingUserId={owner.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/documents/{id}/shares/{userId}

    [Fact]
    public async Task RevokeShare_OwnerRevokesExistingShare_ReturnsNoContent()
    {
        var owner = await CreateTestUser("own12");
        var recipient = await CreateTestUser("rec12");
        var doc = await UploadDocument(owner.Id);
        await ShareDocument(doc.Id, owner.Id, recipient.Id);

        var response = await _client.DeleteAsync($"/api/documents/{doc.Id}/shares/{recipient.Id}?requestingUserId={owner.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RevokeShare_NonExistentShare_ReturnsNotFound()
    {
        var owner = await CreateTestUser("own13");
        var other = await CreateTestUser("oth13");
        var doc = await UploadDocument(owner.Id);

        var response = await _client.DeleteAsync($"/api/documents/{doc.Id}/shares/{other.Id}?requestingUserId={owner.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeShare_NonOwner_ReturnsForbidden()
    {
        var owner = await CreateTestUser("own14");
        var nonOwner = await CreateTestUser("non14");
        var recipient = await CreateTestUser("rec14");
        var doc = await UploadDocument(owner.Id);
        await ShareDocument(doc.Id, owner.Id, recipient.Id);

        var response = await _client.DeleteAsync($"/api/documents/{doc.Id}/shares/{recipient.Id}?requestingUserId={nonOwner.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RevokeShare_AfterRevoke_ShareNoLongerListed()
    {
        var owner = await CreateTestUser("own15");
        var recipient = await CreateTestUser("rec15");
        var doc = await UploadDocument(owner.Id);
        await ShareDocument(doc.Id, owner.Id, recipient.Id);

        await _client.DeleteAsync($"/api/documents/{doc.Id}/shares/{recipient.Id}?requestingUserId={owner.Id}");

        var response = await _client.GetAsync($"/api/documents/{doc.Id}/shares?requestingUserId={owner.Id}");
        var shares = await response.Content.ReadFromJsonAsync<List<DocumentShareDto>>();
        Assert.Empty(shares!);
    }
}
