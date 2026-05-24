using DocManager.Data;
using DocManager.DTOs;
using DocManager.Models;
using DocManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly DocumentShareService _sharingService;

    public DocumentsController(AppDbContext db, IFileStorageService fileStorage, DocumentShareService sharingService)
    {
        _db = db;
        _fileStorage = fileStorage;
        _sharingService = sharingService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetAll([FromQuery] int? folderId)
    {
        var query = _db.Documents
            .Include(d => d.Folder)
            .Include(d => d.CreatedBy)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .AsQueryable();

        if (folderId.HasValue)
            query = query.Where(d => d.FolderId == folderId.Value);

        var documents = await query.OrderByDescending(d => d.UpdatedAt).ToListAsync();
        return Ok(documents.Select(MappingService.ToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDto>> GetById(int id, [FromQuery] int? requestingUserId)
    {
        var doc = await _db.Documents
            .Include(d => d.Folder)
            .Include(d => d.CreatedBy)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            return NotFound(new { message = "Document not found" });

        if (!requestingUserId.HasValue)
            return BadRequest(new { message = "Requesting user ID is required" });

        var user = await _db.Users.FindAsync(requestingUserId.Value);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        if (doc.CreatedByUserId != requestingUserId.Value)
        {
            var share = await _db.DocumentShares.FirstOrDefaultAsync(
                ds => ds.DocumentId == id && ds.SharedWithUserId == requestingUserId.Value);
            if (share == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });
        }

        return Ok(MappingService.ToDto(doc));
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Upload(
        [FromForm] string name,
        [FromForm] string? description,
        [FromForm] int? folderId,
        [FromForm] int createdByUserId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required" });

        // Duplicated authorization check (intentional rough edge)
        var user = await _db.Users.FindAsync(createdByUserId);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return Forbid();

        if (folderId.HasValue)
        {
            var folder = await _db.Folders.FindAsync(folderId.Value);
            if (folder == null)
                return BadRequest(new { message = "Folder not found" });

            // Duplicated authorization check (intentional rough edge)
            if (folder.CreatedByUserId != createdByUserId)
            {
                var folderUser = await _db.Users.FindAsync(createdByUserId);
                if (folderUser == null || !folderUser.IsActive)
                    return Forbid();
            }
        }

        await using var stream = file.OpenReadStream();
        var storagePath = await _fileStorage.SaveFileAsync(stream, file.FileName, file.ContentType);

        var document = new Document
        {
            Name = name,
            Description = description,
            ContentType = file.ContentType,
            FileSize = file.Length,
            StoragePath = storagePath,
            FolderId = folderId,
            CreatedByUserId = createdByUserId,
            Version = 1
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        // Create initial version record
        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = 1,
            StoragePath = storagePath,
            FileSize = file.Length,
            ContentType = file.ContentType,
            ChangeNote = "Initial upload"
        };
        _db.DocumentVersions.Add(version);
        await _db.SaveChangesAsync();

        var created = await _db.Documents
            .Include(d => d.Folder)
            .Include(d => d.CreatedBy)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .FirstAsync(d => d.Id == document.Id);

        return CreatedAtAction(nameof(GetById), new { id = document.Id }, MappingService.ToDto(created));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DocumentDto>> Update(int id,
        [FromForm] string? name,
        [FromForm] string? description,
        [FromForm] int? folderId,
        [FromForm] string? changeNote,
        [FromForm] int requestingUserId,
        IFormFile? file)
    {
        var doc = await _db.Documents
            .Include(d => d.Folder)
            .Include(d => d.CreatedBy)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            return NotFound(new { message = "Document not found" });

        // Duplicated authorization check (intentional rough edge)
        var user = await _db.Users.FindAsync(requestingUserId);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return Forbid();

        if (doc.CreatedByUserId != requestingUserId)
        {
            var share = await _db.DocumentShares.FirstOrDefaultAsync(
                ds => ds.DocumentId == id && ds.SharedWithUserId == requestingUserId);
            if (share == null || !string.Equals(share.Permission, "write", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });
        }

        if (name != null) doc.Name = name;
        if (description != null) doc.Description = description;
        if (folderId.HasValue) doc.FolderId = folderId;

        if (file != null && file.Length > 0)
        {
            // Save current version before overwriting (versioning)
            var versionCopy = await _fileStorage.CopyFileAsync(doc.StoragePath, $"v{doc.Version}_{doc.Name}");
            var versionRecord = new DocumentVersion
            {
                DocumentId = doc.Id,
                VersionNumber = doc.Version,
                StoragePath = versionCopy,
                FileSize = doc.FileSize,
                ContentType = doc.ContentType,
                ChangeNote = changeNote ?? $"Version {doc.Version} archived"
            };
            _db.DocumentVersions.Add(versionRecord);

            // Upload new file
            await _fileStorage.DeleteFileAsync(doc.StoragePath);
            await using var stream = file.OpenReadStream();
            doc.StoragePath = await _fileStorage.SaveFileAsync(stream, file.FileName, file.ContentType);
            doc.FileSize = file.Length;
            doc.ContentType = file.ContentType;
            doc.Version++;
        }

        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MappingService.ToDto(doc));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, [FromQuery] int requestingUserId)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        // Duplicated authorization check (intentional rough edge)
        var user = await _db.Users.FindAsync(requestingUserId);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return Forbid();

        // Delete all version files
        var versions = await _db.DocumentVersions.Where(v => v.DocumentId == id).ToListAsync();
        foreach (var version in versions)
        {
            await _fileStorage.DeleteFileAsync(version.StoragePath);
        }

        await _fileStorage.DeleteFileAsync(doc.StoragePath);
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<ActionResult> Download(int id, [FromQuery] int? requestingUserId)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        if (!requestingUserId.HasValue)
            return BadRequest(new { message = "Requesting user ID is required" });

        var user = await _db.Users.FindAsync(requestingUserId.Value);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        if (doc.CreatedByUserId != requestingUserId.Value)
        {
            var share = await _db.DocumentShares.FirstOrDefaultAsync(
                ds => ds.DocumentId == id && ds.SharedWithUserId == requestingUserId.Value);
            if (share == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });
        }

        var stream = await _fileStorage.GetFileAsync(doc.StoragePath);
        return File(stream, doc.ContentType, doc.Name);
    }

    [HttpGet("{id}/versions")]
    public async Task<ActionResult<List<DocumentVersionDto>>> GetVersions(int id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var versions = await _db.DocumentVersions
            .Where(v => v.DocumentId == id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();

        return Ok(versions.Select(MappingService.ToDto).ToList());
    }

    [HttpGet("{id}/versions/{versionNumber}/download")]
    public async Task<ActionResult> DownloadVersion(int id, int versionNumber)
    {
        var version = await _db.DocumentVersions
            .FirstOrDefaultAsync(v => v.DocumentId == id && v.VersionNumber == versionNumber);

        if (version == null)
            return NotFound(new { message = "Version not found" });

        var stream = await _fileStorage.GetFileAsync(version.StoragePath);
        return File(stream, version.ContentType, $"v{version.VersionNumber}_{version.DocumentId}");
    }

    [HttpPost("{id}/tags/{tagId}")]
    public async Task<ActionResult> AddTag(int id, int tagId)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var tag = await _db.Tags.FindAsync(tagId);
        if (tag == null)
            return NotFound(new { message = "Tag not found" });

        var existing = await _db.DocumentTags
            .FirstOrDefaultAsync(dt => dt.DocumentId == id && dt.TagId == tagId);
        if (existing != null)
            return Conflict(new { message = "Tag already assigned" });

        _db.DocumentTags.Add(new DocumentTag { DocumentId = id, TagId = tagId });
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}/tags/{tagId}")]
    public async Task<ActionResult> RemoveTag(int id, int tagId)
    {
        var docTag = await _db.DocumentTags
            .FirstOrDefaultAsync(dt => dt.DocumentId == id && dt.TagId == tagId);

        if (docTag == null)
            return NotFound(new { message = "Tag assignment not found" });

        _db.DocumentTags.Remove(docTag);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/shares")]
    public async Task<ActionResult<DocumentShareDto>> CreateShare(
        int id,
        [FromBody] CreateShareRequestDto dto,
        [FromQuery] int requestingUserId)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var requestingUser = await _db.Users.FindAsync(requestingUserId);
        if (requestingUser == null)
            return BadRequest(new { message = "User not found" });
        if (!requestingUser.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        if (doc.CreatedByUserId != requestingUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        if (!string.Equals(dto.Permission, "read", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(dto.Permission, "write", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Permission must be 'read' or 'write'" });

        if (dto.SharedWithUserId == requestingUserId)
            return BadRequest(new { message = "Cannot share a document with yourself" });

        var sharedWithUser = await _db.Users.FindAsync(dto.SharedWithUserId);
        if (sharedWithUser == null || !sharedWithUser.IsActive)
            return NotFound(new { message = "User to share with not found or inactive" });

        DocumentShare? share;
        try
        {
            share = await _sharingService.ShareDocumentAsync(id, dto.SharedWithUserId, requestingUserId, dto.Permission);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Permission must be 'read' or 'write'" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "Users cannot share documents with themselves.")
        {
            return BadRequest(new { message = "Cannot share a document with yourself" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "Document not found.")
        {
            return NotFound(new { message = "Document not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "Recipient user not found or inactive.")
        {
            return NotFound(new { message = "User to share with not found or inactive" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "Sharing user not found or inactive.")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });
        }

        if (share == null)
            return Conflict(new { message = "Document is already shared with this user" });

        return CreatedAtAction(nameof(GetShares), new { id }, MappingService.ToDto(share));
    }

    [HttpGet("{id}/shares")]
    public async Task<ActionResult<List<DocumentShareDto>>> GetShares(
        int id,
        [FromQuery] int requestingUserId)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var requestingUser = await _db.Users.FindAsync(requestingUserId);
        if (requestingUser == null)
            return BadRequest(new { message = "User not found" });
        if (!requestingUser.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        if (doc.CreatedByUserId != requestingUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        var shares = await _sharingService.GetSharesForDocumentAsync(id);
        return Ok(shares.Select(MappingService.ToDto).ToList());
    }

    [HttpDelete("{id}/shares/{userId}")]
    public async Task<ActionResult> RevokeShare(
        int id,
        int userId,
        [FromQuery] int requestingUserId)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var requestingUser = await _db.Users.FindAsync(requestingUserId);
        if (requestingUser == null)
            return BadRequest(new { message = "User not found" });
        if (!requestingUser.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        if (doc.CreatedByUserId != requestingUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden" });

        var revoked = await _sharingService.RevokeShareAsync(id, userId);
        if (!revoked)
            return NotFound(new { message = "Share not found" });

        return NoContent();
    }
}
