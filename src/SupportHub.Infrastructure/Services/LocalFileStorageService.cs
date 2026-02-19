using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.Interfaces;

namespace SupportHub.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _basePath = configuration["FileStorage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "SupportHubFiles");
        _logger = logger;
    }

    public async Task<Result<string>> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        try
        {
            var sanitized = SanitizeFileName(fileName);
            var now = DateTimeOffset.UtcNow;
            var subDir = Path.Combine(now.Year.ToString(), now.Month.ToString("D2"), now.Day.ToString("D2"));
            var fullDir = Path.Combine(_basePath, subDir);
            Directory.CreateDirectory(fullDir);

            var storedName = $"{Guid.NewGuid()}_{sanitized}";
            var fullPath = Path.Combine(fullDir, storedName);
            var relativePath = Path.Combine(subDir, storedName);

            await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await fileStream.CopyToAsync(fs, ct);

            return Result<string>.Success(relativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName}", fileName);
            return Result<string>.Failure($"Failed to save file: {ex.Message}");
        }
    }

    public Task<Result<Stream>> GetFileAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (!IsPathWithinBase(fullPath))
            return Task.FromResult(Result<Stream>.Failure("Invalid storage path"));
        if (!File.Exists(fullPath))
            return Task.FromResult(Result<Stream>.Failure("File not found"));

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(Result<Stream>.Success(stream));
    }

    public Task<Result<bool>> DeleteFileAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (!IsPathWithinBase(fullPath))
            return Task.FromResult(Result<bool>.Failure("Invalid storage path"));
        if (!File.Exists(fullPath))
            return Task.FromResult(Result<bool>.Failure("File not found"));

        File.Delete(fullPath);
        return Task.FromResult(Result<bool>.Success(true));
    }

    private bool IsPathWithinBase(string fullPath)
    {
        var resolvedPath = Path.GetFullPath(fullPath);
        var resolvedBase = Path.GetFullPath(_basePath);
        return resolvedPath.StartsWith(resolvedBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || resolvedPath.Equals(resolvedBase, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        clean = clean.Replace("..", "_");
        return clean;
    }
}
