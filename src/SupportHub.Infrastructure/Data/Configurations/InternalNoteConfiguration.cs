namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class InternalNoteConfiguration : IEntityTypeConfiguration<InternalNote>
{
    public void Configure(EntityTypeBuilder<InternalNote> builder)
    {
        builder.ToTable("InternalNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Body).IsRequired();

        // Indexes
        builder.HasIndex(n => n.TicketId);

        // Relationships
        builder.HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(n => !n.IsDeleted);
    }
}
