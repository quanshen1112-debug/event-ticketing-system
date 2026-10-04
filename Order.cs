using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventXpress.Models
{
    public enum OrderStatus
    {
        PendingPayment,
        Paid,
        Cancelled,
        Refunded
    }

    // Ordering / Order Maintenance module (Chua Chi Yang)
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [Required, StringLength(20)]
        public string OrderNumber { get; set; } = string.Empty; // e.g. EX-20260706-000123

        [Required]
        public int CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public User? Customer { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Payment? Payment { get; set; }
    }

    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }

        [Required]
        public int OrderId { get; set; }
        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        [Required]
        public int TicketTypeId { get; set; }
        [ForeignKey(nameof(TicketTypeId))]
        public TicketType? TicketType { get; set; }

        // Null for quantity-based (GA) tickets, set for seated tickets
        public int? SeatId { get; set; }
        [ForeignKey(nameof(SeatId))]
        public Seat? Seat { get; set; }

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        // Additional Feature: unique QR payload per issued ticket
        public string? TicketCode { get; set; }
        public string? QrCodeBase64 { get; set; }

        [NotMapped]
        public decimal Subtotal => UnitPrice * Quantity;
    }

    public enum PaymentMethod
    {
        CreditCard,
        OnlineBanking,
        EWallet
    }

    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        public int OrderId { get; set; }
        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        [Required]
        public PaymentMethod Method { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal AmountPaid { get; set; }

        // Simulated payment reference (no real 3rd-party gateway integrated)
        [StringLength(60)]
        public string TransactionRef { get; set; } = string.Empty;

        public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsSuccessful { get; set; } = true;
    }
}
