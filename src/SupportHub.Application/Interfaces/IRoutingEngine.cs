namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IRoutingEngine
{
    Task<Result<RoutingResult>> EvaluateAsync(RoutingContext context, CancellationToken ct = default);
}
