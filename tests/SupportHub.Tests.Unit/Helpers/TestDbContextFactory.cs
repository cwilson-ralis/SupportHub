namespace SupportHub.Tests.Unit.Helpers;

using Microsoft.EntityFrameworkCore;
using SupportHub.Infrastructure.Data;

public static class TestDbContextFactory
{
    public static SupportHubDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<SupportHubDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new SupportHubDbContext(options);
    }
}
