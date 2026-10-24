using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Products;

public sealed class ProductsApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    // The tests in this class share one database, so every test uses its own code prefix.
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // ---------- POST ----------

    [Fact]
    public async Task Post_ValidProduct_Returns201WithLocationAndZeroOnHand()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            NewProduct("POST-OK", ProductType.RawMaterial, minStock: 5m), ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<ProductResponse>(response);
        Assert.Equal($"/api/products/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("POST-OK", created.Code);
        Assert.Equal(ProductType.RawMaterial, created.Type);
        Assert.Equal(UnitOfMeasure.SquareMeter, created.Unit);
        Assert.Equal(5m, created.MinStock);
        Assert.True(created.IsActive);
        Assert.Equal(0m, created.OnHand);
    }

    [Fact]
    public async Task Post_TrimsCodeAndName()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            NewProduct("  POST-TRIM  ", ProductType.Finished) with { Name = "  Oak bench  " }, ApiFactory.JsonOptions, CancellationToken);

        var created = await ReadAsync<ProductResponse>(response);
        Assert.Equal("POST-TRIM", created.Code);
        Assert.Equal("Oak bench", created.Name);
    }

    [Fact]
    public async Task Post_DuplicateCode_Returns409()
    {
        await CreateAsync(NewProduct("POST-DUP", ProductType.RawMaterial));

        var response = await _client.PostAsJsonAsync("/api/products",
            NewProduct("POST-DUP", ProductType.Finished), ApiFactory.JsonOptions, CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "POST-DUP");
    }

    [Fact]
    public async Task Post_InvalidFields_Returns400WithFieldErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new ProductRequest("", "", ProductType.Finished, UnitOfMeasure.Piece, -1m, true), ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        Assert.Contains("Code", problem.Errors.Keys);
        Assert.Contains("Name", problem.Errors.Keys);
        Assert.Contains("MinStock", problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_UnknownEnumValue_Returns400()
    {
        var response = await _client.PostAsync("/api/products", JsonContent.Create(new
        {
            code = "POST-ENUM",
            name = "Bad type",
            type = "Liquid",
            unit = "Piece",
            minStock = 0,
            isActive = true,
        }), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "code": "POST-MISS", "name": "No type", "unit": "Piece", "minStock": 0 }""")]
    [InlineData("""{ "code": "POST-INT", "name": "Numeric type", "type": 7, "unit": "Piece", "minStock": 0 }""")]
    public async Task Post_MissingOrNumericEnum_Returns400(string json)
    {
        // Without this a missing type would silently become RawMaterial, and 7 would be stored as an undefined type.
        var response = await _client.PostAsync("/api/products",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- GET by id ----------

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/products/999999", CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "999999");
    }

    [Fact]
    public async Task Get_OnHand_IsSumOfMovements()
    {
        var product = await CreateAsync(NewProduct("GET-ONHAND", ProductType.RawMaterial));
        await AddMovementsAsync(product.Id, 10m, -3m, 0.5m);

        var fetched = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(7.5m, fetched!.OnHand);
    }

    // ---------- GET list ----------

    [Fact]
    public async Task List_SearchesCodeAndNameCaseInsensitively_OrderedByCode()
    {
        await CreateAsync(NewProduct("LST-S-2", ProductType.RawMaterial));
        await CreateAsync(NewProduct("LST-S-1", ProductType.RawMaterial));
        await CreateAsync(NewProduct("OTHER-1", ProductType.RawMaterial) with { Name = "Matches lst-s- by name" });
        await CreateAsync(NewProduct("OTHER-2", ProductType.RawMaterial));

        var page = await ListAsync("search=lst-s-");

        Assert.Equal(["LST-S-1", "LST-S-2", "OTHER-1"], page.Items.Select(p => p.Code));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task List_FiltersByType()
    {
        await CreateAsync(NewProduct("LST-T-RAW", ProductType.RawMaterial));
        await CreateAsync(NewProduct("LST-T-SEMI", ProductType.SemiFinished));
        await CreateAsync(NewProduct("LST-T-FIN", ProductType.Finished));

        var page = await ListAsync("search=LST-T-&type=SemiFinished");

        Assert.Equal(["LST-T-SEMI"], page.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task List_Pages_ReturnsRequestedPageAndTotalCount()
    {
        for (var i = 1; i <= 5; i++)
        {
            await CreateAsync(NewProduct($"LST-P-{i}", ProductType.RawMaterial));
        }

        var page = await ListAsync("search=LST-P-&page=2&pageSize=2");

        Assert.Equal(["LST-P-3", "LST-P-4"], page.Items.Select(p => p.Code));
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
    }

    [Fact]
    public async Task List_DefaultsToFirstPageOf20()
    {
        var page = await ListAsync("search=no-such-product");

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task List_IncludesOnHand()
    {
        var product = await CreateAsync(NewProduct("LST-OH-1", ProductType.RawMaterial));
        await AddMovementsAsync(product.Id, 4m, -1.25m);

        var page = await ListAsync("search=LST-OH-");

        Assert.Equal(2.75m, Assert.Single(page.Items).OnHand);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_InvalidPaging_Returns400(string query)
    {
        var response = await _client.GetAsync($"/api/products?{query}", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- PUT ----------

    [Fact]
    public async Task Put_ValidChange_Returns200WithUpdatedProduct()
    {
        var product = await CreateAsync(NewProduct("PUT-OK", ProductType.SemiFinished));
        await AddMovementsAsync(product.Id, 3m);

        var response = await _client.PutAsJsonAsync($"/api/products/{product.Id}",
            new ProductRequest("PUT-OK-2", "Renamed", ProductType.Finished, UnitOfMeasure.Meter, 2m, false),
            ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<ProductResponse>(response);
        Assert.Equal(new ProductResponse(product.Id, "PUT-OK-2", "Renamed", ProductType.Finished, UnitOfMeasure.Meter, 2m, false, 3m), updated);
    }

    [Fact]
    public async Task Put_KeepingOwnCode_IsAllowed()
    {
        var product = await CreateAsync(NewProduct("PUT-SAME", ProductType.Finished));

        var response = await _client.PutAsJsonAsync($"/api/products/{product.Id}",
            NewProduct("PUT-SAME", ProductType.Finished) with { Name = "New name" }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Put_CodeOfAnotherProduct_Returns409()
    {
        await CreateAsync(NewProduct("PUT-TAKEN", ProductType.Finished));
        var product = await CreateAsync(NewProduct("PUT-MINE", ProductType.Finished));

        var response = await _client.PutAsJsonAsync($"/api/products/{product.Id}",
            NewProduct("PUT-TAKEN", ProductType.Finished), ApiFactory.JsonOptions, CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "PUT-TAKEN");
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/products/999999",
            NewProduct("PUT-404", ProductType.Finished), ApiFactory.JsonOptions, CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "999999");
    }

    [Fact]
    public async Task Put_ToRawMaterialWhileItHasBomLines_Returns422()
    {
        // Rule 5: a raw material cannot have BOM lines, so a product with a BOM cannot become one.
        var seat = await CreateAsync(NewProduct("PUT-BOM-SEAT", ProductType.SemiFinished));
        var plywood = await CreateAsync(NewProduct("PUT-BOM-PLY", ProductType.RawMaterial));
        await using (var scope = api.CreateScope())
        {
            var dbContext = ApiFactory.GetDbContext(scope);
            dbContext.BomLines.Add(new BomLine { ParentProductId = seat.Id, ComponentProductId = plywood.Id, QtyPerUnit = 0.25m });
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        var response = await _client.PutAsJsonAsync($"/api/products/{seat.Id}",
            NewProduct("PUT-BOM-SEAT", ProductType.RawMaterial), ApiFactory.JsonOptions, CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "bill of materials");
    }

    [Fact]
    public async Task Put_InvalidFields_Returns400()
    {
        var product = await CreateAsync(NewProduct("PUT-400", ProductType.Finished));

        var response = await _client.PutAsJsonAsync($"/api/products/{product.Id}",
            NewProduct("PUT-400", ProductType.Finished) with { MinStock = -1m }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- helpers ----------

    private static ProductRequest NewProduct(string code, ProductType type, decimal minStock = 0m) =>
        new(code, $"Product {code}", type, UnitOfMeasure.SquareMeter, minStock, true);

    private async Task<ProductResponse> CreateAsync(ProductRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/products", request, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ProductResponse>(response);
    }

    private async Task<PagedResponse<ProductResponse>> ListAsync(string query)
    {
        var response = await _client.GetAsync($"/api/products?{query}", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<PagedResponse<ProductResponse>>(response);
    }

    private async Task AddMovementsAsync(int productId, params decimal[] quantities)
    {
        await using var scope = api.CreateScope();
        var dbContext = ApiFactory.GetDbContext(scope);
        dbContext.StockMovements.AddRange(quantities.Select(q => new StockMovement
        {
            ProductId = productId,
            Quantity = q,
            Type = StockMovementType.Adjustment,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow,
        }));
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.JsonOptions, CancellationToken))!;

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string detailContains)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync<ProblemDetails>(response);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Contains(detailContains, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ProductRequest(
        string Code, string Name, ProductType Type, UnitOfMeasure Unit, decimal MinStock, bool IsActive);

    private sealed record ProductResponse(
        int Id, string Code, string Name, ProductType Type, UnitOfMeasure Unit, decimal MinStock, bool IsActive, decimal OnHand);

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
}
