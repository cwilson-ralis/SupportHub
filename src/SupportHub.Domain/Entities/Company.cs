namespace SupportHub.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public ICollection<Division> Divisions { get; set; } = new List<Division>();
    public ICollection<UserCompanyRole> UserCompanyRoles { get; set; } = new List<UserCompanyRole>();
}
