namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class RoutingRuleConfiguration : IEntityTypeConfiguration<RoutingRule>
{
    public void Configure(EntityTypeBuilder<RoutingRule> builder)
    {
        builder.ToTable("RoutingRules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.MatchValue)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.AutoAddTags)
            .HasMaxLength(1000);

        builder.Property(r => r.MatchType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.MatchOperator)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.AutoSetPriority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasQueryFilter(r => !r.IsDeleted);

        // Indexes
        builder.HasIndex(r => new { r.CompanyId, r.SortOrder });
        builder.HasIndex(r => r.QueueId);
        builder.HasIndex(r => r.IsActive);

        // Relationships
        builder.HasOne(r => r.Company)
            .WithMany()
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Queue relationship defined in QueueConfiguration — don't repeat here to avoid conflict

        builder.HasOne(r => r.AutoAssignAgent)
            .WithMany()
            .HasForeignKey(r => r.AutoAssignAgentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
