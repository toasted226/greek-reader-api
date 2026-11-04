using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GreekReaderAPI.Models;
using GreekReaderAPI.Security;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;

namespace GreekReaderAPI.Endpoints;

public sealed record AnalyseRequest(
    [property: Required] string Text
);

public sealed record UploadFileResponse(string FileName, int CharacterCount);

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

        var files = app.MapGroup("/files").RequireAuthorization();
        files.MapPost("", UploadFile)
            .RequireRateLimiting("file-upload")
            .Produces<UploadFileResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .Produces(StatusCodes.Status429TooManyRequests);
        files.MapGet("", GetFiles);
        files.MapGet("/{fileId:int}", GetFile);
        files.MapDelete("/{fileId:int}", DeleteFile);
        
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
    
    private static async Task<IResult> UploadFile
    (
        IFormFile? file,
        HttpContext context,
        IFileService files,
        CancellationToken ct
    )
    {
        if (file is null || context.Request.Form.Files.Count != 1)
            return TypedResults.BadRequest();

        await using var stream = file.OpenReadStream();

        var result = await files.ReadPlainTextAsync(
            stream, file.FileName, file.ContentType, file.Length, ct);

        return result switch
        {
            PlainTextFileResult.Success success =>
                TypedResults.Ok(new UploadFileResponse(success.FileName, success.Text.Length)),
            PlainTextFileResult.TooLarge =>
                TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge),
            PlainTextFileResult.Unsupported =>
                TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType),
            _ => TypedResults.BadRequest()
        };
    }
    
    private static Task GetFiles(HttpContext context)
    {
        throw new NotImplementedException();
    }
    
    private static Task GetFile(HttpContext context)
    {
        throw new NotImplementedException();
    }

    private static Task DeleteFile(HttpContext context)
    {
        throw new NotImplementedException();
    }
}
