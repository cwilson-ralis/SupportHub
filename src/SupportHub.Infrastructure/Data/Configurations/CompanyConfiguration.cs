namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("IX_Companies_Code");

        builder.HasIndex(c => c.Name)
            .HasDatabaseName("IX_Companies_Name");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
