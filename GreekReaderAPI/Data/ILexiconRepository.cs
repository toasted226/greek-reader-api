using GreekReaderAPI.Models;

namespace GreekReaderAPI.Data;

public interface ILexiconRepository
{
    Task<LexiconLemma[]> GetAsync(int[] ids, CancellationToken ct = default);
    Task<Dictionary<string, LexiconLemma[]>> GetByFormsAsync(string[] forms, CancellationToken ct = default);
    Task<UserLemma[]> GetUserLemmasAsync(long userId, CancellationToken ct = default);
    Task<UserLemma?> GetUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default);
    Task<UserLemma?> CreateUserLemmaAsync(long userId, int lemmaId, string contextSentence, CancellationToken ct = default);
    Task<bool> DeleteUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default);
}
