namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class UserCompanyRoleConfiguration : IEntityTypeConfiguration<UserCompanyRole>
{
    public void Configure(EntityTypeBuilder<UserCompanyRole> builder)
    {
        builder.ToTable("UserCompanyRoles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(r => r.User)
            .WithMany(u => u.UserCompanyRoles)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Company)
            .WithMany(c => c.UserCompanyRoles)
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.UserId, r.CompanyId, r.Role })
            .IsUnique()
            .HasDatabaseName("IX_UserCompanyRoles_UserId_CompanyId_Role");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
