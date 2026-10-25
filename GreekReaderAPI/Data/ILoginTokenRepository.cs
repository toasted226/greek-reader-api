using GreekReaderAPI.Models;

namespace GreekReaderAPI.Data;

public interface ILoginTokenRepository
{
    Task<long> CreateAsync(LoginToken token, CancellationToken ct = default);
    Task<User?> ConsumeAsync(byte[] hash, CancellationToken ct = default);
    Task<LoginToken?> GetAsync(long id, CancellationToken ct = default);
    Task<LoginToken?> GetByUserIdAsync(long userId, CancellationToken ct = default);
    Task<LoginToken?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task<bool> UpdateAsync(LoginToken token, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<bool> DeleteByUserIdAsync(long userId, CancellationToken ct = default);
}
