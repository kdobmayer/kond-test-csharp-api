namespace DocManager.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Folder> Folders { get; set; } = new List<Folder>();
    public ICollection<DocumentShare> SharesGiven { get; set; } = new List<DocumentShare>();
    public ICollection<DocumentShare> SharesReceived { get; set; } = new List<DocumentShare>();
}
