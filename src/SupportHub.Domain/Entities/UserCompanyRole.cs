namespace SupportHub.Domain.Entities;

using SupportHub.Domain.Enums;

public class UserCompanyRole : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public UserRole Role { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
