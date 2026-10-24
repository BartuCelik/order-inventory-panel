using OrderInventory.Api.Errors;
using OrderInventory.Api.HealthChecks;
using OrderInventory.Application;
using OrderInventory.Infrastructure;
using OrderInventory.Infrastructure.Persistence;
using OrderInventory.Infrastructure.Persistence.Seeding;

const string ClientCorsPolicy = "Client";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Every error response is RFC 7807 ProblemDetails: validation (400, from [ApiController]),
// application errors (404/409/422, from ApplicationExceptionHandler) and anything unexpected (500).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

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
    app.MapOpenApi();
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
// Bodiless error responses, such as 404 for an unknown route, also get a ProblemDetails body.
app.UseStatusCodePages();

app.UseCors(ClientCorsPolicy);
app.UseAuthorization();

app.MapControllers();
app.MapHealthCheckEndpoints();

await app.RunAsync();
