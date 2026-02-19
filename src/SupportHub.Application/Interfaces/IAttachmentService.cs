namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface IAttachmentService
{
    Task<Result<TicketAttachmentDto>> UploadAttachmentAsync(Guid ticketId, Guid? messageId, Stream fileStream, string fileName, string contentType, long fileSize, CancellationToken ct = default);
    Task<Result<(Stream FileStream, string ContentType, string FileName)>> DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TicketAttachmentDto>>> GetAttachmentsAsync(Guid ticketId, CancellationToken ct = default);
}
