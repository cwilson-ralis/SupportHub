namespace SupportHub.Web;

using Hangfire.Dashboard;

public class HangfireSuperAdminFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("SuperAdmin");
    }
}
