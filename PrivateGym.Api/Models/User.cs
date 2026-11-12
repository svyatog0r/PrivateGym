namespace PrivateGym.Api.Models;

public enum UserRole
{
    Client,
    Admin
}

public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Client;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}