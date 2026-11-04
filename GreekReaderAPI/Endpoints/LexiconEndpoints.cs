using System.Security.Claims;
using GreekReader.Interop;
using GreekReaderAPI.Data;
using GreekReaderAPI.Models;
using GreekReaderAPI.Security;
using GreekReaderAPI.Services;
using GreekReaderAPI.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GreekReaderAPI.Endpoints;

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
        files.MapGet("", GetFiles)
            .Produces<FileMetadataResponse[]>()
            .Produces(StatusCodes.Status401Unauthorized);
        files.MapGet("/by-id", GetFile)
            .Produces<UploadFileResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        files.MapGet("/{fileId:long}", GetFileByPath)
            .Produces<UploadFileResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        files.MapDelete("/by-id", DeleteFile)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        files.MapDelete("/{fileId:long}", DeleteFileByPath)
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

    private static async Task<IResult> UploadFile
    (
        IFormFile? file,
        HttpContext context,
        IFileService files,
        ILexiconService lexicon,
        IUploadedFileRepository uploadedFiles,
        ClaimsPrincipal principal,
        CancellationToken ct
    )
    {
        if (file is null || context.Request.Form.Files.Count != 1)
            return TypedResults.BadRequest();

        await using var stream = file.OpenReadStream();

        var result = await files.ReadPlainTextAsync(
            stream, file.FileName, file.ContentType, file.Length, ct);

        switch (result)
        {
            case PlainTextFileResult.Success success:
            {
                var analysis = await lexicon.AnalyseAsync(success.Text, ct);
                var displayName = FileNameNormalizer.Normalize(success.FileName);
                var userId = principal.GetUserId();

                var created = await uploadedFiles.CreateAsync(
                    new UploadedFile
                    {
                        UserId = userId,
                        DisplayName = displayName,
                        OriginalFileName = success.FileName,
                        FileSizeBytes = file.Length
                    },
                    success.Text,
                    ct);

                return TypedResults.Ok(new UploadFileResponse
                {
                    FileId = created.Id,
                    OriginalFileName = success.FileName,
                    DisplayName = created.DisplayName,
                    CharacterCount = success.Text.Length,
                    FileSizeBytes = file.Length,
                    Analysis = analysis
                });
            }
            case PlainTextFileResult.TooLarge:
                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            case PlainTextFileResult.Unsupported:
                return TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            default:
                return TypedResults.BadRequest();
        }
    }

    private static async Task<Ok<FileMetadataResponse[]>> GetFiles
    (
        ClaimsPrincipal principal,
        IUploadedFileRepository uploadedFiles,
        CancellationToken ct
    )
    {
        var userId = principal.GetUserId();
        var files = await uploadedFiles.GetByUserIdAsync(userId, ct);

        var result = files
            .Select(f => new FileMetadataResponse
            {
                Id = f.Id,
                OriginalFileName = f.OriginalFileName,
                DisplayName = f.DisplayName,
                FileSizeBytes = f.FileSizeBytes,
                UploadedAt = f.UploadedAt
            })
            .ToArray();

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<UploadFileResponse>, NotFound>> GetFile
    (
        [FromQuery] long fileId,
        ClaimsPrincipal principal,
        IUploadedFileRepository uploadedFiles,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var userId = principal.GetUserId();
        var result = await uploadedFiles.GetWithContentAsync(fileId, userId, ct);

        if (result is null)
            return TypedResults.NotFound();

        var analysis = await lexicon.AnalyseAsync(result.Value.Content, ct);

        return TypedResults.Ok(new UploadFileResponse
        {
            FileId = result.Value.File.Id,
            OriginalFileName = result.Value.File.OriginalFileName,
            DisplayName = result.Value.File.DisplayName,
            CharacterCount = result.Value.Content.Length,
            FileSizeBytes = result.Value.File.FileSizeBytes,
            Analysis = analysis
        });
    }

    private static Task<Results<Ok<UploadFileResponse>, NotFound>> GetFileByPath
    (
        long fileId,
        ClaimsPrincipal principal,
        IUploadedFileRepository uploadedFiles,
        ILexiconService lexicon,
        CancellationToken ct
    ) => GetFile(fileId, principal, uploadedFiles, lexicon, ct);

    private static async Task<Results<NoContent, NotFound>> DeleteFile
    (
        [FromQuery] long fileId,
        ClaimsPrincipal principal,
        IUploadedFileRepository uploadedFiles,
        CancellationToken ct
    )
    {
        var userId = principal.GetUserId();
        var deleted = await uploadedFiles.DeleteAsync(fileId, userId, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static Task<Results<NoContent, NotFound>> DeleteFileByPath
    (
        long fileId,
        ClaimsPrincipal principal,
        IUploadedFileRepository uploadedFiles,
        CancellationToken ct
    ) => DeleteFile(fileId, principal, uploadedFiles, ct);
}
