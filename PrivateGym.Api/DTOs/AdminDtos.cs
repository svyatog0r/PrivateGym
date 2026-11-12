using PrivateGym.Api.Models;

namespace PrivateGym.Api.DTOs
{
    public class ChartPointDto
    {
        public string Label { get; set; } = string.Empty;
        public int Bookings { get; set; }
        public decimal Revenue { get; set; }
        public int TotalPeople { get; set; }
    }

    public class AdminStatisticsDto
    {
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public int ActiveBookings { get; set; }
        public int TotalRooms { get; set; }
        public double? RevenueChange { get; set; }
        public double? BookingsChange { get; set; }
        public List<ChartPointDto> ChartData { get; set; } = new();
    }

    public class AdminBookingDto
    {
        public int UserId { get; set; }
        public int RoomId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int PeopleCount { get; set; } = 1;
        public string? GuestUserIds { get; set; }
        public string? GuestNames { get; set; }
    }

    public class UpdateStatusDto
    {
        public BookingStatus Status { get; set; }
    }
}