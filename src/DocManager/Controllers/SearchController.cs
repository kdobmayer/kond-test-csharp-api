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
    public async Task<ActionResult<List<SearchResultDto>>> Search(
        [FromQuery] string query,
        [FromQuery] string? contentType,
        [FromQuery] int? folderId,
        [FromQuery] int? tagId,
        [FromQuery] DateTime? createdAfter,
        [FromQuery] DateTime? createdBefore)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query is required" });

        if (query.Length < 2)
            return BadRequest(new { message = "Query must be at least 2 characters" });

        var request = new SearchRequestDto(
            query, contentType, folderId, tagId, createdAfter, createdBefore);

        // No pagination — returns ALL results (intentional rough edge)
        var results = await _searchService.SearchAsync(request);
        return Ok(results);
    }
}
