namespace Physiquinator.Models;

public sealed class UserProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
