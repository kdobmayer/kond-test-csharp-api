using DocManager.Data;
using DocManager.DTOs;
using DocManager.Models;
using DocManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly AppDbContext _db;

    public FoldersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<FolderDto>>> GetAll([FromQuery] int? parentId)
    {
        var query = _db.Folders
            .Include(f => f.CreatedBy)
            .Include(f => f.Documents)
            .Include(f => f.Children)
            .AsQueryable();

        if (parentId.HasValue)
            query = query.Where(f => f.ParentFolderId == parentId.Value);
        else
            query = query.Where(f => f.ParentFolderId == null);

        var folders = await query.OrderBy(f => f.Name).ToListAsync();
        return Ok(folders.Select(MappingService.ToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FolderDto>> GetById(int id)
    {
        var folder = await _db.Folders
            .Include(f => f.CreatedBy)
            .Include(f => f.Documents)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder == null)
            return NotFound(new { message = "Folder not found" });

        return Ok(MappingService.ToDto(folder));
    }

    [HttpGet("tree")]
    public async Task<ActionResult<List<FolderTreeDto>>> GetTree()
    {
        var rootFolders = await _db.Folders
            .Include(f => f.Documents)
            .Include(f => f.Children)
            .ThenInclude(c => c.Documents)
            .Include(f => f.Children)
            .ThenInclude(c => c.Children)
            .ThenInclude(gc => gc.Documents)
            .Where(f => f.ParentFolderId == null)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return Ok(rootFolders.Select(MappingService.ToTreeDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FolderDto>> Create([FromBody] CreateFolderDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Name is required" });

        // Duplicated authorization check (intentional rough edge)
        var user = await _db.Users.FindAsync(dto.CreatedByUserId);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return Forbid();

        if (dto.ParentFolderId.HasValue)
        {
            var parent = await _db.Folders.FindAsync(dto.ParentFolderId.Value);
            if (parent == null)
                return BadRequest(new { message = "Parent folder not found" });

            // Duplicated authorization check (intentional rough edge)
            if (parent.CreatedByUserId != dto.CreatedByUserId)
            {
                var parentUser = await _db.Users.FindAsync(dto.CreatedByUserId);
                if (parentUser == null || !parentUser.IsActive)
                    return Forbid();
            }
        }

        // Check for duplicate name in same parent
        var duplicate = await _db.Folders
            .AnyAsync(f => f.Name == dto.Name && f.ParentFolderId == dto.ParentFolderId);
        if (duplicate)
            return Conflict(new { message = "A folder with this name already exists in the same location" });

        var folder = new Folder
        {
            Name = dto.Name,
            Description = dto.Description,
            ParentFolderId = dto.ParentFolderId,
            CreatedByUserId = dto.CreatedByUserId
        };

        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();

        var created = await _db.Folders
            .Include(f => f.CreatedBy)
            .Include(f => f.Documents)
            .Include(f => f.Children)
            .FirstAsync(f => f.Id == folder.Id);

        return CreatedAtAction(nameof(GetById), new { id = folder.Id }, MappingService.ToDto(created));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FolderDto>> Update(int id, [FromBody] UpdateFolderDto dto, [FromQuery] int requestingUserId)
    {
        var folder = await _db.Folders
            .Include(f => f.CreatedBy)
            .Include(f => f.Documents)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder == null)
            return NotFound(new { message = "Folder not found" });

        // Duplicated authorization check (intentional rough edge)
        var user = await _db.Users.FindAsync(requestingUserId);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return Forbid();

        if (dto.Name != null)
        {
            var duplicate = await _db.Folders
                .AnyAsync(f => f.Name == dto.Name && f.ParentFolderId == folder.ParentFolderId && f.Id != id);
            if (duplicate)
                return Conflict(new { message = "A folder with this name already exists in the same location" });
            folder.Name = dto.Name;
        }

        if (dto.Description != null) folder.Description = dto.Description;

        if (dto.ParentFolderId.HasValue && dto.ParentFolderId != folder.ParentFolderId)
        {
            // Prevent circular reference
            if (dto.ParentFolderId.Value == id)
                return BadRequest(new { message = "Cannot move folder into itself" });

            if (await IsDescendant(id, dto.ParentFolderId.Value))
                return BadRequest(new { message = "Cannot move folder into its own descendant" });

            folder.ParentFolderId = dto.ParentFolderId;
        }

        folder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MappingService.ToDto(folder));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, [FromQuery] int requestingUserId, [FromQuery] bool recursive = false)
    {
        var folder = await _db.Folders
            .Include(f => f.Children)
            .Include(f => f.Documents)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder == null)
            return NotFound(new { message = "Folder not found" });

        // Duplicated authorization check (intentional rough edge)
        var user = await _db.Users.FindAsync(requestingUserId);
        if (user == null)
            return BadRequest(new { message = "User not found" });
        if (!user.IsActive)
            return Forbid();

        if (!recursive && (folder.Children.Any() || folder.Documents.Any()))
            return Conflict(new { message = "Folder is not empty. Use recursive=true to delete with contents." });

        if (recursive)
        {
            await DeleteFolderRecursive(folder);
        }
        else
        {
            _db.Folders.Remove(folder);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/path")]
    public async Task<ActionResult<List<FolderDto>>> GetPath(int id)
    {
        var path = new List<Folder>();
        var current = await _db.Folders
            .Include(f => f.CreatedBy)
            .Include(f => f.Documents)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (current == null)
            return NotFound(new { message = "Folder not found" });

        while (current != null)
        {
            path.Insert(0, current);
            if (current.ParentFolderId.HasValue)
            {
                current = await _db.Folders
                    .Include(f => f.CreatedBy)
                    .Include(f => f.Documents)
                    .Include(f => f.Children)
                    .FirstOrDefaultAsync(f => f.Id == current.ParentFolderId);
            }
            else
            {
                current = null;
            }
        }

        return Ok(path.Select(MappingService.ToDto).ToList());
    }

    private async Task<bool> IsDescendant(int ancestorId, int potentialDescendantId)
    {
        var current = await _db.Folders.FindAsync(potentialDescendantId);
        while (current != null)
        {
            if (current.Id == ancestorId) return true;
            if (!current.ParentFolderId.HasValue) break;
            current = await _db.Folders.FindAsync(current.ParentFolderId.Value);
        }
        return false;
    }

    private async Task DeleteFolderRecursive(Folder folder)
    {
        var children = await _db.Folders
            .Include(f => f.Children)
            .Include(f => f.Documents)
            .Where(f => f.ParentFolderId == folder.Id)
            .ToListAsync();

        foreach (var child in children)
        {
            await DeleteFolderRecursive(child);
        }

        // Move documents to no folder (orphan them)
        var docs = await _db.Documents.Where(d => d.FolderId == folder.Id).ToListAsync();
        foreach (var doc in docs)
        {
            doc.FolderId = null;
        }

        _db.Folders.Remove(folder);
    }
}
