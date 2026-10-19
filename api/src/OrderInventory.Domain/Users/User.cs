using OrderInventory.Domain.Common;

namespace OrderInventory.Domain.Users;

public sealed class User : Entity
{
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
}
