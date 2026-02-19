namespace SupportHub.Domain.Entities;

public class ApplicationUser : BaseEntity
{
    public string AzureAdObjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<UserCompanyRole> UserCompanyRoles { get; set; } = new List<UserCompanyRole>();
}
