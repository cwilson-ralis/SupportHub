namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(t => t.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Description)
            .IsRequired();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.RequesterEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.RequesterName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.System).HasMaxLength(200);
        builder.Property(t => t.IssueType).HasMaxLength(200);

        // Indexes
        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.HasIndex(t => t.CompanyId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedAgentId);
        builder.HasIndex(t => t.RequesterEmail);
        builder.HasIndex(t => new { t.CompanyId, t.Status });

        // Relationships
        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedAgent)
            .WithMany()
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Messages)
            .WithOne(m => m.Ticket)
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Attachments)
            .WithOne(a => a.Ticket)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.InternalNotes)
            .WithOne(n => n.Ticket)
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Tags)
            .WithOne(tag => tag.Ticket)
            .HasForeignKey(tag => tag.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
