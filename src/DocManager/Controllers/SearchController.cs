using DocManager.DTOs;
using DocManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SearchResultDto>>> Search(
        [FromQuery] string query,
        [FromQuery] string? contentType,
        [FromQuery] int? folderId,
        [FromQuery] int? tagId,
        [FromQuery] DateTime? createdAfter,
        [FromQuery] DateTime? createdBefore,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query is required" });

        if (query.Length < 2)
            return BadRequest(new { message = "Query must be at least 2 characters" });

        if (page < 1)
            return BadRequest(new { message = "Page must be at least 1" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "PageSize must be between 1 and 100" });

        var request = new SearchRequestDto(
            query, contentType, folderId, tagId, createdAfter, createdBefore);

        var results = await _searchService.SearchAsync(request, page, pageSize);
        return Ok(results);
    }
}
