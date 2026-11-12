using System.ComponentModel.DataAnnotations;

namespace PrivateGym.Api.Models
{
    public class Room
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public int Capacity { get; set; }

        public decimal PricePerHour { get; set; }

        public decimal BasePrice { get; set; }
        public decimal PricePerAdditionalPerson { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public List<string> Equipment { get; set; } = new();
        public List<string> MuscleGroups { get; set; } = new();
        public bool IsActive { get; set; } = true;

        public List<Booking> Bookings { get; set; } = new();
    }
}