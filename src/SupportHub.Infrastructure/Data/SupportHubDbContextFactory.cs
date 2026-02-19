namespace SupportHub.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class SupportHubDbContextFactory : IDesignTimeDbContextFactory<SupportHubDbContext>
{
    public SupportHubDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SupportHubDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=SupportHub_Dev;Trusted_Connection=True;");

        return new SupportHubDbContext(optionsBuilder.Options);
    }
}
