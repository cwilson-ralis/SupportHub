namespace SupportHub.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportHub.Application.Interfaces;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Data.Interceptors;
using SupportHub.Infrastructure.Jobs;
using SupportHub.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<SupportHubDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"))
                .AddInterceptors(interceptor);
        });

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IUserService, UserService>();

        // Phase 2 — Core Ticketing services
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITicketMessageService, TicketMessageService>();
        services.AddScoped<IInternalNoteService, InternalNoteService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ICannedResponseService, CannedResponseService>();
        services.AddScoped<ITagService, TagService>();

        // Phase 3 — Email Integration services
        services.AddSingleton<IGraphClientFactory, GraphClientFactory>();
        services.AddScoped<IEmailPollingService, EmailPollingService>();
        services.AddScoped<IEmailSendingService, EmailSendingService>();
        services.AddScoped<IEmailProcessingService, EmailProcessingService>();
        services.AddScoped<IAiClassificationService, NoOpAiClassificationService>();
        services.AddScoped<IEmailConfigurationService, EmailConfigurationService>();
        services.AddTransient<EmailPollingJob>();

        // Phase 4 — Routing & Queue Management services
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<IRoutingRuleService, RoutingRuleService>();
        services.AddScoped<IRoutingEngine, RoutingEngine>();

        return services;
    }
}
