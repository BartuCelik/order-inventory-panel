using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Domain.Products;

namespace OrderInventory.IntegrationTests.Infrastructure;

/// <summary>Shared setup and assertions for tests that drive the API over HTTP.</summary>
internal static class ApiClientExtensions
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<int> CreateProductAsync(
        this HttpClient client, string code, ProductType type, UnitOfMeasure unit = UnitOfMeasure.Piece)
    {
        var response = await client.PostAsJsonAsync("/products",
            new { code, name = $"Product {code}", type, unit, minStock = 0m }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync<IdOnly>()).Id;
    }

    public static async Task AddBomLineAsync(this HttpClient client, int parentId, int componentId, decimal qtyPerUnit)
    {
        var response = await client.PostAsJsonAsync($"/products/{parentId}/bom-lines",
            new { componentProductId = componentId, qtyPerUnit }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public static async Task ReceiveAsync(this HttpClient client, int productId, decimal quantity)
    {
        var response = await client.PostAsJsonAsync("/stock/movements",
            new { productId, type = "Receipt", quantity }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public static async Task<decimal> OnHandAsync(this HttpClient client, int productId) =>
        (await client.GetFromJsonAsync<OnHandOnly>($"/products/{productId}", ApiFactory.JsonOptions, CancellationToken))!.OnHand;

    public static async Task<int> CreateCustomerAsync(this HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/customers", new { name }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync<IdOnly>()).Id;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.JsonOptions, CancellationToken))!;

    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus, string detailContains)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadAsync<ProblemDetails>();
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Contains(detailContains, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record IdOnly(int Id);

    private sealed record OnHandOnly(decimal OnHand);
}
