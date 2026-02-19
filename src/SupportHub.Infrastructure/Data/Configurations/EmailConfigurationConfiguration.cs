namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class EmailConfigurationConfiguration : IEntityTypeConfiguration<EmailConfiguration>
{
    public void Configure(EntityTypeBuilder<EmailConfiguration> builder)
    {
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.SharedMailboxAddress).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LastPolledMessageId).HasMaxLength(500);
        builder.Property(x => x.DefaultPriority).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CompanyId, x.SharedMailboxAddress }).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}
