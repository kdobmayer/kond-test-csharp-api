using DocManager.Data;
using DocManager.DTOs;
using DocManager.Models;
using DocManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TagsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<TagWithCountDto>>> GetAll()
    {
        var tags = await _db.Tags
            .Include(t => t.DocumentTags)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Ok(tags.Select(MappingService.ToCountDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TagWithCountDto>> GetById(int id)
    {
        var tag = await _db.Tags
            .Include(t => t.DocumentTags)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag == null)
            return NotFound(new { message = "Tag not found" });

        return Ok(MappingService.ToCountDto(tag));
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create([FromBody] CreateTagDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Name is required" });

        var existing = await _db.Tags.AnyAsync(t => t.Name == dto.Name);
        if (existing)
            return Conflict(new { message = "A tag with this name already exists" });

        var tag = new Tag
        {
            Name = dto.Name,
            Color = dto.Color
        };

        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, MappingService.ToDto(tag));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TagDto>> Update(int id, [FromBody] UpdateTagDto dto)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag == null)
            return NotFound(new { message = "Tag not found" });

        if (dto.Name != null)
        {
            var duplicate = await _db.Tags.AnyAsync(t => t.Name == dto.Name && t.Id != id);
            if (duplicate)
                return Conflict(new { message = "A tag with this name already exists" });
            tag.Name = dto.Name;
        }

        if (dto.Color != null) tag.Color = dto.Color;

        await _db.SaveChangesAsync();
        return Ok(MappingService.ToDto(tag));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var tag = await _db.Tags
            .Include(t => t.DocumentTags)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag == null)
            return NotFound(new { message = "Tag not found" });

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/documents")]
    public async Task<ActionResult<List<DocumentDto>>> GetDocuments(int id)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag == null)
            return NotFound(new { message = "Tag not found" });

        var documents = await _db.DocumentTags
            .Where(dt => dt.TagId == id)
            .Include(dt => dt.Document)
            .ThenInclude(d => d.Folder)
            .Include(dt => dt.Document)
            .ThenInclude(d => d.CreatedBy)
            .Include(dt => dt.Document)
            .ThenInclude(d => d.DocumentTags)
            .ThenInclude(dt2 => dt2.Tag)
            .Select(dt => dt.Document)
            .ToListAsync();

        return Ok(documents.Select(MappingService.ToDto).ToList());
    }
}
