using System.Text.Json.Serialization;

namespace PrivateGym.Api.DTOs;

public class CreateBookingDto
{
    [JsonPropertyName("roomId")]
    public int RoomId { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("peopleCount")]
    public int PeopleCount { get; set; } = 1;
}

public class BookingResponseDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public int PeopleCount { get; set; }
}