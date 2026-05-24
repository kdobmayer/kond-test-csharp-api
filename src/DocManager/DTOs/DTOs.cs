namespace DocManager.DTOs;

// Document DTOs
public record DocumentDto(
    int Id,
    string Name,
    string? Description,
    string ContentType,
    long FileSize,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? FolderId,
    string? FolderName,
    int CreatedByUserId,
    string? CreatedByName,
    List<TagDto> Tags);

public record CreateDocumentDto(
    string Name,
    string? Description,
    int? FolderId,
    int CreatedByUserId);

public record UpdateDocumentDto(
    string? Name,
    string? Description,
    int? FolderId,
    string? ChangeNote);

public record DocumentVersionDto(
    int Id,
    int VersionNumber,
    long FileSize,
    string ContentType,
    DateTime CreatedAt,
    string? ChangeNote);

public record DocumentVersionDetailDto(
    int Id,
    int VersionNumber,
    string Name,
    long FileSize,
    string ContentType,
    DateTime CreatedAt,
    string? ChangeNote,
    int CreatedByUserId,
    string? CreatedByName);

public record VersionFieldChangeDto(
    string FieldName,
    string? OldValue,
    string? NewValue);

public record VersionComparisonDto(
    int DocumentId,
    int V1,
    int V2,
    DateTime V1CreatedAt,
    DateTime V2CreatedAt,
    int V1CreatedByUserId,
    string? V1CreatedByName,
    int V2CreatedByUserId,
    string? V2CreatedByName,
    List<VersionFieldChangeDto> Changes);

// Folder DTOs
public record FolderDto(
    int Id,
    string Name,
    string? Description,
    int? ParentFolderId,
    int CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int DocumentCount,
    int ChildFolderCount);

public record FolderTreeDto(
    int Id,
    string Name,
    string? Description,
    int? ParentFolderId,
    DateTime CreatedAt,
    List<FolderTreeDto> Children,
    int DocumentCount);

public record CreateFolderDto(
    string Name,
    string? Description,
    int? ParentFolderId,
    int CreatedByUserId);

public record UpdateFolderDto(
    string? Name,
    string? Description,
    int? ParentFolderId);

// Tag DTOs
public record TagDto(
    int Id,
    string Name,
    string? Color,
    DateTime CreatedAt);

public record CreateTagDto(
    string Name,
    string? Color);

public record UpdateTagDto(
    string? Name,
    string? Color);

public record TagWithCountDto(
    int Id,
    string Name,
    string? Color,
    DateTime CreatedAt,
    int DocumentCount);

// User DTOs
public record UserDto(
    int Id,
    string Username,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateUserDto(
    string Username,
    string Email,
    string DisplayName);

public record UpdateUserDto(
    string? Username,
    string? Email,
    string? DisplayName,
    bool? IsActive);

// Search DTOs
public record SearchResultDto(
    int Id,
    string Name,
    string? Description,
    string ContentType,
    string EntityType,
    string? FolderPath,
    DateTime UpdatedAt,
    double Relevance);

public record SearchRequestDto(
    string Query,
    string? ContentType,
    int? FolderId,
    int? TagId,
    DateTime? CreatedAfter,
    DateTime? CreatedBefore);
