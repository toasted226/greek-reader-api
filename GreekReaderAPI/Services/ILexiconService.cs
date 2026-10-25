using GreekReaderAPI.Models;

namespace GreekReaderAPI.Services;

public interface ILexiconService
{
    Task<LemmaResponse[]> QueryAsync(string form, CancellationToken ct = default);
    Task<AnalyseResponse[]> AnalyseAsync(string text, CancellationToken ct = default);
    Task<UserLemmaResponse[]> GetUserLemmasAsync(long userId, CancellationToken ct = default);
    Task<UserLemmaResponse?> GetUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default);
    Task<UserLemmaResponse?> CreateUserLemmaAsync(long userId, int lemmaId, string contextSentence, CancellationToken ct = default);
    Task<bool> DeleteUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default);
}
