namespace PrivateGym.Api.DTOs
{
    public class BulkCreateBookingDto
    {
        // Список диапазонов, которые клиент выбрал за раз
        public List<CreateBookingDto> Bookings { get; set; } = new();
    }
}