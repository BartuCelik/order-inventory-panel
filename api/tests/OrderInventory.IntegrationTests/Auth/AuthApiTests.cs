using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Users;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Auth;

public sealed class AuthApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // ---------- login ----------

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        await AddUserAsync("ana@auth.test", UserRole.Planner);
        var client = api.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "ana@auth.test", password = Password }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.ReadAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        Assert.True(login.ExpiresAt > DateTime.UtcNow);
        Assert.Equal("ana@auth.test", login.User.Email);
        Assert.Equal(UserRole.Planner, login.User.Role);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        await AddUserAsync("case@auth.test", UserRole.Warehouse);

        var response = await api.CreateAnonymousClient().PostAsJsonAsync("/auth/login",
            new { email = "  Case@Auth.TEST ", password = Password }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_TokenWorksOnProtectedEndpoints()
    {
        await AddUserAsync("token@auth.test", UserRole.Warehouse);
        var client = api.CreateAnonymousClient();
        var login = await (await client.PostAsJsonAsync("/auth/login",
            new { email = "token@auth.test", password = Password }, ApiFactory.JsonOptions, CancellationToken)).ReadAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var response = await client.GetAsync("/products", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await AddUserAsync("wrong@auth.test", UserRole.Admin);

        var response = await api.CreateAnonymousClient().PostAsJsonAsync("/auth/login",
            new { email = "wrong@auth.test", password = "guess" }, ApiFactory.JsonOptions, CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "email or password is incorrect");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401WithTheSameMessage()
    {
        // Same answer as a wrong password, so the endpoint does not reveal which emails have accounts.
        var response = await api.CreateAnonymousClient().PostAsJsonAsync("/auth/login",
            new { email = "nobody@auth.test", password = Password }, ApiFactory.JsonOptions, CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "email or password is incorrect");
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400()
    {
        var response = await api.CreateAnonymousClient().PostAsJsonAsync("/auth/login",
            new { email = "" }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- authentication ----------

    [Theory]
    [InlineData("/products")]
    [InlineData("/stock/movements")]
    [InlineData("/customers")]
    [InlineData("/sales-orders")]
    [InlineData("/work-orders")]
    [InlineData("/dashboard")]
    public async Task Endpoints_WithoutToken_Return401Problem(string url)
    {
        var response = await api.CreateAnonymousClient().GetAsync(url, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Endpoints_WithTamperedToken_Return401()
    {
        var client = api.CreateAnonymousClient();
        var token = api.IssueToken(UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^2] + "xx");

        var response = await client.GetAsync("/products", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthLive_WithoutToken_Returns200()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/health/live", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- roles (CLAUDE.md roles table) ----------

    [Fact]
    public async Task Warehouse_CanReadButNotEditProducts()
    {
        var client = api.CreateClientAs(UserRole.Warehouse);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/products", CancellationToken)).StatusCode);
        var create = await client.PostAsJsonAsync("/products",
            new { code = "AU-WH-1", name = "Nope", type = ProductType.RawMaterial, unit = UnitOfMeasure.Piece, minStock = 0m },
            ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal("application/problem+json", create.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Planner_CannotRecordStockMovements()
    {
        var productId = await api.CreateClient().CreateProductAsync("AU-PL-1", ProductType.RawMaterial);

        var response = await api.CreateClientAs(UserRole.Planner).PostAsJsonAsync("/stock/movements",
            new { productId, type = "Receipt", quantity = 1m }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Warehouse, "POST", "/customers")]
    [InlineData(UserRole.Warehouse, "POST", "/sales-orders")]
    [InlineData(UserRole.Warehouse, "POST", "/sales-orders/1/cancel")]
    [InlineData(UserRole.Warehouse, "POST", "/work-orders")]
    [InlineData(UserRole.Warehouse, "POST", "/work-orders/1/start")]
    [InlineData(UserRole.Warehouse, "POST", "/work-orders/1/cancel")]
    [InlineData(UserRole.Planner, "POST", "/work-orders/1/complete")]
    [InlineData(UserRole.Planner, "POST", "/sales-orders/1/ship")]
    [InlineData(UserRole.Warehouse, "PUT", "/products/1")]
    [InlineData(UserRole.Warehouse, "POST", "/products/1/bom-lines")]
    [InlineData(UserRole.Warehouse, "DELETE", "/products/1/bom-lines/1")]
    public async Task Role_WithoutPermission_Gets403(UserRole role, string method, string url)
    {
        // Authorization runs before model binding, so an empty body is enough to prove the 403.
        using var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) };

        var response = await api.CreateClientAs(role).SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Warehouse_RecordsMovement_CreatedByIsTheTokenUser()
    {
        var productId = await api.CreateClient().CreateProductAsync("AU-WH-2", ProductType.RawMaterial);
        var client = api.CreateClientAs(UserRole.Warehouse, "keeper@auth.test");

        var response = await client.PostAsJsonAsync("/stock/movements",
            new { productId, type = "Receipt", quantity = 3m }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("keeper@auth.test", (await response.ReadAsync<CreatedByOnly>()).CreatedBy);
    }

    private async Task AddUserAsync(string email, UserRole role)
    {
        await using var scope = api.CreateScope();
        var dbContext = ApiFactory.GetDbContext(scope);
        var user = new User { Email = email, DisplayName = email, PasswordHash = "", Role = role };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private sealed record LoginResponse(string Token, DateTime ExpiresAt, LoginUser User);

    private sealed record LoginUser(string Email, string DisplayName, UserRole Role);

    private sealed record CreatedByOnly(string CreatedBy);
}
