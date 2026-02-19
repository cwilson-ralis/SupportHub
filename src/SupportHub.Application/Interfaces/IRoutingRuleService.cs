namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IRoutingRuleService
{
    Task<Result<IReadOnlyList<RoutingRuleDto>>> GetRulesAsync(Guid companyId, CancellationToken ct = default);
    Task<Result<RoutingRuleDto>> GetRuleByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<RoutingRuleDto>> CreateRuleAsync(CreateRoutingRuleRequest request, CancellationToken ct = default);
    Task<Result<RoutingRuleDto>> UpdateRuleAsync(Guid id, UpdateRoutingRuleRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteRuleAsync(Guid id, CancellationToken ct = default);
    Task<Result<bool>> ReorderRulesAsync(Guid companyId, ReorderRoutingRulesRequest request, CancellationToken ct = default);
}
