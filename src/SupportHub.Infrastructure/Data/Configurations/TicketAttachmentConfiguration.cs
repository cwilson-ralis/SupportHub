namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(1000);

        // Indexes
        builder.HasIndex(a => a.TicketId);
        builder.HasIndex(a => a.TicketMessageId);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
