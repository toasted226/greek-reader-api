using GreekReader.Interop;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GreekReaderAPI.Endpoints;

public sealed record LemmaResponse(
    string Lemma,
    string Pos,
    string[] Glosses,
    ExampleResponse[] Examples,
    FormResponse[] Forms);

public sealed record ExampleResponse(string Greek, string English);

public sealed record FormResponse(string Form, string[] Features);

public static class LexiconEndpoints
{
    public static IEndpointRouteBuilder MapLexiconEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/lexicon");
        group.MapGet("/query", Query)
            .Produces<LemmaResponse[]>()
            .Produces(StatusCodes.Status404NotFound);
        return app;
    }

    private static LemmaResponse ToLemmaResponse(LemmaEntry l)
    {
        return new LemmaResponse(
            l.Lemma,
            l.Pos.ToString().ToLowerInvariant(),
            l.Glosses,
            [.. l.Examples.Select(e => new ExampleResponse(e.Greek, e.English))],
            [.. l.Forms.Select(f => new FormResponse(f.Form, f.Features))]
        );
    }

    private static async Task<Results<Ok<LemmaResponse[]>, NotFound>> Query
    (
        string form,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var entries = await lexicon.QueryAsync(form, ct);
        if (entries.Length == 0)
            return TypedResults.NotFound();

        var response = entries.Select(ToLemmaResponse).ToArray();

        return TypedResults.Ok(response);
    }
}