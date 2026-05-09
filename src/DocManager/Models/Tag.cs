namespace DocManager.Models;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentTag> DocumentTags { get; set; } = new List<DocumentTag>();
}
