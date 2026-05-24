using DocManager.DTOs;
using DocManager.Models;

namespace DocManager.Services;

public static class MappingService
{
    public static DocumentDto ToDto(Document doc)
    {
        return new DocumentDto(
            doc.Id,
            doc.Name,
            doc.Description,
            doc.ContentType,
            doc.FileSize,
            doc.Version,
            doc.CreatedAt,
            doc.UpdatedAt,
            doc.FolderId,
            doc.Folder?.Name,
            doc.CreatedByUserId,
            doc.CreatedBy?.DisplayName,
            doc.DocumentTags?.Select(dt => ToDto(dt.Tag)).ToList() ?? new List<TagDto>());
    }

    public static FolderDto ToDto(Folder folder)
    {
        return new FolderDto(
            folder.Id,
            folder.Name,
            folder.Description,
            folder.ParentFolderId,
            folder.CreatedByUserId,
            folder.CreatedBy?.DisplayName,
            folder.CreatedAt,
            folder.UpdatedAt,
            folder.Documents?.Count ?? 0,
            folder.Children?.Count ?? 0);
    }

    public static FolderTreeDto ToTreeDto(Folder folder)
    {
        return new FolderTreeDto(
            folder.Id,
            folder.Name,
            folder.Description,
            folder.ParentFolderId,
            folder.CreatedAt,
            folder.Children?.Select(ToTreeDto).ToList() ?? new List<FolderTreeDto>(),
            folder.Documents?.Count ?? 0);
    }

    public static TagDto ToDto(Tag tag)
    {
        return new TagDto(
            tag.Id,
            tag.Name,
            tag.Color,
            tag.CreatedAt);
    }

    public static TagWithCountDto ToCountDto(Tag tag)
    {
        return new TagWithCountDto(
            tag.Id,
            tag.Name,
            tag.Color,
            tag.CreatedAt,
            tag.DocumentTags?.Count ?? 0);
    }

    public static UserDto ToDto(User user)
    {
        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt);
    }

    public static DocumentVersionDto ToDto(DocumentVersion version)
    {
        return new DocumentVersionDto(
            version.Id,
            version.VersionNumber,
            version.FileSize,
            version.ContentType,
            version.CreatedAt,
            version.ChangeNote);
    }

    public static DocumentVersionDetailDto ToDetailDto(DocumentVersion version)
    {
        return new DocumentVersionDetailDto(
            version.Id,
            version.VersionNumber,
            version.Name,
            version.FileSize,
            version.ContentType,
            version.CreatedAt,
            version.ChangeNote,
            version.CreatedByUserId,
            version.CreatedBy?.DisplayName);
    }
}
