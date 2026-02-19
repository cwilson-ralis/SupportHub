namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface ICompanyService
{
    Task<Result<PagedResult<CompanyDto>>> GetCompaniesAsync(
        int page, int pageSize, string? search = null, CancellationToken ct = default);

    Task<Result<CompanyDto>> GetCompanyByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<Result<CompanyDto>> CreateCompanyAsync(
        CreateCompanyRequest request, CancellationToken ct = default);

    Task<Result<CompanyDto>> UpdateCompanyAsync(
        Guid id, UpdateCompanyRequest request, CancellationToken ct = default);

    Task<Result<bool>> DeleteCompanyAsync(
        Guid id, CancellationToken ct = default);
}
