using Microsoft.OpenApi;
using Dapper;
using DbUp;
using GreekReaderAPI.Data;
using GreekReaderAPI.Email;
using GreekReaderAPI.Endpoints;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
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

builder.Services.AddOpenApi(options => { options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1; });

// Dependency Injection
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILoginTokenRepository, LoginTokenRepository>();
builder.Services.AddScoped<ILexiconRepository, LexiconRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILexiconService, LexiconService>();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
    //builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
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
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.None;
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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        var retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan remaining)
            ? Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds))
            : 0;

        ctx.HttpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "Too many OTP requests. Please try again later.",
                retryAfterSeconds = retryAfter
            },
            ct);
    };
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
});

var app = builder.Build();
app.UseRouting();
app.UseCors("AllowAngular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

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
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "GreekReaderAPI v1"));
}

app.MapGet("/", () => new { Message = "Hello, world!" });
app.MapAuthEndpoints();
app.MapLexiconEndpoints();

app.Run();
