using Microsoft.OpenApi;
using Dapper;
using DbUp;
using GreekReaderAPI.Data;
using GreekReaderAPI.Email;
using GreekReaderAPI.Endpoints;
using GreekReaderAPI.ErrorHandling;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Configuration.AddEnvironmentVariables();

var connString = builder.Configuration.GetConnectionString("Postgres")!;

var fileUploadOptions = builder.Configuration
    .GetSection(FileUploadOptions.SectionName)
    .Get<FileUploadOptions>() ?? new FileUploadOptions();

var maxRequestBodyBytes = fileUploadOptions.MaxFileBytes + 64 * 1024;

if (args.Contains("--reset") && builder.Environment.IsDevelopment())
{
    DropDatabase.For.PostgresqlDatabase(connString);
}

if (args.Contains("--migrate") || args.Contains("--reset"))
{
    Migrate.Run(connString);
    return;
}

builder.Services.AddNpgsqlDataSource(connString, _ => { });
builder.Services.AddTransient<LexiconImporter>();

DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddHealthChecks().AddNpgSql();
builder.Services.AddValidation();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails is HttpValidationProblemDetails { Errors.Count: > 0 } validation)
        {
            context.HttpContext.Items[RequestFailureLogging.ValidationErrorsItemKey] = validation.Errors;
        }
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(options => { options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1; });

// Dependency Injection
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILoginTokenRepository, LoginTokenRepository>();
builder.Services.AddScoped<ILexiconRepository, LexiconRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILexiconService, LexiconService>();
builder.Services.AddScoped<IFileService, FileService>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IEmailThrottle, EmailThrottle>();

builder.Services.Configure<FileUploadOptions>(
    builder.Configuration.GetSection(FileUploadOptions.SectionName));

builder.Services.Configure<AnalyseOptions>(
    builder.Configuration.GetSection(AnalyseOptions.SectionName));

builder.Services.Configure<LexiconOptions>(
    builder.Configuration.GetSection(LexiconOptions.SectionName));

var dataProtectionKeysPath = ResolveDataProtectionKeysPath(builder.Configuration, builder.Environment);

builder.Services
    .AddDataProtection()
    .SetApplicationName("GreekReaderAPI")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.Configure<FormOptions>(options =>
{
    options.BufferBody = false;
    options.MultipartBodyLengthLimit = maxRequestBodyBytes;
    options.MemoryBufferThreshold = (int)Math.Min(maxRequestBodyBytes, int.MaxValue);
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));

if (builder.Environment.IsDevelopment())
{
    //builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        var frontendBaseUrl = builder.Configuration["App:FrontendBaseUrl"] ?? "http://localhost:4200";

        policy.WithOrigins(frontendBaseUrl.TrimEnd('/'))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        // The frontend and API are subdomains of modulrza.app, so requests
        // between them are same-site and Lax cookies are sent.
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(1);
        o.SlidingExpiration = true;

        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "greekreader.antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // The frontend and API are subdomains of modulrza.app, so requests
    // between them are same-site and Lax cookies are sent.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        var retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan remaining)
            ? Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds))
            : 0;

        ctx.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "Too many requests. Please try again later.",
                retryAfterSeconds = retryAfter
            },
            ct);
    };
    options.AddPolicy("sign-in-request", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("otp-request", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("file-upload", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();
app.Logger.LogInformation(
    "Data Protection key ring path: {DataProtectionKeysPath}",
    dataProtectionKeysPath);
app.UseExceptionHandler();
app.UseMiddleware<RequestFailureLoggingMiddleware>();
app.UseRouting();
app.UseCors("AllowAngular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

if (args.Contains("--import"))
{
    await using var scope =  app.Services.CreateAsyncScope();
    var importer = scope.ServiceProvider.GetRequiredService<LexiconImporter>();

    var entries = GreekReader.Interop.Api.Load();
    await importer.ImportAsync(entries);
    return;
}

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "GreekReaderAPI v1"));
}

app.MapGet("/", () => new { Message = "API online." });
app.MapAuthEndpoints();
app.MapLexiconEndpoints();

app.Run();
return;

static string ResolveDataProtectionKeysPath(
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    const string fallbackDirectoryName = ".dataprotection-keys";

    var configuredPath = configuration["DataProtection:KeysPath"];
    string path;

    if (string.IsNullOrWhiteSpace(configuredPath))
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath is not configured. Set " +
                "'DataProtection__KeysPath' to a persistent directory so the " +
                "Data Protection key ring survives restarts and redeploys. " +
                "See docs/DATA_PROTECTION.md.");
        }

        path = Path.Combine(environment.ContentRootPath, fallbackDirectoryName);
    }
    else
    {
        path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    if (Directory.Exists(path)) return Path.GetFullPath(path);
    if (OperatingSystem.IsWindows())
    {
        Directory.CreateDirectory(path);
    }
    else
    {
        Directory.CreateDirectory(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    return Path.GetFullPath(path);
}
