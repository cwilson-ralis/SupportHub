namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class CompanyServiceTests : IDisposable
{
    private readonly SupportHub.Infrastructure.Data.SupportHubDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly CompanyService _sut;

    public CompanyServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _auditService = Substitute.For<IAuditService>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        // Default: SuperAdmin so all existing tests pass without modification
        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>
            {
                new UserCompanyRole { Role = UserRole.SuperAdmin, CompanyId = Guid.NewGuid(), UserId = Guid.NewGuid() }
            }));
        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _sut = new CompanyService(_context, _auditService, _currentUserService);
    }

    public void Dispose() => _context.Dispose();

    // Helper to seed a company
    private async Task<Company> SeedCompanyAsync(string name = "Test Corp", string code = "TC")
    {
        var company = new Company { Name = name, Code = code };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        return company;
    }

    [Fact]
    public async Task GetCompaniesAsync_ReturnsPagedResult()
    {
        // Arrange
        await SeedCompanyAsync("Alpha Corp", "ALP");
        await SeedCompanyAsync("Beta Corp", "BET");

        // Act
        var result = await _sut.GetCompaniesAsync(1, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCompanyByIdAsync_WhenExists_ReturnsCompany()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        // Act
        var result = await _sut.GetCompanyByIdAsync(company.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(company.Id);
        result.Value.Name.Should().Be("Test Corp");
    }

    [Fact]
    public async Task GetCompanyByIdAsync_WhenNotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetCompanyByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateCompanyAsync_WithValidData_CreatesAndReturns()
    {
        // Arrange
        var request = new CreateCompanyRequest("New Corp", "NEW", "A new company");

        // Act
        var result = await _sut.CreateCompanyAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Corp");
        result.Value.Code.Should().Be("NEW");
        _context.Companies.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateCompanyAsync_WithDuplicateCode_ReturnsFailure()
    {
        // Arrange
        await SeedCompanyAsync("Existing Corp", "EXIST");
        var request = new CreateCompanyRequest("New Corp", "exist", null); // same code, different case

        // Act
        var result = await _sut.CreateCompanyAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateCompanyAsync_WithValidData_UpdatesAndReturns()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var request = new UpdateCompanyRequest("Updated Corp", "UPD", true, "Updated description");

        // Act
        var result = await _sut.UpdateCompanyAsync(company.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Updated Corp");
        result.Value.Code.Should().Be("UPD");
    }

    [Fact]
    public async Task UpdateCompanyAsync_WhenNotFound_ReturnsFailure()
    {
        // Act
        var request = new UpdateCompanyRequest("X", "X", true, null);
        var result = await _sut.UpdateCompanyAsync(Guid.NewGuid(), request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteCompanyAsync_SoftDeletesSetsIsDeleted()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        // Act
        var result = await _sut.DeleteCompanyAsync(company.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Bypass the soft-delete query filter to verify the entity
        var deletedCompany = await _context.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == company.Id);
        deletedCompany.Should().NotBeNull();
        deletedCompany!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCompanyAsync_WhenNotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.DeleteCompanyAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateCompanyAsync_LogsAuditEvent()
    {
        // Arrange
        var request = new CreateCompanyRequest("Audited Corp", "AUD", null);

        // Act
        await _sut.CreateCompanyAsync(request);

        // Assert
        await _auditService.Received(1).LogAsync(
            "Create",
            "Company",
            Arg.Any<string>(),
            oldValues: Arg.Any<object?>(),
            newValues: Arg.Any<object?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCompanyAsync_LogsAuditEventWithOldAndNewValues()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var request = new UpdateCompanyRequest("Updated", "UPD", true, null);

        // Act
        await _sut.UpdateCompanyAsync(company.Id, request);

        // Assert
        await _auditService.Received(1).LogAsync(
            "Update",
            "Company",
            company.Id.ToString(),
            oldValues: Arg.Any<object?>(),
            newValues: Arg.Any<object?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCompanyAsync_LogsAuditEvent()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        // Act
        await _sut.DeleteCompanyAsync(company.Id);

        // Assert
        await _auditService.Received(1).LogAsync(
            "Delete",
            "Company",
            company.Id.ToString(),
            oldValues: Arg.Any<object?>(),
            newValues: Arg.Any<object?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCompaniesAsync_NonSuperAdmin_ReturnsOnlyAccessibleCompanies()
    {
        // Arrange — seed two companies
        var company1 = await SeedCompanyAsync("Alpha Corp", "ALP");
        var company2 = await SeedCompanyAsync("Beta Corp", "BET");

        // Set up a non-SuperAdmin user with access only to company1
        var limitedUser = Substitute.For<ICurrentUserService>();
        limitedUser.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>
            {
                new() { UserId = Guid.NewGuid(), CompanyId = company1.Id, Role = UserRole.Agent }
            }));
        limitedUser.HasAccessToCompanyAsync(company1.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        limitedUser.HasAccessToCompanyAsync(company2.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var sut = new CompanyService(_context, _auditService, limitedUser);

        // Act
        var result = await sut.GetCompaniesAsync(1, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(c => c.Id == company1.Id);
        result.Value.Items.Should().NotContain(c => c.Id == company2.Id);
    }

    [Fact]
    public async Task GetCompanyByIdAsync_WhenAccessDenied_ReturnsFailure()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        var deniedUser = Substitute.For<ICurrentUserService>();
        deniedUser.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        deniedUser.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>()));

        var sut = new CompanyService(_context, _auditService, deniedUser);

        // Act
        var result = await sut.GetCompanyByIdAsync(company.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Access denied.");
    }

    [Fact]
    public async Task UpdateCompanyAsync_WhenAccessDenied_ReturnsFailure()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        var deniedUser = Substitute.For<ICurrentUserService>();
        deniedUser.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        deniedUser.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>()));

        var sut = new CompanyService(_context, _auditService, deniedUser);

        // Act
        var result = await sut.UpdateCompanyAsync(company.Id, new UpdateCompanyRequest("X", "X", true, null));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Access denied.");
    }

    [Fact]
    public async Task DeleteCompanyAsync_WhenAccessDenied_ReturnsFailure()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        var deniedUser = Substitute.For<ICurrentUserService>();
        deniedUser.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        deniedUser.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>()));

        var sut = new CompanyService(_context, _auditService, deniedUser);

        // Act
        var result = await sut.DeleteCompanyAsync(company.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Access denied.");
    }
}
