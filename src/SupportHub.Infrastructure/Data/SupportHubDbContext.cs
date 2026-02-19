namespace SupportHub.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using SupportHub.Domain.Entities;

public class SupportHubDbContext : DbContext
{
    public SupportHubDbContext(DbContextOptions<SupportHubDbContext> options)
        : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<UserCompanyRole> UserCompanyRoles => Set<UserCompanyRole>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<InternalNote> InternalNotes => Set<InternalNote>();
    public DbSet<TicketTag> TicketTags => Set<TicketTag>();
    public DbSet<CannedResponse> CannedResponses => Set<CannedResponse>();
    public DbSet<EmailConfiguration> EmailConfigurations => Set<EmailConfiguration>();
    public DbSet<EmailProcessingLog> EmailProcessingLogs => Set<EmailProcessingLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportHubDbContext).Assembly);
    }
}
