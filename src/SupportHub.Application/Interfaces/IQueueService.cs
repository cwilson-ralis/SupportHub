namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IQueueService
{
    Task<Result<PagedResult<QueueDto>>> GetQueuesAsync(Guid companyId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<QueueDto>> GetQueueByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<QueueDto>> CreateQueueAsync(CreateQueueRequest request, CancellationToken ct = default);
    Task<Result<QueueDto>> UpdateQueueAsync(Guid id, UpdateQueueRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteQueueAsync(Guid id, CancellationToken ct = default);
}
