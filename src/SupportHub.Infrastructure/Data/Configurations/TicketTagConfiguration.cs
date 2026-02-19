namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.ToTable("TicketTags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Tag).IsRequired().HasMaxLength(100);

        // Unique constraint: one tag per ticket (case-insensitive enforced at service layer)
        builder.HasIndex(t => new { t.TicketId, t.Tag }).IsUnique();

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
