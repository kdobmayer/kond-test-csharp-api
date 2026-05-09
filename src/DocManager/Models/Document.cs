namespace DocManager.Models;

public class Document
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? FolderId { get; set; }
    public Folder? Folder { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<DocumentTag> DocumentTags { get; set; } = new List<DocumentTag>();
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
}
