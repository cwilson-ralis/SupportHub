namespace SupportHub.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "SuperAdmin")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCompanies(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _companyService.GetCompaniesAsync(page, pageSize, search, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCompany(Guid id, CancellationToken ct = default)
    {
        var result = await _companyService.GetCompanyByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error == "Company not found."
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCompany(
        [FromBody] CreateCompanyRequest request,
        CancellationToken ct = default)
    {
        var result = await _companyService.CreateCompanyAsync(request, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });
        return CreatedAtAction(nameof(GetCompany), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCompany(
        Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken ct = default)
    {
        var result = await _companyService.UpdateCompanyAsync(id, request, ct);
        if (!result.IsSuccess)
            return result.Error == "Company not found."
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCompany(Guid id, CancellationToken ct = default)
    {
        var result = await _companyService.DeleteCompanyAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error == "Company not found."
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        return NoContent();
    }
}
