using DocManager.Data;
using DocManager.Models;
using DocManager.Services;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Tests;

public class DocumentShareServiceTests
{
    private AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ShareTest_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private async Task<(User owner, User other, Document doc)> SeedAsync(AppDbContext db)
    {
        var owner = new User { Username = $"owner_{Guid.NewGuid():N}", Email = $"owner_{Guid.NewGuid():N}@t.com", DisplayName = "Owner" };
        var other = new User { Username = $"other_{Guid.NewGuid():N}", Email = $"other_{Guid.NewGuid():N}@t.com", DisplayName = "Other" };
        db.Users.AddRange(owner, other);
        await db.SaveChangesAsync();

        var doc = new Document
        {
            Name = "test.txt",
            ContentType = "text/plain",
            StoragePath = "test.txt",
            CreatedByUserId = owner.Id
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        return (owner, other, doc);
    }

    [Fact]
    public async Task ShareDocument_CreatesShare()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);
        var svc = new DocumentShareService(db);

        var share = await svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "read");

        Assert.NotNull(share);
        Assert.Equal(doc.Id, share.DocumentId);
        Assert.Equal(other.Id, share.SharedWithUserId);
        Assert.Equal(owner.Id, share.SharedByUserId);
        Assert.Equal("read", share.Permission);
    }

    [Fact]
    public async Task ShareDocument_Duplicate_ReturnsNull()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);
        var svc = new DocumentShareService(db);

        await svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "read");
        var duplicate = await svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "write");

        Assert.Null(duplicate);
        Assert.Equal(1, await db.DocumentShares.CountAsync());
    }

    [Fact]
    public async Task RevokeShare_ExistingShare_ReturnsTrue()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);
        var svc = new DocumentShareService(db);

        await svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "read");
        var result = await svc.RevokeShareAsync(doc.Id, other.Id);

        Assert.True(result);
        Assert.Equal(0, await db.DocumentShares.CountAsync());
    }

    [Fact]
    public async Task RevokeShare_NonExistent_ReturnsFalse()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);
        var svc = new DocumentShareService(db);

        var result = await svc.RevokeShareAsync(doc.Id, other.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task GetSharesForDocument_ReturnsOnlyDocumentShares()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);

        var otherDoc = new Document { Name = "other.txt", ContentType = "text/plain", StoragePath = "other.txt", CreatedByUserId = owner.Id };
        db.Documents.Add(otherDoc);
        await db.SaveChangesAsync();

        var svc = new DocumentShareService(db);
        await svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "read");
        await svc.ShareDocumentAsync(otherDoc.Id, other.Id, owner.Id, "write");

        var shares = await svc.GetSharesForDocumentAsync(doc.Id);

        Assert.Single(shares);
        Assert.Equal(doc.Id, shares[0].DocumentId);
        Assert.NotNull(shares[0].Document);
        Assert.NotNull(shares[0].SharedWith);
        Assert.NotNull(shares[0].SharedBy);
    }

    [Fact]
    public async Task GetDocumentsSharedWithUser_ReturnsOnlyUserShares()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);

        var thirdUser = new User { Username = $"third_{Guid.NewGuid():N}", Email = $"third_{Guid.NewGuid():N}@t.com", DisplayName = "Third" };
        db.Users.Add(thirdUser);
        await db.SaveChangesAsync();

        var svc = new DocumentShareService(db);
        await svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "read");
        await svc.ShareDocumentAsync(doc.Id, thirdUser.Id, owner.Id, "write");

        var shares = await svc.GetDocumentsSharedWithUserAsync(other.Id);

        Assert.Single(shares);
        Assert.Equal(other.Id, shares[0].SharedWithUserId);
        Assert.NotNull(shares[0].Document);
        Assert.NotNull(shares[0].SharedWith);
        Assert.NotNull(shares[0].SharedBy);
    }

    [Fact]
    public async Task GetSharesForDocument_EmptyList_WhenNoShares()
    {
        await using var db = CreateDb();
        var (_, _, doc) = await SeedAsync(db);
        var svc = new DocumentShareService(db);

        var shares = await svc.GetSharesForDocumentAsync(doc.Id);

        Assert.Empty(shares);
    }

    [Fact]
    public async Task ShareDocument_InvalidPermission_Throws()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);
        var svc = new DocumentShareService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "admin"));
    }

    [Fact]
    public async Task ShareDocument_InactiveRecipient_Throws()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);
        other.IsActive = false;
        await db.SaveChangesAsync();
        var svc = new DocumentShareService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "read"));
    }

    [Fact]
    public async Task ShareDocument_ReturnsShareWithMappedNavigationData()
    {
        await using var db = CreateDb();
        var (owner, other, doc) = await SeedAsync(db);
        var svc = new DocumentShareService(db);

        var share = await svc.ShareDocumentAsync(doc.Id, other.Id, owner.Id, "WRITE");

        Assert.NotNull(share);
        Assert.Equal(doc.Name, share.Document.Name);
        Assert.Equal(other.DisplayName, share.SharedWith.DisplayName);
        Assert.Equal(owner.DisplayName, share.SharedBy.DisplayName);
        Assert.Equal("write", share.Permission);
    }
}
