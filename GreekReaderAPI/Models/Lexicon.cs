using GreekReader.Interop;

namespace GreekReaderAPI.Models;

public sealed record UserLemmaResponse(
    int Id,
    long UserId,
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

public sealed record UserLemma(
    int Id,
    long UserId,
    int LemmaId,
    string ContextSentence);

public sealed record LexiconLemma(
    int Id,
    string Lemma,
    PartOfSpeech Pos,
    string[] Glosses,
    Example[] Examples,
    FormEntry[] Forms);
    