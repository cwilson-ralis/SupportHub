namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface ICannedResponseService
{
    Task<Result<PagedResult<CannedResponseDto>>> GetCannedResponsesAsync(Guid? companyId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<CannedResponseDto>> CreateCannedResponseAsync(CreateCannedResponseRequest request, CancellationToken ct = default);
    Task<Result<CannedResponseDto>> UpdateCannedResponseAsync(Guid id, UpdateCannedResponseRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteCannedResponseAsync(Guid id, CancellationToken ct = default);
}
