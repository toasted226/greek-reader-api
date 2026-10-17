using GreekReaderAPI.Models;

namespace GreekReaderAPI.Data;

public interface IUserRepository
{
    Task<int> InsertAsync(User user, CancellationToken ct = default);
    Task<User?> GetAsync(int id, CancellationToken ct = default);
    Task<bool> UpdateAsync(User user, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}