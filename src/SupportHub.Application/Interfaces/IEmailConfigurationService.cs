namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IEmailConfigurationService
{
    Task<Result<EmailConfigurationDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailConfigurationDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailConfigurationDto>>> GetActiveAsync(CancellationToken ct = default);
    Task<Result<EmailConfigurationDto>> CreateAsync(CreateEmailConfigurationRequest request, CancellationToken ct = default);
    Task<Result<EmailConfigurationDto>> UpdateAsync(Guid id, UpdateEmailConfigurationRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailProcessingLogDto>>> GetLogsAsync(Guid emailConfigurationId, int count = 50, CancellationToken ct = default);
}
