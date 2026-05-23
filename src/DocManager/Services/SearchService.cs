using DocManager.Data;
using DocManager.DTOs;
using DocManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DocManager.Services;

public interface ISearchService
{
    Task<PagedResult<SearchResultDto>> SearchAsync(SearchRequestDto request, int page, int pageSize);
}

public class SearchService : ISearchService
{
    private readonly AppDbContext _db;

    public SearchService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SearchResultDto>> SearchAsync(SearchRequestDto request, int page, int pageSize)
    {
        var query = request.Query.ToLower();
        var results = new List<SearchResultDto>();

        // Search documents
        var docQuery = _db.Documents
            .Include(d => d.Folder)
            .Include(d => d.DocumentTags)
            .ThenInclude(dt => dt.Tag)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.ContentType))
            docQuery = docQuery.Where(d => d.ContentType == request.ContentType);

        if (request.FolderId.HasValue)
            docQuery = docQuery.Where(d => d.FolderId == request.FolderId.Value);

        if (request.TagId.HasValue)
            docQuery = docQuery.Where(d => d.DocumentTags.Any(dt => dt.TagId == request.TagId.Value));

        if (request.CreatedAfter.HasValue)
            docQuery = docQuery.Where(d => d.CreatedAt >= request.CreatedAfter.Value);

        if (request.CreatedBefore.HasValue)
            docQuery = docQuery.Where(d => d.CreatedAt <= request.CreatedBefore.Value);

        var documents = await docQuery.ToListAsync();

        foreach (var doc in documents)
        {
            var relevance = CalculateRelevance(query, doc.Name, doc.Description);
            if (relevance > 0)
            {
                results.Add(new SearchResultDto(
                    doc.Id,
                    doc.Name,
                    doc.Description,
                    doc.ContentType,
                    "Document",
                    doc.Folder?.Name,
                    doc.UpdatedAt,
                    relevance));
            }
        }

        // Search folders
        var folders = await _db.Folders
            .Include(f => f.Documents)
            .ToListAsync();

        foreach (var folder in folders)
        {
            var relevance = CalculateRelevance(query, folder.Name, folder.Description);
            if (relevance > 0)
            {
                results.Add(new SearchResultDto(
                    folder.Id,
                    folder.Name,
                    folder.Description,
                    "folder",
                    "Folder",
                    null,
                    folder.UpdatedAt,
                    relevance));
            }
        }

        var sorted = results.OrderByDescending(r => r.Relevance).ToList();
        var totalCount = sorted.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = ((long)page - 1L) * pageSize;
        var items = skip >= totalCount
            ? new List<SearchResultDto>()
            : sorted.Skip((int)skip).Take(pageSize).ToList();
        return new PagedResult<SearchResultDto>(items, page, pageSize, totalCount, totalPages);
    }

    private static double CalculateRelevance(string query, string name, string? description)
    {
        double score = 0;
        var nameLower = name.ToLower();
        var descLower = description?.ToLower() ?? string.Empty;

        if (nameLower == query)
            score += 10.0;
        else if (nameLower.Contains(query))
            score += 5.0;
        else if (nameLower.Split(' ', '-', '_').Any(w => w.StartsWith(query)))
            score += 3.0;

        if (descLower.Contains(query))
            score += 2.0;

        // Boost for exact word match in description
        if (descLower.Split(' ', '.', ',', '-', '_').Contains(query))
            score += 1.5;

        return score;
    }
}
