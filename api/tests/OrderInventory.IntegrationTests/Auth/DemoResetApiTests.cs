using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Domain.Products;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Auth;

public sealed class DemoResetApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const string ResetKey = "integration-tests-reset-key";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    /// <summary>The API as the live demo runs it: seeded, with a reset key.</summary>
    private WebApplicationFactory<Program> DemoApi(string? resetKey = ResetKey) =>
        api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:SeedDemoData", "true");
            builder.UseSetting("Demo:ResetKey", resetKey ?? "");
        });

    private static HttpRequestMessage Reset(string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/demo/reset");
        if (key is not null)
        {
            request.Headers.Add("X-Demo-Reset-Key", key);
        }
        return request;
    }

    [Fact]
    public async Task Reset_WithTheKey_RestoresTheSeed()
    {
        await using var demo = DemoApi();
        using var client = demo.CreateClient();
        await using (var scope = demo.Services.CreateAsyncScope())
        {
            var dbContext = ApiFactory.GetDbContext(scope);
            dbContext.Products.Add(new Product { Code = "VISITOR-2", Name = "Visitor product", Type = ProductType.RawMaterial, Unit = UnitOfMeasure.Piece });
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        var response = await client.SendAsync(Reset(ResetKey), CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var check = demo.Services.CreateAsyncScope();
        var products = ApiFactory.GetDbContext(check).Products;
        Assert.False(await products.AnyAsync(p => p.Code == "VISITOR-2", CancellationToken));
        Assert.True(await products.AnyAsync(p => p.Code == "FG-DT-160", CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task Reset_WithoutTheRightKey_IsUnauthorized(string? key)
    {
        await using var demo = DemoApi();
        // No bearer token either: the key is the only credential this endpoint accepts.
        using var client = demo.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.SendAsync(Reset(key), CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reset_WhenNoKeyIsConfigured_DoesNotExist()
    {
        await using var demo = DemoApi(resetKey: null);
        using var client = demo.CreateClient();

        var response = await client.SendAsync(Reset(""), CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reset_WhenTheDemoSeedIsOff_DoesNotExist()
    {
        // The shared factory runs with Database:SeedDemoData=false, like a real deployment.
        await using var demo = api.WithWebHostBuilder(builder => builder.UseSetting("Demo:ResetKey", ResetKey));
        using var client = demo.CreateClient();

        var response = await client.SendAsync(Reset(ResetKey), CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
