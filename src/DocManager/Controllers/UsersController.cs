using DocManager.Data;
using DocManager.DTOs;
using DocManager.Models;
using DocManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll([FromQuery] bool? activeOnly)
    {
        var query = _db.Users.AsQueryable();

        if (activeOnly == true)
            query = query.Where(u => u.IsActive);

        var users = await query.OrderBy(u => u.Username).ToListAsync();
        return Ok(users.Select(MappingService.ToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(MappingService.ToDto(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username))
            return BadRequest(new { message = "Username is required" });
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email is required" });

        var existingUsername = await _db.Users.AnyAsync(u => u.Username == dto.Username);
        if (existingUsername)
            return Conflict(new { message = "Username already taken" });

        var existingEmail = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (existingEmail)
            return Conflict(new { message = "Email already in use" });

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            DisplayName = dto.DisplayName
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, MappingService.ToDto(user));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (dto.Username != null)
        {
            var duplicate = await _db.Users.AnyAsync(u => u.Username == dto.Username && u.Id != id);
            if (duplicate)
                return Conflict(new { message = "Username already taken" });
            user.Username = dto.Username;
        }

        if (dto.Email != null)
        {
            var duplicate = await _db.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id);
            if (duplicate)
                return Conflict(new { message = "Email already in use" });
            user.Email = dto.Email;
        }

        if (dto.DisplayName != null) user.DisplayName = dto.DisplayName;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MappingService.ToDto(user));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        // Check if user has documents or folders
        var hasDocuments = await _db.Documents.AnyAsync(d => d.CreatedByUserId == id);
        var hasFolders = await _db.Folders.AnyAsync(f => f.CreatedByUserId == id);

        if (hasDocuments || hasFolders)
            return Conflict(new { message = "Cannot delete user with existing documents or folders. Deactivate instead." });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/documents")]
    public async Task<ActionResult<List<DocumentDto>>> GetDocuments(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var documents = await _db.Documents
            .Where(d => d.CreatedByUserId == id)
            .Include(d => d.Folder)
            .Include(d => d.CreatedBy)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();

        return Ok(documents.Select(MappingService.ToDto).ToList());
    }

    [HttpGet("{id}/folders")]
    public async Task<ActionResult<List<FolderDto>>> GetFolders(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var folders = await _db.Folders
            .Where(f => f.CreatedByUserId == id)
            .Include(f => f.CreatedBy)
            .Include(f => f.Documents)
            .Include(f => f.Children)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return Ok(folders.Select(MappingService.ToDto).ToList());
    }
}
