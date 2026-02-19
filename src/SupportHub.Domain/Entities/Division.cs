namespace SupportHub.Domain.Entities;

public class Division : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
}
