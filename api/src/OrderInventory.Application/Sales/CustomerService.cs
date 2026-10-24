using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Sales;

namespace OrderInventory.Application.Sales;

public sealed class CustomerService(IAppDbContext db)
{
    private static readonly Expression<Func<Customer, CustomerDto>> ToDto =
        c => new CustomerDto(c.Id, c.Name, c.ContactName, c.Email, c.Phone);

    private static readonly Func<Customer, CustomerDto> ToDtoInMemory = ToDto.Compile();

    public async Task<PagedResult<CustomerDto>> ListAsync(CustomerListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customers = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            customers = customers.Where(c => EF.Functions.ILike(c.Name, pattern, LikePattern.Escape)
                || (c.ContactName != null && EF.Functions.ILike(c.ContactName, pattern, LikePattern.Escape)));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .OrderBy(c => c.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<CustomerDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await db.Customers.Where(c => c.Id == id).Select(ToDto).SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound(id);

    public async Task<CustomerDto> CreateAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customer = new Customer { Name = request.Name.Trim() };
        Apply(request, customer);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return ToDtoInMemory(customer);
    }

    public async Task<CustomerDto> UpdateAsync(int id, CustomerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw NotFound(id);
        customer.Name = request.Name.Trim();
        Apply(request, customer);
        await db.SaveChangesAsync(cancellationToken);
        return ToDtoInMemory(customer);
    }

    private static void Apply(CustomerRequest request, Customer customer)
    {
        customer.ContactName = Blank(request.ContactName);
        customer.Email = Blank(request.Email);
        customer.Phone = Blank(request.Phone);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NotFoundException NotFound(int id) => new($"Customer {id} was not found.");
}
