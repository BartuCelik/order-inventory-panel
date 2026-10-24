namespace OrderInventory.Application.Abstractions;

/// <summary>
/// Who is making the request. Stock movements record this as CreatedBy; it never comes from the request body.
/// </summary>
public interface ICurrentUser
{
    string Name { get; }
}
