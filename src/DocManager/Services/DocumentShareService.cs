using DocManager.Data;
using DocManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Services;

public class DocumentShareService
{
    private readonly AppDbContext _db;
    private static readonly HashSet<string> AllowedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "read",
        "write"
    };

    public DocumentShareService(AppDbContext db)
    {
        _db = db;
    }

    // Returns the created share, or null if the share already exists.
    public async Task<DocumentShare?> ShareDocumentAsync(int documentId, int sharedWithUserId, int sharedByUserId, string permission)
    {
        if (!AllowedPermissions.Contains(permission))
            throw new ArgumentException("Permission must be either 'read' or 'write'.", nameof(permission));

        if (sharedWithUserId == sharedByUserId)
            throw new InvalidOperationException("Users cannot share documents with themselves.");

        var existing = await _db.DocumentShares
            .FindAsync(documentId, sharedWithUserId);
        if (existing != null)
            return null;

        var document = await _db.Documents.FindAsync(documentId);
        if (document == null)
            throw new InvalidOperationException("Document not found.");

        var sharedWith = await _db.Users.FindAsync(sharedWithUserId);
        if (sharedWith == null || !sharedWith.IsActive)
            throw new InvalidOperationException("Recipient user not found or inactive.");

        var sharedBy = await _db.Users.FindAsync(sharedByUserId);
        if (sharedBy == null || !sharedBy.IsActive)
            throw new InvalidOperationException("Sharing user not found or inactive.");

        var share = new DocumentShare
        {
            DocumentId = documentId,
            Document = document,
            SharedWithUserId = sharedWithUserId,
            SharedWith = sharedWith,
            SharedByUserId = sharedByUserId,
            SharedBy = sharedBy,
            Permission = permission.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        _db.DocumentShares.Add(share);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(share).State = EntityState.Detached;

            var duplicateExists = await _db.DocumentShares
                .AsNoTracking()
                .AnyAsync(ds => ds.DocumentId == documentId && ds.SharedWithUserId == sharedWithUserId);

            if (duplicateExists)
                return null;

            throw;
        }

        return share;
    }

    // Returns true if the share was found and removed, false if it did not exist.
    public async Task<bool> RevokeShareAsync(int documentId, int sharedWithUserId)
    {
        var share = await _db.DocumentShares.FindAsync(documentId, sharedWithUserId);
        if (share == null)
            return false;

        _db.DocumentShares.Remove(share);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<DocumentShare>> GetSharesForDocumentAsync(int documentId)
    {
        return await _db.DocumentShares
            .Where(ds => ds.DocumentId == documentId)
            .Include(ds => ds.Document)
            .Include(ds => ds.SharedWith)
            .Include(ds => ds.SharedBy)
            .ToListAsync();
    }

    public async Task<List<DocumentShare>> GetDocumentsSharedWithUserAsync(int userId)
    {
        return await _db.DocumentShares
            .Where(ds => ds.SharedWithUserId == userId)
            .Include(ds => ds.Document)
            .Include(ds => ds.SharedWith)
            .Include(ds => ds.SharedBy)
            .ToListAsync();
    }
}
