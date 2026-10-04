using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventXpress.Models
{
    public enum EventStatus
    {
        Draft,
        Published,
        Cancelled,
        Completed
    }

    // Event Maintenance / Catalog module (Fang Quan Shen)
    public class Event
    {
        [Key]
        public int EventId { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime StartDateTime { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime EndDateTime { get; set; }

        [StringLength(300)]
        public string? BannerImagePath { get; set; }

        [Required]
        public EventStatus Status { get; set; } = EventStatus.Draft;

        // FK: Organizer who created the event
        [Required]
        public int OrganizerId { get; set; }
        [ForeignKey(nameof(OrganizerId))]
        public User? Organizer { get; set; }

        [Required]
        public int CategoryId { get; set; }
        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        [Required]
        public int VenueId { get; set; }
        [ForeignKey(nameof(VenueId))]
        public Venue? Venue { get; set; }

        public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        [NotMapped]
        public double AverageRating => Reviews.Any() ? Reviews.Average(r => r.Rating) : 0;
    }
}
