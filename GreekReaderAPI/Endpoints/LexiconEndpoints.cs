using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GreekReaderAPI.Models;
using GreekReaderAPI.Security;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GreekReaderAPI.Endpoints;

public sealed record AnalyseRequest(
    [property: Required] string Text
);

public sealed record CreateUserLemmaRequest(
    [property: Required] int LemmaId,
    [property: Required] string ContextSentence
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

        var userLemmas = app.MapGroup("/lemmas").RequireAuthorization();
        userLemmas.MapGet("", GetUserLemmas)
            .Produces<UserLemmaResponse[]>()
            .Produces(StatusCodes.Status401Unauthorized);
        userLemmas.MapPost("", CreateUserLemma)
            .Produces<UserLemmaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        userLemmas.MapGet("/{lemmaId:int}", GetUserLemma)
            .Produces<UserLemmaResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        userLemmas.MapDelete("/{lemmaId:int}", DeleteUserLemma)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        return app;
    }

    private static async Task<Ok<UserLemmaResponse[]>> GetUserLemmas
    (
        ClaimsPrincipal principal,
        ILexiconService lexicon,
        CancellationToken ct
    ) => TypedResults.Ok(await lexicon.GetUserLemmasAsync(principal.GetUserId(), ct));

    private static async Task<Results<Created<UserLemmaResponse>, NotFound>> CreateUserLemma
    (
        CreateUserLemmaRequest request,
        ClaimsPrincipal principal,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var userLemma = await lexicon.CreateUserLemmaAsync(
            principal.GetUserId(), request.LemmaId, request.ContextSentence, ct);
        return userLemma is null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/lemmas/{request.LemmaId}", userLemma);
    }

    private static async Task<Results<Ok<UserLemmaResponse>, NotFound>> GetUserLemma
    (
        int lemmaId,
        ClaimsPrincipal principal,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var userLemma = await lexicon.GetUserLemmaAsync(principal.GetUserId(), lemmaId, ct);
        return userLemma is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(userLemma);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteUserLemma
    (
        int lemmaId,
        ClaimsPrincipal principal,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var deleted = await lexicon.DeleteUserLemmaAsync(principal.GetUserId(), lemmaId, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
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
