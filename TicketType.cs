using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventXpress.Models
{
    // Ticket & Seat Maintenance module (Wong Pang Yang)
    public class TicketType
    {
        [Key]
        public int TicketTypeId { get; set; }

        [Required, StringLength(60)]
        public string Name { get; set; } = string.Empty; // VIP, Standard, Early Bird

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Range(0, 100000)]
        public decimal Price { get; set; }

        // For standing/GA (quantity-based) events
        public int? QuantityAvailable { get; set; }
        public int QuantitySold { get; set; } = 0;

        [Required]
        public int EventId { get; set; }
        [ForeignKey(nameof(EventId))]
        public Event? Event { get; set; }

        public ICollection<Seat> Seats { get; set; } = new List<Seat>();

        [NotMapped]
        public int? QuantityRemaining => QuantityAvailable.HasValue ? QuantityAvailable - QuantitySold : null;
    }
}
