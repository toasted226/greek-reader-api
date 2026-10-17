using GreekReaderAPI.Models;

namespace GreekReaderAPI.Data;

public interface ILoginTokenRepository
{
    Task<long> InsertAsync(LoginToken token, CancellationToken ct = default);
    Task<LoginToken?> GetAsync(long id, CancellationToken ct = default);
    Task<LoginToken?> GetByUserIdAsync(long userId, CancellationToken ct = default);
    Task<LoginToken?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task<bool> UpdateAsync(LoginToken token, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
