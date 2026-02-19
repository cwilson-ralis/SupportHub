namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;

public interface IFileStorageService
{
    Task<Result<string>> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<Result<Stream>> GetFileAsync(string storagePath, CancellationToken ct = default);
    Task<Result<bool>> DeleteFileAsync(string storagePath, CancellationToken ct = default);
}
