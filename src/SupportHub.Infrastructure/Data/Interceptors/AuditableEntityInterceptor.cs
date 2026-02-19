namespace SupportHub.Infrastructure.Data.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditableEntityInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var user = _currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                    // Handle soft-delete stamp
                    if (entry.Entity.IsDeleted && entry.Entity.DeletedAt is null)
                    {
                        entry.Entity.DeletedAt = now;
                        entry.Entity.DeletedBy = user;
                    }
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context is null)
            return base.SavingChanges(eventData, result);

        var now = DateTimeOffset.UtcNow;
        var user = _currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                    if (entry.Entity.IsDeleted && entry.Entity.DeletedAt is null)
                    {
                        entry.Entity.DeletedAt = now;
                        entry.Entity.DeletedBy = user;
                    }
                    break;
            }
        }

        return base.SavingChanges(eventData, result);
    }
}
