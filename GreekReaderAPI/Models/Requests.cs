using System.ComponentModel.DataAnnotations;

namespace GreekReaderAPI.Models;

public sealed record AnalyseRequest(
    [property: Required] string Text
);

public sealed record CreateUserLemmaRequest(
    [property: Required] int LemmaId,
    [property: Required] string ContextSentence
);
