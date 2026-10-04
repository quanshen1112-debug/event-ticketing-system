using System.ComponentModel.DataAnnotations;

namespace EventXpress.Models
{
    // Venue Maintenance module (Fang Quan Shen)
    public class Venue
    {
        [Key]
        public int VenueId { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Range(1, 200000)]
        public int Capacity { get; set; }

        // Additional Feature: Maps integration to display venue location
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public bool IsSeated { get; set; } = true; // seated (has SeatMap) vs standing/GA (quantity-based)

        public int Rows { get; set; } = 10;    // used to auto-generate a simple seat grid when IsSeated
        public int SeatsPerRow { get; set; } = 10;

        public ICollection<Event> Events { get; set; } = new List<Event>();
        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
