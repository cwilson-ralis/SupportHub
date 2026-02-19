namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("ApplicationUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.AzureAdObjectId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(u => u.AzureAdObjectId)
            .IsUnique()
            .HasDatabaseName("IX_ApplicationUsers_AzureAdObjectId");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_ApplicationUsers_Email");

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
