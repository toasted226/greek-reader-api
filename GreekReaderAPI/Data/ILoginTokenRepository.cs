using GreekReaderAPI.Models;

namespace GreekReaderAPI.Data;

public interface ILoginTokenRepository
{
    Task<Guid> CreateAsync(LoginToken token, CancellationToken ct = default);
    Task<User?> ConsumeAsync(byte[] hash, CancellationToken ct = default);
    Task<bool> DeleteByUserIdAsync(long userId, CancellationToken ct = default);
}
