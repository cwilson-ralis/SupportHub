namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class CannedResponseConfiguration : IEntityTypeConfiguration<CannedResponse>
{
    public void Configure(EntityTypeBuilder<CannedResponse> builder)
    {
        builder.ToTable("CannedResponses");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Body).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(100);

        // Indexes
        builder.HasIndex(c => c.CompanyId);
        builder.HasIndex(c => c.Category);

        // Relationships
        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
