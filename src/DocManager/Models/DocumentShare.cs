namespace DocManager.Models;

public class DocumentShare
{
    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public int SharedWithUserId { get; set; }
    public User SharedWith { get; set; } = null!;
    public int SharedByUserId { get; set; }
    public User SharedBy { get; set; } = null!;
    public string Permission { get; set; } = "read";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
