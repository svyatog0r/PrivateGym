namespace PrivateGym.Api.Models;

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public decimal TotalPrice { get; set; }
    public int PeopleCount { get; set; } = 1;
    // Вместо строк теперь коллекция объектов
    public List<BookingGuest> Guests { get; set; } = new();
}