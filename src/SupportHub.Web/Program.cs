using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using Serilog;
using SupportHub.Application.Interfaces;
using SupportHub.Infrastructure;
using SupportHub.Infrastructure.Jobs;
using SupportHub.Web;
using SupportHub.Web.Components;
using SupportHub.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/supporthub-.log", rollingInterval: RollingInterval.Day));

// Azure AD Authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("SuperAdmin", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "SuperAdmin")));

    options.AddPolicy("Admin", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "Admin") ||
            context.User.HasClaim("role", "SuperAdmin")));

    options.AddPolicy("Agent", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "Agent") ||
            context.User.HasClaim("role", "Admin") ||
            context.User.HasClaim("role", "SuperAdmin")));
});

// Infrastructure (DbContext, services)
builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire
var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnectionString, new SqlServerStorageOptions { SchemaName = "Hangfire" }));
builder.Services.AddHangfireServer();

// MudBlazor
builder.Services.AddMudServices();

// Current user service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Razor pages (for Microsoft.Identity.Web.UI login/logout pages)
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

// Blazor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Controllers (for API endpoints)
builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireSuperAdminFilter()]
});

RecurringJob.AddOrUpdate<EmailPollingJob>(
    "email-polling",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Minutely);

app.UseAntiforgery();

app.MapControllers();
app.MapRazorPages();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
