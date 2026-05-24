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

    public DocumentsController(AppDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
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
    public async Task<ActionResult<DocumentDto>> GetById(int id)
    {
        var doc = await _db.Documents
            .Include(d => d.Folder)
            .Include(d => d.CreatedBy)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            return NotFound(new { message = "Document not found" });

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
            Name = name,
            StoragePath = storagePath,
            FileSize = file.Length,
            ContentType = file.ContentType,
            ChangeNote = "Initial upload",
            CreatedByUserId = createdByUserId
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

        var currentName = doc.Name;
        if (name != null) doc.Name = name;
        if (description != null) doc.Description = description;
        if (folderId.HasValue) doc.FolderId = folderId;

        if (file != null && file.Length > 0)
        {
            var currentVersionRecord = await _db.DocumentVersions
                .FirstOrDefaultAsync(v => v.DocumentId == doc.Id && v.VersionNumber == doc.Version);

            if (currentVersionRecord == null)
                return Conflict(new { message = "Current document version record not found" });

            // Preserve the current live file as an immutable archived version before replacing it.
            var archivedStoragePath = await _fileStorage.CopyFileAsync(doc.StoragePath, $"v{doc.Version}_{currentName}");
            currentVersionRecord.Name = currentName;
            currentVersionRecord.StoragePath = archivedStoragePath;

            await using var stream = file.OpenReadStream();
            var newStoragePath = await _fileStorage.SaveFileAsync(stream, file.FileName, file.ContentType);
            var newVersionNumber = doc.Version + 1;
            var versionRecord = new DocumentVersion
            {
                DocumentId = doc.Id,
                VersionNumber = newVersionNumber,
                Name = doc.Name,
                StoragePath = newStoragePath,
                FileSize = file.Length,
                ContentType = file.ContentType,
                ChangeNote = changeNote ?? $"Version {newVersionNumber}",
                CreatedByUserId = requestingUserId
            };
            _db.DocumentVersions.Add(versionRecord);

            doc.StoragePath = newStoragePath;
            doc.FileSize = file.Length;
            doc.ContentType = file.ContentType;
            doc.Version = newVersionNumber;
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
    public async Task<ActionResult> Download(int id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var stream = await _fileStorage.GetFileAsync(doc.StoragePath);
        return File(stream, doc.ContentType, doc.Name);
    }

    [HttpGet("{id}/versions")]
    public async Task<ActionResult<List<DocumentVersionDetailDto>>> GetVersions(int id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var versions = await _db.DocumentVersions
            .Include(v => v.CreatedBy)
            .Where(v => v.DocumentId == id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();

        return Ok(versions.Select(MappingService.ToDetailDto).ToList());
    }

    [HttpGet("{id}/versions/{v1}/compare/{v2}")]
    public async Task<ActionResult<VersionComparisonDto>> CompareVersions(int id, int v1, int v2)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        var version1 = await _db.DocumentVersions
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.DocumentId == id && v.VersionNumber == v1);
        if (version1 == null)
            return NotFound(new { message = $"Version {v1} not found" });

        var version2 = await _db.DocumentVersions
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.DocumentId == id && v.VersionNumber == v2);
        if (version2 == null)
            return NotFound(new { message = $"Version {v2} not found" });

        var changes = new List<VersionFieldChangeDto>();

        if (version1.Name != version2.Name)
            changes.Add(new VersionFieldChangeDto("name", version1.Name, version2.Name));
        if (version1.ContentType != version2.ContentType)
            changes.Add(new VersionFieldChangeDto("contentType", version1.ContentType, version2.ContentType));
        if (version1.FileSize != version2.FileSize)
            changes.Add(new VersionFieldChangeDto("fileSize", version1.FileSize.ToString(), version2.FileSize.ToString()));

        return Ok(new VersionComparisonDto(
            id,
            v1,
            v2,
            version1.CreatedAt,
            version2.CreatedAt,
            version1.CreatedByUserId,
            version1.CreatedBy?.DisplayName,
            version2.CreatedByUserId,
            version2.CreatedBy?.DisplayName,
            changes));
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
}
