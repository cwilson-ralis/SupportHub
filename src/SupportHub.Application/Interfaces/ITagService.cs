namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface ITagService
{
    Task<Result<TicketTagDto>> AddTagAsync(Guid ticketId, string tag, CancellationToken ct = default);
    Task<Result<bool>> RemoveTagAsync(Guid ticketId, string tag, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> GetPopularTagsAsync(Guid? companyId, int count = 20, CancellationToken ct = default);
}
