namespace DocManager.Models;

public class DocumentTag
{
    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
    public DateTime TaggedAt { get; set; } = DateTime.UtcNow;
}
