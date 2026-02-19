using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

namespace SupportHub.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private readonly SupportHubDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly long _maxFileSizeBytes;
    private readonly HashSet<string> _allowedExtensions;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        SupportHubDbContext dbContext,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        IConfiguration configuration,
        ILogger<AttachmentService> logger)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _logger = logger;
        _maxFileSizeBytes = configuration.GetValue<long>("FileStorage:MaxFileSizeBytes", 26214400L);
        var extensions = configuration["FileStorage:AllowedExtensions"]
            ?? ".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg,.gif,.txt,.csv,.zip,.msg,.eml";
        _allowedExtensions = extensions.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLowerInvariant())
            .ToHashSet();
    }

    public async Task<Result<TicketAttachmentDto>> UploadAttachmentAsync(
        Guid ticketId, Guid? messageId, Stream fileStream, string fileName,
        string contentType, long fileSize, CancellationToken ct = default)
    {
        if (fileSize > _maxFileSizeBytes)
            return Result<TicketAttachmentDto>.Failure($"File exceeds maximum size of {_maxFileSizeBytes / 1024 / 1024} MB");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(ext))
            return Result<TicketAttachmentDto>.Failure($"File extension '{ext}' is not allowed");

        var ticket = await _dbContext.Tickets.FindAsync([ticketId], ct);
        if (ticket is null)
            return Result<TicketAttachmentDto>.Failure("Ticket not found");
        if (!await _currentUser.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<TicketAttachmentDto>.Failure("Access denied");

        var saveResult = await _fileStorage.SaveFileAsync(fileStream, fileName, contentType, ct);
        if (!saveResult.IsSuccess)
            return Result<TicketAttachmentDto>.Failure(saveResult.Error!);

        var attachment = new TicketAttachment
        {
            TicketId = ticketId,
            TicketMessageId = messageId,
            FileName = Path.GetFileName(saveResult.Value!),
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            StoragePath = saveResult.Value!
        };
        _dbContext.TicketAttachments.Add(attachment);
        await _dbContext.SaveChangesAsync(ct);

        return Result<TicketAttachmentDto>.Success(MapToDto(attachment));
    }

    public async Task<Result<(Stream FileStream, string ContentType, string FileName)>> DownloadAttachmentAsync(
        Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await _dbContext.TicketAttachments
            .Include(a => a.Ticket)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

        if (attachment is null)
            return Result<(Stream, string, string)>.Failure("Attachment not found");
        if (!await _currentUser.HasAccessToCompanyAsync(attachment.Ticket.CompanyId, ct))
            return Result<(Stream, string, string)>.Failure("Access denied");

        var streamResult = await _fileStorage.GetFileAsync(attachment.StoragePath, ct);
        if (!streamResult.IsSuccess)
            return Result<(Stream, string, string)>.Failure(streamResult.Error!);

        return Result<(Stream, string, string)>.Success(
            (streamResult.Value!, attachment.ContentType, attachment.OriginalFileName));
    }

    public async Task<Result<IReadOnlyList<TicketAttachmentDto>>> GetAttachmentsAsync(
        Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _dbContext.Tickets.FindAsync([ticketId], ct);
        if (ticket is null)
            return Result<IReadOnlyList<TicketAttachmentDto>>.Failure("Ticket not found");
        if (!await _currentUser.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<IReadOnlyList<TicketAttachmentDto>>.Failure("Access denied");

        var attachments = await _dbContext.TicketAttachments
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return Result<IReadOnlyList<TicketAttachmentDto>>.Success(
            attachments.Select(MapToDto).ToList());
    }

    private static TicketAttachmentDto MapToDto(TicketAttachment a) => new(
        a.Id, a.TicketId, a.TicketMessageId,
        a.FileName, a.OriginalFileName, a.ContentType, a.FileSize, a.CreatedAt);
}
