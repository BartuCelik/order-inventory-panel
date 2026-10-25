using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Sales;

public sealed class CustomersApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Valid_Returns201AndTrimsFields()
    {
        var response = await _client.PostAsJsonAsync("/customers",
            new { name = "  Cedar Lodge  ", contactName = "Ines Park", email = "ines@cedarlodge.example.com", phone = "+1 555 0199" },
            ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.ReadAsync<CustomerResponse>();
        Assert.Equal($"/customers/{customer.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(new CustomerResponse(customer.Id, "Cedar Lodge", "Ines Park", "ines@cedarlodge.example.com", "+1 555 0199"), customer);
    }

    [Fact]
    public async Task Post_EmptyOptionalFields_AreStoredAsNull()
    {
        var response = await _client.PostAsJsonAsync("/customers",
            new { name = "Birch & Co", contactName = " ", email = "", phone = (string?)null }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.ReadAsync<CustomerResponse>();
        Assert.Null(customer.ContactName);
        Assert.Null(customer.Email);
        Assert.Null(customer.Phone);
    }

    [Theory]
    [InlineData("", null, "Name")]
    [InlineData("Valid name", "not-an-email", "Email")]
    public async Task Post_InvalidFields_Returns400(string name, string? email, string field)
    {
        var response = await _client.PostAsJsonAsync("/customers", new { name, email }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys);
    }

    [Fact]
    public async Task List_SearchesNameAndContact_OrderedByName()
    {
        await _client.CreateCustomerAsync("CL-Search Zeta Interiors");
        await _client.CreateCustomerAsync("CL-Search Alpha Studio");
        await _client.PostAsJsonAsync("/customers",
            new { name = "Unrelated Ltd", contactName = "cl-search contact" }, ApiFactory.JsonOptions, CancellationToken);

        var page = await _client.GetFromJsonAsync<PagedResponse<CustomerResponse>>(
            "/customers?search=cl-search", ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(["CL-Search Alpha Studio", "CL-Search Zeta Interiors", "Unrelated Ltd"], page!.Items.Select(c => c.Name));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/customers/999999", CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "999999");
    }

    [Fact]
    public async Task Put_UpdatesFields()
    {
        var id = await _client.CreateCustomerAsync("CL-Put Old Name");

        var response = await _client.PutAsJsonAsync($"/customers/{id}",
            new { name = "CL-Put New Name", email = "orders@new.example.com" }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new CustomerResponse(id, "CL-Put New Name", null, "orders@new.example.com", null), await response.ReadAsync<CustomerResponse>());
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/customers/999999", new { name = "Nobody" }, ApiFactory.JsonOptions, CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "999999");
    }

    private sealed record CustomerResponse(int Id, string Name, string? ContactName, string? Email, string? Phone);

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
}
