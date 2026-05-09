namespace DocManager.Models;

public class Folder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentFolderId { get; set; }
    public Folder? ParentFolder { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Folder> Children { get; set; } = new List<Folder>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
