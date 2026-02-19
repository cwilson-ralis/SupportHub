namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable("Divisions");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(d => d.Company)
            .WithMany(c => c.Divisions)
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.CompanyId, d.Name })
            .IsUnique()
            .HasDatabaseName("IX_Divisions_CompanyId_Name");

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
