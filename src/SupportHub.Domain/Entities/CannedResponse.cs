namespace SupportHub.Domain.Entities;

public class CannedResponse : BaseEntity
{
    public Guid? CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Company? Company { get; set; }
}
