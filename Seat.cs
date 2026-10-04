using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventXpress.Models
{
    public enum SeatStatus
    {
        Available,
        Held,      // in someone's cart, temporarily reserved
        Booked
    }

    // Interactive seat map (Additional Feature). One row per physical seat per venue,
    // linked to a TicketType once an event assigns pricing tiers to seat ranges.
    public class Seat
    {
        [Key]
        public int SeatId { get; set; }

        [Required]
        public int VenueId { get; set; }
        [ForeignKey(nameof(VenueId))]
        public Venue? Venue { get; set; }

        // Seats are generated per event (by the organizer, based on how many
        // seats they set for that event's ticket type) rather than shared
        // across every event at the venue. Nullable only for backward
        // compatibility with any pre-existing rows.
        public int? EventId { get; set; }
        [ForeignKey(nameof(EventId))]
        public Event? Event { get; set; }

        [Required, StringLength(5)]
        public string RowLabel { get; set; } = string.Empty; // A, B, C...

        [Required]
        public int SeatNumber { get; set; } // 1, 2, 3...

        // Null until an event maps this seat to a ticket type/price tier
        public int? TicketTypeId { get; set; }
        [ForeignKey(nameof(TicketTypeId))]
        public TicketType? TicketType { get; set; }

        public SeatStatus Status { get; set; } = SeatStatus.Available;

        public DateTime? HeldUntilUtc { get; set; } // cart hold expiry (e.g. 10 min)

        // House seat: reserved by the organizer (comps, press, production holds).
        // Not tied to a TicketType/price and never purchasable by customers, but
        // still rendered on the seat map so the venue layout looks complete.
        public bool IsHouseSeat { get; set; } = false;

        // Concurrency token: prevents two customers from booking the same seat
        // simultaneously (equivalent of the "SELECT ... FOR UPDATE" row lock
        // used in the AMIT3253 PHP booking system).
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        [NotMapped]
        public string SeatCode => $"{RowLabel}{SeatNumber}";
    }
}
