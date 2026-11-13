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
            .Produces<LemmaPageResponse>()
            .ProducesValidationProblem()
            .AddOpenApiOperationTransformer((op, _, _) =>
            {
                op.Summary = "Browse and filter the lexicon";
                op.Description =
                    """
                    Returns a page of lemmas from the lexicon. All filters are optional;
                    omitting every filter browses the whole lexicon in lemma order. Matching
                    is case-insensitive. The form filters are normalised through the
                    morphology engine, so accents, case, and final sigma do not matter.
                    Lemma and example text is matched literally, so accents are significant
                    there. A filter that matches nothing returns an empty page, not a 404.
                    """;

                DescribeParameter(op, "partOfSpeech",
                    """
                    Filter by part of speech (e.g. noun, verb, proper-noun). Matching
                    ignores case, spaces, and punctuation, so proper-noun, proper noun,
                    and ProperNoun are equivalent. An unrecognised part of speech returns
                    an empty page.
                    """,
                    new Dictionary<string, IOpenApiExample>
                    {
                        ["noun"] = new OpenApiExample { Value = "noun" },
                        ["hyphenated proper noun"] = new OpenApiExample { Value = "proper-noun" }
                    });

                DescribeParameter(op, "lemma",
                    """
                    Filter by a case-insensitive substring of the lemma (dictionary
                    headword). Accents are significant, so λογ does not match λόγος.
                    """,
                    new Dictionary<string, IOpenApiExample>
                    {
                        ["accented"] = new OpenApiExample { Value = "λόγ" }
                    });

                DescribeParameter(op, "form",
                    """
                    Filter by an exact inflected form. The value is normalised through the
                    morphology engine, so accent, case, and final-sigma differences are
                    ignored.
                    """,
                    new Dictionary<string, IOpenApiExample>
                    {
                        ["accented"] = new OpenApiExample { Value = "λόγος" },
                        ["unaccented"] = new OpenApiExample { Value = "λογος" }
                    });

                DescribeParameter(op, "formContains",
                    """
                    Filter by a substring of an inflected form. The value is normalised
                    through the morphology engine, so λογ matches λόγος.
                    """,
                    new Dictionary<string, IOpenApiExample>
                    {
                        ["form fragment"] = new OpenApiExample { Value = "λογ" }
                    });

                DescribeParameter(op, "example",
                    """
                    Filter by a case-insensitive substring of an example's Greek or English
                    text. Accents are significant.
                    """,
                    new Dictionary<string, IOpenApiExample>
                    {
                        ["English"] = new OpenApiExample { Value = "word" }
                    });

                DescribeParameter(op, "featureList",
                    """
                    Filter by grammatical features. Accepts a single feature (e.g. masculine)
                    or a comma-separated list, optionally wrapped in square brackets (e.g.
                    [masculine, nominative]). Only lemmas with a form that includes every
                    listed feature are returned; matching is case-insensitive.
                    """,
                    new Dictionary<string, IOpenApiExample>
                    {
                        ["single feature"] = new OpenApiExample { Value = "masculine" },
                        ["feature list"] = new OpenApiExample { Value = "[masculine, nominative]" }
                    });

                DescribeParameter(op, "offset",
                    "Number of matching lemmas to skip. Defaults to 0.");

                DescribeParameter(op, "limit",
                    """
                    Maximum number of lemmas to return. Defaults to the configured page size
                    and may not exceed the configured maximum.
                    """);

                return Task.CompletedTask;
            });
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

    private static async Task<Results<Ok<LemmaPageResponse>, ValidationProblem>> Query
    (
        [AsParameters] GetLexiconLemmasRequest filter,
        IOptions<LexiconOptions> options,
        ILexiconService lexicon,
        CancellationToken ct
    )
    {
        var maxFormLength = options.Value.FormMaxLength;
        var maxPageSize = options.Value.MaxPageSize;
        var offset = filter.Offset ?? 0;
        var limit = filter.Limit ?? options.Value.DefaultPageSize;

        var errors = new Dictionary<string, string[]>();

        if (filter.Form is { Length: > 0 } form && form.Length > maxFormLength)
        {
            errors["form"] =
                [$"The field form must be a string with a maximum length of {maxFormLength}."];
        }

        if (filter.FormContains is { Length: > 0 } formContains && formContains.Length > maxFormLength)
        {
            errors["formContains"] =
                [$"The field formContains must be a string with a maximum length of {maxFormLength}."];
        }

        if (offset < 0)
            errors["offset"] = ["The field offset must be zero or greater."];

        if (limit < 1 || limit > maxPageSize)
            errors["limit"] = [$"The field limit must be between 1 and {maxPageSize}."];

        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var page = await lexicon.GetLexiconLemmasAsync(filter, offset, limit, ct);
        return TypedResults.Ok(page);
    }

    private static void DescribeParameter(
        OpenApiOperation op,
        string name,
        string description,
        Dictionary<string, IOpenApiExample>? examples = null)
    {
        if (op.Parameters?.FirstOrDefault(p => p.Name == name) is not OpenApiParameter parameter)
            return;

        parameter.Description = description;
        if (examples is not null)
            parameter.Examples = examples;
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

        return bytes switch
        {
            >= bytesPerMiB when bytes % bytesPerMiB == 0 => $"{bytes / bytesPerMiB} MiB",
            >= bytesPerKiB => $"{bytes / (double)bytesPerKiB:0.#} KiB",
            _ => $"{bytes} bytes"
        };
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
