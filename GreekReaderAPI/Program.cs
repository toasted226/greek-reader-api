using Microsoft.OpenApi;
using Dapper;
using DbUp;
using GreekReaderAPI.Data;
using GreekReaderAPI.Email;
using GreekReaderAPI.Endpoints;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
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

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILexiconService, LexiconService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}
else
{
    // TODO: implement production email sending
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
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

var app = builder.Build();
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

app.Run("http://localhost:3000");