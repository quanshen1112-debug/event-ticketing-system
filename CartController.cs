using System.Security.Claims;
using System.Text.Json;
using EventXpress.Data;
using EventXpress.Models;
using EventXpress.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Booking / Cart module (Wong Pang Yang)
    [Authorize(Roles = "Customer")]
    public class CartController : Controller
    {
        private const string SessionKey = "Cart";
        private readonly ApplicationDbContext _db;

        public CartController(ApplicationDbContext db) => _db = db;

        private List<CartLineItem> GetCart()
        {
            var json = HttpContext.Session.GetString(SessionKey);
            return json == null ? new List<CartLineItem>() : JsonSerializer.Deserialize<List<CartLineItem>>(json)!;
        }

        private void SaveCart(List<CartLineItem> cart)
        {
            HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(cart));
        }

        public IActionResult Index() => View(new CartViewModel { Items = GetCart() });

        // Seat map "click-to-select" posts here for seated events — holds
        // every selected seat in one request so customers can pick several
        // seats at once instead of one at a time.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSeats(int eventId, List<int> seatIds)
        {
            if (seatIds == null || !seatIds.Any())
            {
                TempData["Error"] = "No seats were selected.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            var ev = await _db.Events.FindAsync(eventId);
            if (ev == null) return NotFound();

            var cart = GetCart();
            var held = new List<string>();
            var unavailable = new List<string>();

            foreach (var seatId in seatIds.Distinct())
            {
                var seat = await _db.Seats.Include(s => s.TicketType).FirstOrDefaultAsync(s => s.SeatId == seatId);
                if (seat == null || seat.TicketType == null || seat.Status != SeatStatus.Available)
                {
                    unavailable.Add(seat?.SeatCode ?? $"#{seatId}");
                    continue;
                }

                seat.Status = SeatStatus.Held;
                seat.HeldUntilUtc = DateTime.UtcNow.AddMinutes(10);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    unavailable.Add(seat.SeatCode);
                    continue;
                }

                cart.Add(new CartLineItem
                {
                    EventId = eventId,
                    EventTitle = ev.Title,
                    TicketTypeId = seat.TicketType.TicketTypeId,
                    TicketTypeName = seat.TicketType.Name,
                    UnitPrice = seat.TicketType.Price,
                    Quantity = 1,
                    SeatId = seat.SeatId,
                    SeatCode = seat.SeatCode
                });
                held.Add(seat.SeatCode);
            }

            SaveCart(cart);

            if (held.Any())
                TempData["Message"] = $"Seat(s) {string.Join(", ", held)} held in your cart for 10 minutes.";
            if (unavailable.Any())
                TempData["Error"] = $"Seat(s) {string.Join(", ", unavailable)} were just taken and could not be added. Please pick different seats.";

            return RedirectToAction(nameof(Index));
        }

        // Kept for any direct single-seat callers; AddSeats (plural) above is
        // what the seat map now uses for multi-select.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSeat(int eventId, int seatId)
        {
            var seat = await _db.Seats.Include(s => s.TicketType)
                .FirstOrDefaultAsync(s => s.SeatId == seatId);

            if (seat == null || seat.TicketType == null)
            {
                TempData["Error"] = "That seat is not available for booking.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            // Concurrency-safe hold: only proceed if seat is still Available.
            // Mirrors the row-locking pattern used against double-booking in
            // the AMIT3253 PHP system (SELECT ... FOR UPDATE), implemented
            // here via EF Core's optimistic concurrency RowVersion token.
            if (seat.Status != SeatStatus.Available)
            {
                TempData["Error"] = $"Seat {seat.SeatCode} was just taken by someone else. Please pick another seat.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            seat.Status = SeatStatus.Held;
            seat.HeldUntilUtc = DateTime.UtcNow.AddMinutes(10);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = $"Seat {seat.SeatCode} was just taken by someone else. Please pick another seat.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            var ev = await _db.Events.FindAsync(eventId);
            var cart = GetCart();
            cart.Add(new CartLineItem
            {
                EventId = eventId,
                EventTitle = ev!.Title,
                TicketTypeId = seat.TicketType.TicketTypeId,
                TicketTypeName = seat.TicketType.Name,
                UnitPrice = seat.TicketType.Price,
                Quantity = 1,
                SeatId = seat.SeatId,
                SeatCode = seat.SeatCode
            });
            SaveCart(cart);

            TempData["Message"] = $"Seat {seat.SeatCode} held in your cart for 10 minutes.";
            return RedirectToAction(nameof(Index));
        }

        // GA / quantity-based ticket add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuantity(int eventId, int ticketTypeId, int quantity)
        {
            quantity = Math.Clamp(quantity, 1, 10);

            var tt = await _db.TicketTypes.Include(t => t.Event).FirstOrDefaultAsync(t => t.TicketTypeId == ticketTypeId);
            if (tt == null) return NotFound();

            if (tt.QuantityRemaining.HasValue && tt.QuantityRemaining < quantity)
            {
                TempData["Error"] = $"Only {tt.QuantityRemaining} ticket(s) left for {tt.Name}.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            var cart = GetCart();
            var existing = cart.FirstOrDefault(c => c.TicketTypeId == ticketTypeId && c.SeatId == null);
            if (existing != null)
                existing.Quantity += quantity;
            else
                cart.Add(new CartLineItem
                {
                    EventId = eventId,
                    EventTitle = tt.Event!.Title,
                    TicketTypeId = ticketTypeId,
                    TicketTypeName = tt.Name,
                    UnitPrice = tt.Price,
                    Quantity = quantity
                });

            SaveCart(cart);
            TempData["Message"] = $"Added {quantity} x {tt.Name} to your cart.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int ticketTypeId, int? seatId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.TicketTypeId == ticketTypeId && c.SeatId == seatId);
            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);

                if (seatId.HasValue)
                {
                    var seat = await _db.Seats.FindAsync(seatId.Value);
                    if (seat != null && seat.Status == SeatStatus.Held)
                    {
                        seat.Status = SeatStatus.Available;
                        seat.HeldUntilUtc = null;
                        await _db.SaveChangesAsync();
                    }
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
