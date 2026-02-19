namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class UserServiceTests : IDisposable
{
    private readonly SupportHub.Infrastructure.Data.SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("test-azure-id");
        _currentUserService.DisplayName.Returns("Test User");
        _currentUserService.Email.Returns("test@example.com");
        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _auditService = Substitute.For<IAuditService>();
        _sut = new UserService(_context, _currentUserService, _auditService);
    }

    public void Dispose() => _context.Dispose();

    private async Task<ApplicationUser> SeedUserAsync(
        string azureId = "azure-123",
        string email = "user@example.com",
        string displayName = "Test User")
    {
        var user = new ApplicationUser
        {
            AzureAdObjectId = azureId,
            Email = email,
            DisplayName = displayName
        };
        _context.ApplicationUsers.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Company> SeedCompanyAsync(string name = "Test Corp", string code = "TC")
    {
        var company = new Company { Name = name, Code = code };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        return company;
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsPagedResult()
    {
        // Arrange
        await SeedUserAsync("azure-1", "user1@example.com", "User One");
        await SeedUserAsync("azure-2", "user2@example.com", "User Two");

        // Act
        var result = await _sut.GetUsersAsync(1, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenExists_ReturnsUserWithRoles()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        var result = await _sut.GetUserByIdAsync(user.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("user@example.com");
        result.Value.Roles.Should().BeEmpty(); // No roles seeded
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenNotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetUserByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SyncUserFromAzureAdAsync_NewUser_CreatesUser()
    {
        // Arrange
        _currentUserService.DisplayName.Returns("New User");
        _currentUserService.Email.Returns("new@example.com");

        // Act
        var result = await _sut.SyncUserFromAzureAdAsync("new-azure-id");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AzureAdObjectId.Should().Be("new-azure-id");
        _context.ApplicationUsers.Should().ContainSingle();
        await _auditService.Received(1).LogAsync("Create", "ApplicationUser",
            Arg.Any<string>(), oldValues: Arg.Any<object?>(), newValues: Arg.Any<object?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserFromAzureAdAsync_ExistingUser_UpdatesFields()
    {
        // Arrange
        var user = await SeedUserAsync("existing-azure", "old@example.com", "Old Name");
        _currentUserService.DisplayName.Returns("New Name");
        _currentUserService.Email.Returns("new@example.com");

        // Act
        var result = await _sut.SyncUserFromAzureAdAsync("existing-azure");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("new@example.com");
        result.Value.DisplayName.Should().Be("New Name");
        await _auditService.Received(1).LogAsync("Update", "ApplicationUser",
            Arg.Any<string>(), oldValues: Arg.Any<object?>(), newValues: Arg.Any<object?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRoleAsync_ValidRequest_CreatesRole()
    {
        // Arrange
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync();

        // Act
        var result = await _sut.AssignRoleAsync(user.Id, company.Id, UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _context.UserCompanyRoles.Should().ContainSingle(r =>
            r.UserId == user.Id && r.CompanyId == company.Id && r.Role == UserRole.Agent);
    }

    [Fact]
    public async Task AssignRoleAsync_DuplicateRole_ReturnsFailure()
    {
        // Arrange
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync();
        _context.UserCompanyRoles.Add(new UserCompanyRole
        {
            UserId = user.Id,
            CompanyId = company.Id,
            Role = UserRole.Agent
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.AssignRoleAsync(user.Id, company.Id, UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AssignRoleAsync_InvalidUser_ReturnsFailure()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        // Act
        var result = await _sut.AssignRoleAsync(Guid.NewGuid(), company.Id, UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AssignRoleAsync_InvalidCompany_ReturnsFailure()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        var result = await _sut.AssignRoleAsync(user.Id, Guid.NewGuid(), UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RemoveRoleAsync_ExistingRole_SoftDeletes()
    {
        // Arrange
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync();
        var ucr = new UserCompanyRole { UserId = user.Id, CompanyId = company.Id, Role = UserRole.Agent };
        _context.UserCompanyRoles.Add(ucr);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.RemoveRoleAsync(user.Id, company.Id, UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = await _context.UserCompanyRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == ucr.Id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveRoleAsync_NotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.RemoveRoleAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AssignRoleAsync_WhenAccessDenied_ReturnsFailure()
    {
        // Arrange
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync();

        // Override access check to deny for this company
        _currentUserService.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.AssignRoleAsync(user.Id, company.Id, UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Access denied to this company.");
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenAccessDenied_ReturnsFailure()
    {
        // Arrange
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync();
        var ucr = new UserCompanyRole { UserId = user.Id, CompanyId = company.Id, Role = UserRole.Agent };
        _context.UserCompanyRoles.Add(ucr);
        await _context.SaveChangesAsync();

        // Override access check to deny
        _currentUserService.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.RemoveRoleAsync(user.Id, company.Id, UserRole.Agent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Access denied to this company.");
    }
}
