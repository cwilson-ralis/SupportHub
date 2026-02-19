namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Direction)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.SenderEmail).HasMaxLength(256);
        builder.Property(m => m.SenderName).HasMaxLength(200);
        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.ExternalMessageId).HasMaxLength(500);

        // Indexes
        builder.HasIndex(m => m.TicketId);
        builder.HasIndex(m => m.ExternalMessageId);

        // Relationships
        builder.HasMany(m => m.Attachments)
            .WithOne(a => a.TicketMessage)
            .HasForeignKey(a => a.TicketMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
