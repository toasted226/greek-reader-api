using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace GreekReaderAPI.Models;

public sealed record AnalyseRequest(
    [property: Required] string Text
);

public sealed record CreateUserLemmaRequest(
    [property: Required] int LemmaId,
    [property: Required] string ContextSentence
);

/// <summary>
/// A bracket-delimited list of grammatical features, e.g. <c>[masculine, nominative]</c>.
/// Items are comma-separated and optional whitespace around each item is ignored.
/// </summary>
public readonly struct FeatureListParam(string[] features)
{
    public string[] Features { get; } = features;

    /// <summary>
    /// Accepts <c>[masculine, nominative]</c> or bare comma-separated values
    /// like <c>masculine, nominative</c>.
    /// </summary>
    public static bool TryParse(string? value, out FeatureListParam result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = new FeatureListParam([]);
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            trimmed = trimmed[1..^1];

        var items = trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToArray();

        result = new FeatureListParam(items);
        return true;
    }

    public override string ToString() => $"[{string.Join(", ", Features)}]";
}

/// <summary>
/// Optional filter and paging parameters for browsing the lexicon.
/// </summary>
public sealed record GetLexiconLemmasRequest
{
    /// <summary>Filter by part of speech (e.g. "verb", "noun").</summary>
    [FromQuery(Name = "partOfSpeech")]
    public string? PartOfSpeech { get; init; }

    /// <summary>Filter by lemma (dictionary headword) text.</summary>
    [FromQuery(Name = "lemma")]
    public string? Lemma { get; init; }

    /// <summary>
    /// Filter by an exact inflected form. The value is normalised through the
    /// morphology engine, so accents, case, and final sigma do not matter.
    /// </summary>
    [FromQuery(Name = "form")]
    public string? Form { get; init; }

    /// <summary>
    /// Filter by a fragment of an inflected form. The value is normalised
    /// through the morphology engine before the substring match.
    /// </summary>
    [FromQuery(Name = "formContains")]
    public string? FormContains { get; init; }

    /// <summary>Filter by example sentence text.</summary>
    [FromQuery(Name = "example")]
    public string? Example { get; init; }

    /// <summary>
    /// Filter to lemmas whose forms include all of the listed grammatical features.
    /// Pass as a bracket-delimited list, e.g. <c>?featureList=[masculine, nominative]</c>.
    /// </summary>
    [FromQuery(Name = "featureList")]
    public FeatureListParam? FeatureList { get; init; }

    /// <summary>Number of results to skip. Defaults to 0.</summary>
    [FromQuery(Name = "offset")]
    public int? Offset { get; init; }

    /// <summary>
    /// Maximum number of results to return. Defaults to the configured page size.
    /// </summary>
    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }
}

/// <summary>
/// Optional filter parameters for retrieving a user's saved lemmas.
/// </summary>
public sealed record GetUserLemmasRequest
{
    /// <summary>Filter by part of speech (e.g. "verb", "noun").</summary>
    [FromQuery(Name = "partOfSpeech")]
    public string? PartOfSpeech { get; init; }

    /// <summary>Filter by lemma (dictionary headword).</summary>
    [FromQuery(Name = "lemma")]
    public string? Lemma { get; init; }

    /// <summary>Filter by a specific inflected form.</summary>
    [FromQuery(Name = "form")]
    public string? Form { get; init; }

    /// <summary>Filter by example sentence text.</summary>
    [FromQuery(Name = "example")]
    public string? Example { get; init; }

    /// <summary>
    /// Filter to lemmas whose forms include all of the listed grammatical features.
    /// Pass as a bracket-delimited list, e.g. <c>?featureList=[masculine, nominative]</c>.
    /// </summary>
    [FromQuery(Name = "featureList")]
    public FeatureListParam? FeatureList { get; init; }

    /// <summary>Include only lemmas saved on or after this date (inclusive).</summary>
    [FromQuery(Name = "minDate")]
    public DateOnly? MinDate { get; init; }

    /// <summary>Include only lemmas saved on or before this date (inclusive).</summary>
    [FromQuery(Name = "maxDate")]
    public DateOnly? MaxDate { get; init; }

    /// <summary>When true, return only the most recently saved occurrence of each lemma.</summary>
    [FromQuery(Name = "latest")]
    public bool? Latest { get; init; }
}
