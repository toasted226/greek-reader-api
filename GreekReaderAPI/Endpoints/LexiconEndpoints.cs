using System.ComponentModel.DataAnnotations;
using GreekReader.Interop;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GreekReaderAPI.Endpoints;

public sealed record AnalyseRequest(
    [property: Required] string Text
);

public static class LexiconEndpoints
{
    public static IEndpointRouteBuilder MapLexiconEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/lexicon");
        group.MapGet("/query", Query)
            .Produces<LemmaResponse[]>()
            .Produces(StatusCodes.Status404NotFound);
        group.MapPost("/analyse", Analyse)
            .Produces<AnalyseResponse[]>()
            .Produces(StatusCodes.Status404NotFound);
        return app;
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

        return TypedResults.Ok(entries);
    }

    private static async Task<Results<Ok<AnalyseResponse[]>, NotFound>> Analyse
    (
        AnalyseRequest req,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var entries = await lexicon.AnalyseAsync(req.Text, ct);
        if (entries.Length == 0)
            return TypedResults.NotFound();

        return TypedResults.Ok(entries);
    }
}