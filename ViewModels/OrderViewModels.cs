using System.ComponentModel.DataAnnotations;

namespace EventXpress.Models.ViewModels
{
    // Session-based cart line item (Booking / Cart module - Wong Pang Yang)
    public class CartLineItem
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int? SeatId { get; set; }     // set for seated events
        public string? SeatCode { get; set; }

        public decimal Subtotal => UnitPrice * Quantity;
    }

    public class CartViewModel
    {
        public List<CartLineItem> Items { get; set; } = new();
        public decimal Total => Items.Sum(i => i.Subtotal);
    }

    public class CheckoutViewModel
    {
        public CartViewModel Cart { get; set; } = new();

        [Required(ErrorMessage = "Please choose a payment method.")]
        public PaymentMethod PaymentMethod { get; set; }

        // Simulated payment: the customer just enters whatever reference/
        // transaction number their payment method gave them (or any value —
        // it isn't verified against a real gateway). It's stored as-is on
        // the Payment record; the transaction is always treated as successful.
        [Required(ErrorMessage = "Please enter your payment reference number.")]
        [StringLength(60)]
        [Display(Name = "Reference / Transaction Number")]
        public string ReferenceNumber { get; set; } = string.Empty;
    }

    public class OrderConfirmationViewModel
    {
        public Order Order { get; set; } = null!;
    }

    // Organizer/Admin sales dashboard (Reporting module - Chua Chi Yang)
    public class SalesReportViewModel
    {
        public int? EventId { get; set; } // null = across all of the organizer's events
        public List<Event> EventOptions { get; set; } = new();

        public decimal TotalRevenue { get; set; }
        public int TotalTicketsSold { get; set; }
        public int TotalOrders { get; set; }

        public List<string> ChartLabels { get; set; } = new();       // e.g. event titles or dates
        public List<decimal> RevenueByEvent { get; set; } = new();   // for column chart
        public List<int> TicketsByCategory { get; set; } = new();    // for pie chart
        public List<string> CategoryLabels { get; set; } = new();
    }
}
