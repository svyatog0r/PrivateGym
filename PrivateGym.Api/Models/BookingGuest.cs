namespace PrivateGym.Api.Models
{
    public class BookingGuest
    {
        public int Id { get; set; }

        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        // Если гость зарегистрирован в системе
        public int? UserId { get; set; }
        public User? User { get; set; }

        // Если гость не зарегистрирован (введено просто имя)
        public string? GuestName { get; set; }
    }
}