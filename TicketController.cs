using System.Security.Claims;
using EventXpress.Data;
using EventXpress.Models;
using EventXpress.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Ticket & Seat Maintenance (Wong Pang Yang)
    [Authorize(Roles = "Organizer")]
    public class TicketController : Controller
    {
        private readonly ApplicationDbContext _db;
        public TicketController(ApplicationDbContext db) => _db = db;

        private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Index(int eventId)
        {
            var ev = await _db.Events.Include(e => e.TicketTypes).ThenInclude(t => t.Seats).Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.EventId == eventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            ViewBag.Event = ev;
            return View(ev.TicketTypes.ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Create(int eventId)
        {
            var ev = await _db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.EventId == eventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            ViewBag.IsSeated = ev.Venue!.IsSeated;
            ViewBag.VenueCapacity = ev.Venue.Capacity;
            ViewBag.SeatsAllocated = await _db.Seats.CountAsync(s => s.EventId == eventId);
            return View(new TicketTypeFormViewModel { EventId = eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TicketTypeFormViewModel vm)
        {
            var ev = await _db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.EventId == vm.EventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            if (!ev.Venue!.IsSeated && vm.QuantityAvailable == null)
                ModelState.AddModelError(nameof(vm.QuantityAvailable), "Quantity is required for standing/GA venues.");

            int existingSeatCount = 0;
            int rows = 0, cols = 0, startRowIndex = 0;
            if (ev.Venue.IsSeated)
            {
                if (vm.SeatQuantity == null || vm.SeatQuantity <= 0)
                {
                    ModelState.AddModelError(nameof(vm.SeatQuantity), "Enter how many seats this ticket type should have.");
                }
                else
                {
                    existingSeatCount = await _db.Seats.CountAsync(s => s.EventId == ev.EventId);
                    if (existingSeatCount + vm.SeatQuantity > ev.Venue.Capacity)
                    {
                        var remaining = Math.Max(ev.Venue.Capacity - existingSeatCount, 0);
                        ModelState.AddModelError(nameof(vm.SeatQuantity),
                            $"This venue's capacity is {ev.Venue.Capacity}. Only {remaining} seat(s) are left to allocate across this event's ticket types.");
                    }
                    else
                    {
                        rows = (int)Math.Ceiling(Math.Sqrt(vm.SeatQuantity.Value));
                        cols = (int)Math.Ceiling((double)vm.SeatQuantity.Value / rows);
                        startRowIndex = existingSeatCount == 0
                            ? 0
                            : (await _db.Seats.Where(s => s.EventId == ev.EventId).Select(s => s.RowLabel).ToListAsync())
                                .Select(r => r[0] - 'A').Max() + 1;

                        if (startRowIndex + rows > 26)
                        {
                            ModelState.AddModelError(nameof(vm.SeatQuantity),
                                "This event has run out of row letters (A-Z) to allocate across its ticket types. Reduce the seat quantity.");
                        }
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.IsSeated = ev.Venue.IsSeated;
                ViewBag.VenueCapacity = ev.Venue.Capacity;
                ViewBag.SeatsAllocated = existingSeatCount;
                return View(vm);
            }

            var ticketType = new TicketType
            {
                EventId = vm.EventId,
                Name = vm.Name,
                Price = vm.Price,
                QuantityAvailable = ev.Venue.IsSeated ? null : vm.QuantityAvailable
            };
            _db.TicketTypes.Add(ticketType);
            await _db.SaveChangesAsync();

            if (ev.Venue.IsSeated)
            {
                // Square-ish grid (rows = ceil(sqrt(quantity))) so the seat map
                // reads as a block. Row lettering continues on from whatever
                // this event already has, so multiple tiers stack without
                // colliding on the same row/seat number.
                int remaining = vm.SeatQuantity!.Value;
                for (int r = 0; r < rows; r++)
                {
                    string rowLabel = ((char)('A' + startRowIndex + r)).ToString();
                    int seatsThisRow = Math.Min(cols, remaining);
                    for (int s = 1; s <= seatsThisRow; s++)
                    {
                        _db.Seats.Add(new Seat
                        {
                            VenueId = ev.VenueId,
                            EventId = ev.EventId,
                            TicketTypeId = ticketType.TicketTypeId,
                            RowLabel = rowLabel,
                            SeatNumber = s,
                            Status = SeatStatus.Available
                        });
                    }
                    remaining -= seatsThisRow;
                }
                await _db.SaveChangesAsync();
                TempData["Message"] = $"Ticket type added and {vm.SeatQuantity} seat(s) generated ({rows}x{cols} grid).";
                return RedirectToAction(nameof(ViewSeats), new { eventId = vm.EventId });
            }

            TempData["Message"] = "Ticket type added.";
            return RedirectToAction(nameof(Index), new { eventId = vm.EventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int eventId)
        {
            var ev = await _db.Events.FindAsync(eventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            var tt = await _db.TicketTypes.FindAsync(id);
            if (tt == null) return NotFound();

            bool hasSales = await _db.OrderItems.AnyAsync(oi => oi.TicketTypeId == id);
            if (hasSales)
            {
                TempData["Error"] = "Cannot delete a ticket type that already has sales.";
                return RedirectToAction(nameof(Index), new { eventId });
            }

            // Seats are generated together with the ticket type, so they need
            // to be freed before the ticket type itself can be removed —
            // otherwise the delete fails with a foreign-key error. Block it
            // if any of those seats are currently held in someone's cart.
            var seats = await _db.Seats.Where(s => s.TicketTypeId == id).ToListAsync();
            if (seats.Any(s => s.Status == SeatStatus.Held))
            {
                TempData["Error"] = "Cannot delete: some of this ticket type's seats are currently held in a customer's cart. Try again in a few minutes.";
                return RedirectToAction(nameof(Index), new { eventId });
            }

            _db.Seats.RemoveRange(seats);
            _db.TicketTypes.Remove(tt);
            await _db.SaveChangesAsync();
            TempData["Message"] = seats.Count > 0
                ? $"Ticket type deleted, along with its {seats.Count} generated seat(s)."
                : "Ticket type deleted.";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        // Interactive seat map: organizer assigns a price tier (TicketType) to a
        // range/selection of physical seats for this event's venue.
        [HttpGet]
        public async Task<IActionResult> AssignSeats(int eventId, int ticketTypeId)
        {
            var ev = await _db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.EventId == eventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            var seats = await _db.Seats.Include(s => s.TicketType).Where(s => s.EventId == ev.EventId)
                .OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber).ToListAsync();

            var vm = new SeatMapViewModel
            {
                EventId = eventId,
                EventTitle = ev.Title,
                IsSeated = ev.Venue!.IsSeated,
                Rows = ev.Venue.Rows,
                SeatsPerRow = ev.Venue.SeatsPerRow,
                Seats = seats.Select(s => new SeatCellViewModel
                {
                    SeatId = s.SeatId,
                    RowLabel = s.RowLabel,
                    SeatNumber = s.SeatNumber,
                    Status = s.IsHouseSeat ? "House" : (s.TicketTypeId == ticketTypeId ? "Assigned" : (s.TicketTypeId != null ? "Taken" : "Available")),
                    TicketTypeName = s.TicketType?.Name
                }).ToList()
            };
            ViewBag.TicketTypeId = ticketTypeId;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignSeats(int eventId, int ticketTypeId, List<int> seatIds)
        {
            var ev = await _db.Events.FindAsync(eventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            var seats = await _db.Seats.Where(s => seatIds.Contains(s.SeatId) && s.TicketTypeId == null && !s.IsHouseSeat).ToListAsync();
            foreach (var seat in seats)
                seat.TicketTypeId = ticketTypeId;

            await _db.SaveChangesAsync();
            TempData["Message"] = $"{seats.Count} seat(s) assigned to this ticket type.";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        // Read-only overview of the entire venue seat map for this event, across
        // all ticket types — lets the organizer see the full available/sold/
        // held/house picture instead of one tier at a time (that's AssignSeats).
        [HttpGet]
        public async Task<IActionResult> ViewSeats(int eventId)
        {
            var ev = await _db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.EventId == eventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();
            if (!ev.Venue!.IsSeated) return NotFound();

            var seats = await _db.Seats
                .Include(s => s.TicketType)
                .Where(s => s.EventId == eventId)
                .OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber)
                .ToListAsync();

            var vm = new SeatMapViewModel
            {
                EventId = eventId,
                EventTitle = ev.Title,
                IsSeated = true,
                Rows = ev.Venue.Rows,
                SeatsPerRow = ev.Venue.SeatsPerRow,
                Seats = seats.Select(s => new SeatCellViewModel
                {
                    SeatId = s.SeatId,
                    RowLabel = s.RowLabel,
                    SeatNumber = s.SeatNumber,
                    Status = s.IsHouseSeat ? "House" : (s.TicketTypeId == null ? "Unpriced" : s.Status.ToString()),
                    TicketTypeName = s.IsHouseSeat ? "House Seat" : (s.TicketType?.Name ?? "Not yet priced"),
                    Price = s.TicketType?.Price ?? 0
                }).ToList()
            };
            return View(vm);
        }

        // Additional Feature: organizers can hold back seats as "house seats"
        // (comps, press, production) even after they've already been
        // generated/priced into a ticket type — marking a seat as house frees
        // it from that tier's paid inventory.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkHouseSeats(int eventId, int ticketTypeId, List<int> seatIds)
        {
            var ev = await _db.Events.FindAsync(eventId);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            var seats = await _db.Seats
                .Where(s => seatIds.Contains(s.SeatId) && s.EventId == eventId && s.Status == SeatStatus.Available && !s.IsHouseSeat)
                .ToListAsync();
            foreach (var seat in seats)
            {
                seat.IsHouseSeat = true;
                seat.TicketTypeId = null;
            }

            await _db.SaveChangesAsync();
            TempData["Message"] = $"{seats.Count} seat(s) reserved as house seats (not for sale).";
            return RedirectToAction(nameof(AssignSeats), new { eventId, ticketTypeId });
        }
    }
}
