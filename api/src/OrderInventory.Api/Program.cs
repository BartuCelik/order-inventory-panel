using System.Text.Json.Serialization;
using OrderInventory.Api.Auth;
using OrderInventory.Api.ErrorHandling;
using OrderInventory.Api.HealthChecks;
using OrderInventory.Application;
using OrderInventory.Application.Abstractions;
using OrderInventory.Infrastructure;
using OrderInventory.Infrastructure.Persistence;
using OrderInventory.Infrastructure.Persistence.Seeding;

const string ClientCorsPolicy = "Client";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Enums travel as names ("Finished", "Meter"); numbers are rejected so a typo cannot become a valid value.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddOpenApi();

// Who is calling: CreatedBy on stock movements comes from here, never from the request body.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy(ClientCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Startup migration is safe while the API runs as a single instance (local dev and the demo host).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

// The seeder writes nothing when the database already has data, so it can run on every start.
if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
{
    await app.Services.SeedDemoDataAsync();
}

app.UseExceptionHandler();
// 401/403 from the auth middleware have no body; this gives them a ProblemDetails one like every other error.
app.UseStatusCodePages();
app.UseCors(ClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthCheckEndpoints();

await app.RunAsync();
