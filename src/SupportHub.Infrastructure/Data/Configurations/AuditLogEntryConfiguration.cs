namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.UserDisplayName)
            .HasMaxLength(200);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("IX_AuditLogEntries_EntityType_EntityId");

        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("IX_AuditLogEntries_Timestamp");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditLogEntries_UserId");

        // No soft-delete filter — audit logs are immutable
    }
}
