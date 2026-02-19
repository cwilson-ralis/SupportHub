namespace SupportHub.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Enums;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "SuperAdmin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _userService.GetUsersAsync(page, pageSize, search, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct = default)
    {
        var result = await _userService.GetUserByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error == "User not found."
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncUser(
        [FromBody] string azureAdObjectId,
        CancellationToken ct = default)
    {
        var result = await _userService.SyncUserFromAzureAdAsync(azureAdObjectId, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpPost("{userId:guid}/roles")]
    public async Task<IActionResult> AssignRole(
        Guid userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken ct = default)
    {
        var result = await _userService.AssignRoleAsync(userId, request.CompanyId, request.Role, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });
        return Ok();
    }

    [HttpDelete("{userId:guid}/roles/{companyId:guid}/{role}")]
    public async Task<IActionResult> RemoveRole(
        Guid userId,
        Guid companyId,
        string role,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var userRole))
            return BadRequest(new { error = $"Invalid role: {role}" });

        var result = await _userService.RemoveRoleAsync(userId, companyId, userRole, ct);
        if (!result.IsSuccess)
            return result.Error == "Role assignment not found."
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        return NoContent();
    }
}

public record AssignRoleRequest(Guid CompanyId, UserRole Role);
