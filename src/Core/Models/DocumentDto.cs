namespace Core.Models;

public class DocumentDto
{
    public string DocumentType { get; set; } = null!;
    public Guid Id { get; set; }
    public int OrderedItemCount { get; set; }
    public int AvailableItemCount { get; set; }
    public string Status { get; set; } = "new";
    public DateTime Timestamp { get; set; }
}