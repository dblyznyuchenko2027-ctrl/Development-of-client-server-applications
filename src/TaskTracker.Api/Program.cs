using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Data;
using TaskTracker.Api.Extensions;
using TaskTracker.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:Default' is not configured. " +
        "Set it via user-secrets or the ConnectionStrings__Default environment variable (see README).");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<DbCommandCounter>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        // Dev diagnostics: number of SQL commands per request -> X-Db-Query-Count header (N+1 detection).
        options.AddInterceptors(serviceProvider.GetRequiredService<DbCommandCounter>());
    }
});
builder.Services.AddApplicationServices();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)))
    .AddApiValidationProblemDetails();

builder.Services.AddApiErrorHandling();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDbQueryCountHeader();
}

// Any unhandled exception -> RFC 7807 JSON; empty 404/405/415 responses -> RFC 7807 JSON as well.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Dev convenience only: apply pending migrations and seed demo data.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Needed for WebApplicationFactory<Program> in integration tests (stage 3).
public partial class Program;
