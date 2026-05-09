using DocManager.Data;
using DocManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorage;

    public StatsController(AppDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var totalDocuments = await _db.Documents.CountAsync();
        var totalFolders = await _db.Folders.CountAsync();
        var totalTags = await _db.Tags.CountAsync();
        var totalUsers = await _db.Users.CountAsync();
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive);
        var totalVersions = await _db.DocumentVersions.CountAsync();
        var totalStorageBytes = await _db.Documents.SumAsync(d => d.FileSize);

        var recentDocuments = await _db.Documents
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .Include(d => d.CreatedBy)
            .Include(d => d.Folder)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .ToListAsync();

        var topTags = await _db.Tags
            .Include(t => t.DocumentTags)
            .OrderByDescending(t => t.DocumentTags.Count)
            .Take(5)
            .ToListAsync();

        var contentTypeBreakdown = await _db.Documents
            .GroupBy(d => d.ContentType)
            .Select(g => new ContentTypeStatDto(g.Key, g.Count(), g.Sum(d => d.FileSize)))
            .ToListAsync();

        var userActivity = await _db.Users
            .Where(u => u.IsActive)
            .Select(u => new UserActivityDto(
                u.Id,
                u.DisplayName,
                u.Documents.Count,
                u.Folders.Count,
                u.Documents.Any() ? u.Documents.Max(d => d.UpdatedAt) : u.CreatedAt))
            .OrderByDescending(ua => ua.LastActivity)
            .Take(10)
            .ToListAsync();

        return Ok(new DashboardStatsDto(
            totalDocuments,
            totalFolders,
            totalTags,
            totalUsers,
            activeUsers,
            totalVersions,
            totalStorageBytes,
            recentDocuments.Select(MappingService.ToDto).ToList(),
            topTags.Select(MappingService.ToCountDto).ToList(),
            contentTypeBreakdown,
            userActivity));
    }

    [HttpGet("storage")]
    public async Task<ActionResult<StorageStatsDto>> GetStorageStats()
    {
        var totalFiles = await _db.Documents.CountAsync();
        var totalVersionFiles = await _db.DocumentVersions.CountAsync();
        var totalDocumentBytes = await _db.Documents.SumAsync(d => d.FileSize);
        var totalVersionBytes = await _db.DocumentVersions.SumAsync(v => v.FileSize);

        var largestDocuments = await _db.Documents
            .OrderByDescending(d => d.FileSize)
            .Take(10)
            .Select(d => new FileSizeDto(d.Id, d.Name, d.FileSize, d.ContentType))
            .ToListAsync();

        var storageByFolder = await _db.Folders
            .Include(f => f.Documents)
            .Where(f => f.Documents.Any())
            .Select(f => new FolderStorageDto(
                f.Id,
                f.Name,
                f.Documents.Count,
                f.Documents.Sum(d => d.FileSize)))
            .OrderByDescending(fs => fs.TotalBytes)
            .Take(10)
            .ToListAsync();

        return Ok(new StorageStatsDto(
            totalFiles,
            totalVersionFiles,
            totalDocumentBytes,
            totalVersionBytes,
            totalDocumentBytes + totalVersionBytes,
            _fileStorage.GetStorageDirectory(),
            largestDocuments,
            storageByFolder));
    }
}

public record DashboardStatsDto(
    int TotalDocuments,
    int TotalFolders,
    int TotalTags,
    int TotalUsers,
    int ActiveUsers,
    int TotalVersions,
    long TotalStorageBytes,
    List<DocManager.DTOs.DocumentDto> RecentDocuments,
    List<DocManager.DTOs.TagWithCountDto> TopTags,
    List<ContentTypeStatDto> ContentTypeBreakdown,
    List<UserActivityDto> UserActivity);

public record StorageStatsDto(
    int TotalFiles,
    int TotalVersionFiles,
    long TotalDocumentBytes,
    long TotalVersionBytes,
    long TotalBytes,
    string StorageDirectory,
    List<FileSizeDto> LargestDocuments,
    List<FolderStorageDto> StorageByFolder);

public record ContentTypeStatDto(string ContentType, int Count, long TotalBytes);
public record UserActivityDto(int UserId, string DisplayName, int DocumentCount, int FolderCount, DateTime LastActivity);
public record FileSizeDto(int DocumentId, string Name, long FileSize, string ContentType);
public record FolderStorageDto(int FolderId, string Name, int DocumentCount, long TotalBytes);
