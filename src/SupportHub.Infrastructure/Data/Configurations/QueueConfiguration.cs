namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class QueueConfiguration : IEntityTypeConfiguration<Queue>
{
    public void Configure(EntityTypeBuilder<Queue> builder)
    {
        builder.ToTable("Queues");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(q => q.Description)
            .HasMaxLength(1000);

        builder.HasQueryFilter(q => !q.IsDeleted);

        // Indexes
        builder.HasIndex(q => new { q.CompanyId, q.Name }).IsUnique();
        // Filtered unique index: only one default queue per company
        builder.HasIndex(q => new { q.CompanyId, q.IsDefault })
            .HasFilter("IsDefault = 1")
            .IsUnique();
        builder.HasIndex(q => q.IsActive);

        // Relationships
        builder.HasOne(q => q.Company)
            .WithMany()
            .HasForeignKey(q => q.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.RoutingRules)
            .WithOne(r => r.Queue)
            .HasForeignKey(r => r.QueueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Tickets)
            .WithOne(t => t.Queue)
            .HasForeignKey(t => t.QueueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
