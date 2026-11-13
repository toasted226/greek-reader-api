using GreekReader.Interop;

namespace GreekReaderAPI.Models;

public sealed record UserLemmaResponse(
    int Id,
    LemmaResponse Lemma,
    string ContextSentence);

public sealed record AnalyseResponse(
    TokenResponse Token,
    LemmaResponse[] Lemmas);

public sealed record TokenResponse(
    string Text,
    int Start,
    bool IsWord);

public sealed record LemmaResponse(
    long Id,
    string Lemma,
    string Pos,
    string[] Glosses,
    ExampleResponse[] Examples,
    FormResponse[] Forms);

public sealed record ExampleResponse(string Greek, string English);

public sealed record FormResponse(string Form, string[] Features);

public sealed record FeaturesResponse(string[] Features);

/// <summary>
/// A page of lexicon lemmas plus the total number of lemmas matching the filter.
/// </summary>
public sealed record LemmaPageResponse(
    LemmaResponse[] Items,
    int Offset,
    int Limit,
    int Total);

/// <summary>
/// Resolved filter values for a lexicon query. Text values that have a
/// normalised column are already normalised through the morphology engine.
/// </summary>
public sealed record LexiconLemmaQuery(
    PartOfSpeech? PartOfSpeech,
    string? Lemma,
    string? FormNormalized,
    string? FormContainsNormalized,
    string? Example,
    string[] Features);

public sealed record UserLemma(
    int Id,
    long UserId,
    int LemmaId,
    string ContextSentence,
    DateTime SavedAt);

public sealed record LexiconLemma(
    int Id,
    string Lemma,
    PartOfSpeech Pos,
    string[] Glosses,
    ExampleResponse[] Examples,
    FormEntry[] Forms);
    