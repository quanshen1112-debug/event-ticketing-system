using System.ComponentModel.DataAnnotations;

namespace EventXpress.Models.ViewModels
{
    public class TicketTypeFormViewModel
    {
        public int TicketTypeId { get; set; }

        [Required]
        public int EventId { get; set; }

        [Required, StringLength(60)]
        public string Name { get; set; } = string.Empty;

        [Required, Range(0, 100000)]
        public decimal Price { get; set; }

        public int? QuantityAvailable { get; set; }

        // Seated venues only: how many seats the organizer wants for this
        // tier. The system auto-generates that many seats in a square-ish
        // grid instead of the organizer picking each seat by hand.
        [Range(1, 5000)]
        public int? SeatQuantity { get; set; }
    }

    // Interactive click-to-select seat map (Additional Feature)
    public class SeatMapViewModel
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public bool IsSeated { get; set; }
        public int Rows { get; set; }
        public int SeatsPerRow { get; set; }
        public List<SeatCellViewModel> Seats { get; set; } = new();
        public List<TicketType> TicketTypes { get; set; } = new(); // for GA / quantity-based events
    }

    public class SeatCellViewModel
    {
        public int SeatId { get; set; }
        public string RowLabel { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public string Status { get; set; } = "Available"; // Available / Held / Booked
        public string? TicketTypeName { get; set; }
        public decimal Price { get; set; }
    }
}
