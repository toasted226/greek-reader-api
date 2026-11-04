using GreekReaderAPI.Models;

namespace GreekReaderAPI.Data;

public interface IUploadedFileRepository
{
    Task<UploadedFileCreateResult> CreateAsync(UploadedFile file, string content, CancellationToken ct = default);

    Task<UploadedFile[]> GetByUserIdAsync(long userId, CancellationToken ct = default);

    Task<(UploadedFile File, string Content)?> GetWithContentAsync(long fileId, long userId, CancellationToken ct = default);

    Task<bool> UpdateDisplayNameAsync(long fileId, long userId, string displayName, CancellationToken ct = default);

    Task<bool> DeleteAsync(long fileId, long userId, CancellationToken ct = default);
}
