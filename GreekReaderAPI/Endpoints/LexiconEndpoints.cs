using System.Security.Claims;
using System.Text;
using GreekReaderAPI.Data;
using GreekReaderAPI.Models;
using GreekReaderAPI.Security;
using GreekReaderAPI.Services;
using GreekReaderAPI.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

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
        group.MapGet("/features", GetFeatures)
            .Produces<FeaturesResponse>()
            .AddOpenApiOperationTransformer((op, _, _) =>
            {
                op.Summary = "List distinct grammatical features";
                op.Description =
                    "Returns every distinct grammatical feature that can occur on a form " +
                    "of the given part of speech. Omit partOfSpeech to list features " +
                    "across all parts of speech. An unrecognised part of speech returns " +
                    "an empty feature list.";
                var p = op.Parameters?.FirstOrDefault(p => p.Name == "partOfSpeech");
                if (p is not OpenApiParameter parameter) return Task.CompletedTask;
                parameter.Description =
                    "Part of speech to restrict the feature list to (e.g. noun, verb, " +
                    "proper-noun). Matching ignores case, spaces, and punctuation, so " +
                    "proper-noun, proper noun, and ProperNoun are equivalent. Omit to " +
                    "return all features.";
                parameter.Examples = new Dictionary<string, IOpenApiExample>
                {
                    ["noun"] = new OpenApiExample { Value = "noun" },
                    ["verb"] = new OpenApiExample { Value = "verb" },
                    ["hyphenated proper noun"] = new OpenApiExample { Value = "proper-noun" }
                };
                return Task.CompletedTask;
            });

        var userLemmas = app.MapGroup("/lemmas").RequireAuthorization();
        userLemmas.MapGet("", GetUserLemmas)
            .Produces<UserLemmaResponse[]>()
            .Produces(StatusCodes.Status401Unauthorized)
            .AddOpenApiOperationTransformer((op, _, _) =>
            {
                var p = op.Parameters?.FirstOrDefault(p => p.Name == "featureList");
                if (p is not OpenApiParameter parameter) return Task.CompletedTask;
                parameter.Description = "Filter by grammatical features. Accepts a single feature " +
                                        "(e.g. masculine) or a comma-separated list, optionally wrapped " +
                                        "in square brackets (e.g. [masculine, nominative]). Only lemmas " +
                                        "with a form that includes every listed feature are returned; " +
                                        "matching is case-insensitive.";
                parameter.Examples = new Dictionary<string, IOpenApiExample>
                {
                    ["single feature"] = new OpenApiExample { Value = "masculine" },
                    ["feature list"] = new OpenApiExample { Value = "[masculine, nominative]" }
                };
                return Task.CompletedTask;
            });
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
            .Produces(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")
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
        [AsParameters] GetUserLemmasRequest filter,
        ClaimsPrincipal principal,
        ILexiconService lexicon,
        CancellationToken ct
    ) => TypedResults.Ok(await lexicon.GetUserLemmasAsync(principal.GetUserId(), filter, ct));

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

    private static async Task<Results<Ok<LemmaResponse[]>, NotFound, ValidationProblem>> Query
    (
        string form,
        IOptions<LexiconOptions> options,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var maxFormLength = options.Value.FormMaxLength;

        if (form.Length > maxFormLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["form"] = [$"The field form must be a string with a maximum length of {maxFormLength}."]
            });
        }

        var entries = await lexicon.QueryAsync(form, ct);
        if (entries.Length == 0)
            return TypedResults.NotFound();

        return TypedResults.Ok(entries);
    }

    private static async Task<Results<Ok<AnalyseResponse[]>, NotFound, ValidationProblem>> Analyse
    (
        AnalyseRequest req,
        IOptions<AnalyseOptions> options,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var maxTextLength = options.Value.MaxTextLength;

        if (req.Text.Length > maxTextLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Text"] = [$"The Text field must be a string with a maximum length of {maxTextLength}."]
            });
        }

        var entries = await lexicon.AnalyseAsync(req.Text, ct);
        if (entries.Length == 0)
            return TypedResults.NotFound();

        return TypedResults.Ok(entries);
    }

    private static async Task<Ok<FeaturesResponse>> GetFeatures
    (
        [FromQuery] string? partOfSpeech,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var features = await lexicon.GetFeaturesAsync(partOfSpeech, ct);
        return TypedResults.Ok(new FeaturesResponse(features));
    }

    private static async Task<IResult> UploadFile
    (
        IFormFile? file,
        HttpContext context,
        IFileService files,
        IOptions<FileUploadOptions> uploadOptions,
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

                var creation = await uploadedFiles.CreateAsync(
                    new UploadedFile
                    {
                        UserId = userId,
                        DisplayName = displayName,
                        OriginalFileName = success.FileName,
                        FileSizeBytes = file.Length,
                        TextSizeBytes = Encoding.UTF8.GetByteCount(success.Text)
                    },
                    success.Text,
                    ct);

                if (creation is UploadedFileCreateResult.LimitExceeded exceeded)
                {
                    return exceeded.Limit switch
                    {
                        FileUploadLimitExceeded.FileCount => TypedResults.Conflict(),
                        FileUploadLimitExceeded.TextBytes => TextQuotaExceeded(uploadOptions.Value.MaxTextBytesPerUser),
                        _ => TypedResults.StatusCode(StatusCodes.Status500InternalServerError)
                    };
                }

                var created = ((UploadedFileCreateResult.Created)creation).File;

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
                return FileTooLarge(uploadOptions.Value.MaxFileBytes);
            case PlainTextFileResult.Unsupported:
                return TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            default:
                return TypedResults.BadRequest();
        }
    }

    private static IResult FileTooLarge(long maxFileBytes) => Results.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "File too large",
        detail: $"This file exceeds the per-file upload limit of {FormatByteLimit(maxFileBytes)}. Choose a smaller file.",
        extensions: new Dictionary<string, object?> { ["code"] = "file_too_large" });

    private static IResult TextQuotaExceeded(long maxTextBytes) => Results.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Your file storage limit has been reached",
        detail: $"This upload would put your stored file text over the {FormatByteLimit(maxTextBytes)} limit. Delete one or more uploaded files before trying again.",
        extensions: new Dictionary<string, object?> { ["code"] = "user_text_storage_limit_exceeded" });

    private static string FormatByteLimit(long bytes)
    {
        const long bytesPerKiB = 1024;
        const long bytesPerMiB = 1024 * bytesPerKiB;

        if (bytes >= bytesPerMiB && bytes % bytesPerMiB == 0)
            return $"{bytes / bytesPerMiB} MiB";

        if (bytes >= bytesPerKiB)
            return $"{bytes / (double)bytesPerKiB:0.#} KiB";

        return $"{bytes} bytes";
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
