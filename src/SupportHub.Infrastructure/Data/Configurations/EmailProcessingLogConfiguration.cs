namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class EmailProcessingLogConfiguration : IEntityTypeConfiguration<EmailProcessingLog>
{
    public void Configure(EntityTypeBuilder<EmailProcessingLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalMessageId).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.SenderEmail).HasMaxLength(256);
        builder.Property(x => x.ProcessingResult).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(x => x.EmailConfiguration)
            .WithMany()
            .HasForeignKey(x => x.EmailConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Ticket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.EmailConfigurationId);
        builder.HasIndex(x => x.ExternalMessageId);
        builder.HasIndex(x => x.ProcessedAt);
    }
}
