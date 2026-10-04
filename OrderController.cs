using System.Security.Claims;
using System.Text.Json;
using EventXpress.Data;
using EventXpress.Hubs;
using EventXpress.Models;
using EventXpress.Models.ViewModels;
using EventXpress.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Ordering, Payment & Order Maintenance module (Chua Chi Yang)
    [Authorize(Roles = "Customer")]
    public class OrderController : Controller
    {
        private const string SessionKey = "Cart";
        private readonly ApplicationDbContext _db;
        private readonly IQrCodeService _qr;
        private readonly IEmailService _emailService;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<OrderController> _logger;

        public OrderController(ApplicationDbContext db, IQrCodeService qr, IEmailService emailService, IHubContext<NotificationHub> hub, ILogger<OrderController> logger)
        {
            _db = db;
            _qr = qr;
            _emailService = emailService;
            _hub = hub;
            _logger = logger;
        }

        private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private List<CartLineItem> GetCart()
        {
            var json = HttpContext.Session.GetString(SessionKey);
            return json == null ? new List<CartLineItem>() : JsonSerializer.Deserialize<List<CartLineItem>>(json)!;
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }
            return View(new CheckoutViewModel { Cart = new CartViewModel { Items = cart } });
        }

        // Checkout process: creates the Order, records the (simulated) Payment,
        // issues one OrderItem + QR ticket per unit, and clears the cart/seat holds.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel vm)
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                vm.Cart = new CartViewModel { Items = cart };
                return View(vm);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    OrderNumber = $"EX-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}",
                    CustomerId = CurrentUserId(),
                    TotalAmount = cart.Sum(c => c.Subtotal),
                    Status = OrderStatus.Paid
                };
                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                var organizerIdsToNotify = new HashSet<int>();

                foreach (var line in cart)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        TicketTypeId = line.TicketTypeId,
                        SeatId = line.SeatId,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        TicketCode = Guid.NewGuid().ToString("N")[..12].ToUpper()
                    };
                    orderItem.QrCodeBase64 = _qr.GenerateBase64(
                        $"EVENTXPRESS|Order:{order.OrderNumber}|Ticket:{orderItem.TicketCode}|EventId:{line.EventId}");

                    _db.OrderItems.Add(orderItem);

                    if (line.SeatId.HasValue)
                    {
                        var seat = await _db.Seats.FindAsync(line.SeatId.Value);
                        if (seat != null)
                        {
                            seat.Status = SeatStatus.Booked;
                            seat.HeldUntilUtc = null;
                        }
                    }
                    else
                    {
                        var tt = await _db.TicketTypes.FindAsync(line.TicketTypeId);
                        if (tt != null) tt.QuantitySold += line.Quantity;
                    }

                    var ev = await _db.Events.FindAsync(line.EventId);
                    if (ev != null) organizerIdsToNotify.Add(ev.OrganizerId);
                }

                _db.Payments.Add(new Payment
                {
                    OrderId = order.OrderId,
                    Method = vm.PaymentMethod,
                    AmountPaid = order.TotalAmount,
                    // Simulated payment: whatever reference the customer typed
                    // in is stored as-is; there's no real gateway to verify it
                    // against, so every submission is treated as successful.
                    TransactionRef = vm.ReferenceNumber,
                    IsSuccessful = true
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                HttpContext.Session.Remove(SessionKey);

                TempData["Message"] = "Payment successful! Your booking is confirmed.";

                // The payment is already committed above — a failure sending the
                // confirmation email or the live organizer notification must NOT
                // make the customer see an error after they've actually been
                // charged successfully. Best-effort only; log and move on.
                try
                {
                    var customer = await _db.Users.FindAsync(CurrentUserId());
                    await _emailService.SendOrderConfirmationAsync(
                        customer!.Email, order.OrderNumber, Url.Action("Confirmation", "Order", new { id = order.OrderId })!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Order {OrderNumber} paid successfully but the confirmation email failed to send.", order.OrderNumber);
                }

                try
                {
                    // Additional Feature: real-time order/sales update notification
                    foreach (var organizerId in organizerIdsToNotify)
                    {
                        await _hub.Clients.Group($"organizer-{organizerId}")
                            .SendAsync("NewOrder", new { orderNumber = order.OrderNumber, amount = order.TotalAmount });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Order {OrderNumber} paid successfully but the live organizer notification failed to send.", order.OrderNumber);
                }

                return RedirectToAction(nameof(Confirmation), new { id = order.OrderId });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "One or more seats in your cart were just booked by someone else. Please review your cart.";
                return RedirectToAction("Index", "Cart");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Checkout failed for user {UserId}.", CurrentUserId());
                TempData["Error"] = "Something went wrong while processing your payment. You have not been charged — please try again.";
                return RedirectToAction("Index", "Cart");
            }
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var order = await LoadOrderForCustomer(id);
            if (order == null) return NotFound();
            return View(new OrderConfirmationViewModel { Order = order });
        }

        public async Task<IActionResult> History()
        {
            var orders = await _db.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.TicketType).ThenInclude(t => t!.Event)
                .Where(o => o.CustomerId == CurrentUserId())
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync();
            return View(orders);
        }

        // Additional Feature: e-invoice generation & print (browser print-to-PDF friendly view)
        public async Task<IActionResult> Invoice(int id)
        {
            var order = await LoadOrderForCustomer(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await LoadOrderForCustomer(id);
            if (order == null) return NotFound();

            if (order.Status != OrderStatus.Paid)
            {
                TempData["Error"] = "Only paid orders can be cancelled.";
                return RedirectToAction(nameof(History));
            }

            order.Status = OrderStatus.Cancelled;
            foreach (var item in order.OrderItems)
            {
                if (item.SeatId.HasValue)
                {
                    var seat = await _db.Seats.FindAsync(item.SeatId.Value);
                    if (seat != null) seat.Status = SeatStatus.Available;
                }
                else if (item.TicketType != null)
                {
                    item.TicketType.QuantitySold -= item.Quantity;
                }
            }
            await _db.SaveChangesAsync();

            TempData["Message"] = "Order cancelled.";
            return RedirectToAction(nameof(History));
        }

        private async Task<Order?> LoadOrderForCustomer(int id)
        {
            return await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.TicketType).ThenInclude(t => t!.Event).ThenInclude(e => e!.Venue)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Seat)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == CurrentUserId());
        }
    }
}
