using Microsoft.OpenApi;
using Dapper;
using DbUp;
using GreekReaderAPI.Data;

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

DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddHealthChecks().AddNpgSql();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => { options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1; });

var app = builder.Build();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "GreekReaderAPI v1"); });
}

app.MapGet("/", () => new { Message = "Hello, world!" });

app.MapGet("/hi/{name}", (string name) =>
    new { Message = $"Hello, {name}!" }
);

app.Run("http://localhost:3000");