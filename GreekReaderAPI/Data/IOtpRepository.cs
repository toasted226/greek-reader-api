using GreekReaderAPI.Models;

namespace GreekReaderAPI.Data;

public interface IOtpRepository
{
    Task<Guid> CreateAsync(OtpCode code, CancellationToken ct = default);
    Task<User?> ConsumeAsync(long userId, byte[] hash, int maxAttempts, CancellationToken ct = default);
}
