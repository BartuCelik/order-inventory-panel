using SiparisStok.Api.HealthChecks;
using SiparisStok.Application;
using SiparisStok.Infrastructure;
using SiparisStok.Infrastructure.Persistence;

const string ClientCorsPolicy = "Client";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
    await app.Services.MigrateDatabaseAsync();
}

app.UseCors(ClientCorsPolicy);
app.UseAuthorization();

app.MapControllers();
app.MapHealthCheckEndpoints();

await app.RunAsync();
