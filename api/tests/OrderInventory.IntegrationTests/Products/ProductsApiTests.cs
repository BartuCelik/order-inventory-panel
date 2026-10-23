using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Products;

/// <summary>
/// The HTTP contract only: routes, status codes, the ProblemDetails shape and enums as names.
/// The behaviour behind it is covered by the service tests.
/// </summary>
public sealed class ProductsApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Create_ThenGetLocation_ReturnsTheProductWithEnumNames()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();
        var code = TestData.NewCode("API");

        using var created = await client.PostAsJsonAsync("/products", NewProduct(code, "Finished"), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var product = await client.GetFromJsonAsync<JsonElement>(created.Headers.Location, cancellationToken);
        Assert.Equal(code, product.GetProperty("code").GetString());
        Assert.Equal("Finished", product.GetProperty("type").GetString());
        Assert.Equal("Piece", product.GetProperty("unit").GetString());
        Assert.Equal(0, product.GetProperty("bomLines").GetArrayLength());
    }

    [Fact]
    public async Task Create_DuplicateCode_Returns409Problem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();
        var code = TestData.NewCode("DUP");
        (await client.PostAsJsonAsync("/products", NewProduct(code, "RawMaterial"), cancellationToken)).Dispose();

        using var response = await client.PostAsJsonAsync("/products", NewProduct(code, "RawMaterial"), cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_UnknownTypeName_Returns400()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();

        using var response = await client.PostAsJsonAsync("/products", NewProduct(TestData.NewCode("BAD"), "Gizmo"), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingName_Returns400()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/products", new { code = TestData.NewCode("NONAME"), type = "Finished", unit = "Piece" }, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404Problem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();

        using var response = await client.GetAsync($"/products/{int.MaxValue}", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddBomLine_RawMaterialParent_Returns422WithViolationCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();
        var parentId = await CreateAsync(client, "RawMaterial", cancellationToken);
        var componentId = await CreateAsync(client, "RawMaterial", cancellationToken);

        using var response = await client.PostAsJsonAsync(
            $"/products/{parentId}/bom-lines", new { componentProductId = componentId, qtyPerUnit = 1 }, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("ParentIsRawMaterial", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task BomLine_AddUpdateDelete_RoundTrips()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();
        var parentId = await CreateAsync(client, "Finished", cancellationToken);
        var componentId = await CreateAsync(client, "RawMaterial", cancellationToken);

        using var added = await client.PostAsJsonAsync(
            $"/products/{parentId}/bom-lines", new { componentProductId = componentId, qtyPerUnit = 2.5 }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var lineId = (await added.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt32();

        using var updated = await client.PutAsJsonAsync(
            $"/products/{parentId}/bom-lines/{lineId}", new { qtyPerUnit = 3 }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(3m, (await updated.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("qtyPerUnit").GetDecimal());

        using var deleted = await client.DeleteAsync($"/products/{parentId}/bom-lines/{lineId}", cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsThePagingShape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();
        await CreateAsync(client, "Finished", cancellationToken);

        var page = await client.GetFromJsonAsync<JsonElement>("/products?page=1&pageSize=1", cancellationToken);

        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(1, page.GetProperty("pageSize").GetInt32());
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task List_PageSizeAboveLimit_Returns400()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = api.CreateClient();

        using var response = await client.GetAsync("/products?pageSize=500", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object NewProduct(string code, string type) =>
        new { code, name = $"{code} product", type, unit = "Piece", minStock = 0 };

    private static async Task<int> CreateAsync(HttpClient client, string type, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("/products", NewProduct(TestData.NewCode("API"), type), cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt32();
    }
}
