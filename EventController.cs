using System.Security.Claims;
using EventXpress.Data;
using EventXpress.Models;
using EventXpress.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Event Maintenance + Event Catalog (Fang Quan Shen)
    public class EventController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public EventController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // ---------------- PUBLIC CATALOG ----------------

        [AllowAnonymous]
        public async Task<IActionResult> Index(EventCatalogViewModel filter)
        {
            filter.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();

            var query = BuildCatalogQuery(filter);

            filter.TotalCount = await query.CountAsync();
            filter.Events = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return View(filter);
        }

        // Additional Feature: AJAX-based search/filter/sort/paging.
        // Returns just the partial grid so the page doesn't fully reload.
        [AllowAnonymous]
        public async Task<IActionResult> Search(EventCatalogViewModel filter)
        {
            var query = BuildCatalogQuery(filter);

            filter.TotalCount = await query.CountAsync();
            filter.Events = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return PartialView("_EventGridPartial", filter);
        }

        private IQueryable<Event> BuildCatalogQuery(EventCatalogViewModel filter)
        {
            var query = _db.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketTypes)
                .Include(e => e.Reviews)
                .Where(e => e.Status == EventStatus.Published)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
                query = query.Where(e => e.Title.Contains(filter.Keyword) || e.Description.Contains(filter.Keyword));

            if (filter.CategoryId.HasValue)
                query = query.Where(e => e.CategoryId == filter.CategoryId);

            if (filter.DateFrom.HasValue)
                query = query.Where(e => e.StartDateTime >= filter.DateFrom);

            if (filter.DateTo.HasValue)
                query = query.Where(e => e.StartDateTime <= filter.DateTo);

            if (!string.IsNullOrWhiteSpace(filter.Location))
                query = query.Where(e => e.Venue!.Address.Contains(filter.Location) || e.Venue!.Name.Contains(filter.Location));

            query = filter.SortBy switch
            {
                "date_desc" => query.OrderByDescending(e => e.StartDateTime),
                "price_asc" => query.OrderBy(e => e.TicketTypes.Min(t => (decimal?)t.Price) ?? 0),
                "price_desc" => query.OrderByDescending(e => e.TicketTypes.Min(t => (decimal?)t.Price) ?? 0),
                "popularity" => query.OrderByDescending(e => e.Reviews.Count),
                _ => query.OrderBy(e => e.StartDateTime),
            };

            return query;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var ev = await _db.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.TicketTypes)
                .Include(e => e.Reviews).ThenInclude(r => r.Customer)
                .FirstOrDefaultAsync(e => e.EventId == id && e.Status == EventStatus.Published);

            if (ev == null) return NotFound();
            return View(ev);
        }

        // ---------------- ORGANIZER: MY EVENTS ----------------

        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> MyEvents()
        {
            int organizerId = CurrentUserId();
            var events = await _db.Events
                .Include(e => e.Category).Include(e => e.Venue)
                .Where(e => e.OrganizerId == organizerId)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();
            return View(events);
        }

        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> Create()
        {
            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            var venues = await _db.Venues.OrderBy(v => v.Name).ToListAsync();

            if (!categories.Any() || !venues.Any())
            {
                TempData["Error"] = "An Admin needs to set up at least one Category and one Venue before you can create an event.";
                return RedirectToAction(nameof(MyEvents));
            }

            var vm = new EventFormViewModel
            {
                CategoryOptions = categories,
                VenueOptions = venues
            };
            return View(vm);
        }

        [Authorize(Roles = "Organizer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EventFormViewModel vm)
        {
            if (vm.EndDateTime <= vm.StartDateTime)
                ModelState.AddModelError(nameof(vm.EndDateTime), "End time must be after the start time.");

            if (!await _db.Categories.AnyAsync(c => c.CategoryId == vm.CategoryId))
                ModelState.AddModelError(nameof(vm.CategoryId), "Please choose a valid category.");

            if (!await _db.Venues.AnyAsync(v => v.VenueId == vm.VenueId))
                ModelState.AddModelError(nameof(vm.VenueId), "Please choose a valid venue.");

            if (!ModelState.IsValid)
            {
                vm.CategoryOptions = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
                vm.VenueOptions = await _db.Venues.OrderBy(v => v.Name).ToListAsync();
                return View(vm);
            }

            var ev = new Event
            {
                Title = vm.Title,
                Description = vm.Description,
                StartDateTime = vm.StartDateTime,
                EndDateTime = vm.EndDateTime,
                CategoryId = vm.CategoryId,
                VenueId = vm.VenueId,
                Status = vm.Status,
                OrganizerId = CurrentUserId()
            };

            if (vm.BannerImage != null)
                ev.BannerImagePath = await SaveBannerImageAsync(vm.BannerImage);

            _db.Events.Add(ev);
            await _db.SaveChangesAsync();

            TempData["Message"] = "Event created. Add ticket types next.";
            return RedirectToAction("Index", "Ticket", new { eventId = ev.EventId });
        }

        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> Edit(int id)
        {
            var ev = await _db.Events.FindAsync(id);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            var vm = new EventFormViewModel
            {
                EventId = ev.EventId,
                Title = ev.Title,
                Description = ev.Description,
                StartDateTime = ev.StartDateTime,
                EndDateTime = ev.EndDateTime,
                ExistingBannerPath = ev.BannerImagePath,
                CategoryId = ev.CategoryId,
                VenueId = ev.VenueId,
                Status = ev.Status,
                CategoryOptions = await _db.Categories.OrderBy(c => c.Name).ToListAsync(),
                VenueOptions = await _db.Venues.OrderBy(v => v.Name).ToListAsync()
            };
            return View(vm);
        }

        [Authorize(Roles = "Organizer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EventFormViewModel vm)
        {
            var ev = await _db.Events.FindAsync(id);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            if (vm.EndDateTime <= vm.StartDateTime)
                ModelState.AddModelError(nameof(vm.EndDateTime), "End time must be after the start time.");

            if (!ModelState.IsValid)
            {
                vm.CategoryOptions = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
                vm.VenueOptions = await _db.Venues.OrderBy(v => v.Name).ToListAsync();
                return View(vm);
            }

            ev.Title = vm.Title;
            ev.Description = vm.Description;
            ev.StartDateTime = vm.StartDateTime;
            ev.EndDateTime = vm.EndDateTime;
            ev.CategoryId = vm.CategoryId;
            ev.VenueId = vm.VenueId;
            ev.Status = vm.Status;

            if (vm.BannerImage != null)
                ev.BannerImagePath = await SaveBannerImageAsync(vm.BannerImage);

            await _db.SaveChangesAsync();
            TempData["Message"] = "Event updated.";
            return RedirectToAction(nameof(MyEvents));
        }

        [Authorize(Roles = "Organizer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ev = await _db.Events.FindAsync(id);
            if (ev == null || ev.OrganizerId != CurrentUserId()) return NotFound();

            bool hasOrders = await _db.OrderItems.AnyAsync(oi => oi.TicketType!.EventId == id);
            if (hasOrders)
            {
                ev.Status = EventStatus.Cancelled; // soft-cancel instead of deleting, orders exist
                TempData["Message"] = "Event has existing bookings, so it was marked Cancelled instead of deleted.";
            }
            else
            {
                _db.Events.Remove(ev);
                TempData["Message"] = "Event deleted.";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(MyEvents));
        }

        // Additional Feature: banner image upload (crop/resize handled client-side via Cropper.js,
        // server receives the already-cropped image blob and just persists it).
        private async Task<string> SaveBannerImageAsync(IFormFile file)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var path = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/{fileName}";
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int eventId, int rating, string? comment)
        {
            var alreadyReviewed = await _db.Reviews.AnyAsync(r => r.EventId == eventId && r.CustomerId == CurrentUserId());
            if (!alreadyReviewed)
            {
                _db.Reviews.Add(new Review
                {
                    EventId = eventId,
                    CustomerId = CurrentUserId(),
                    Rating = Math.Clamp(rating, 1, 5),
                    Comment = comment
                });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        [AllowAnonymous]
        public async Task<IActionResult> SeatMap(int id)
        {
            var ev = await _db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.EventId == id && e.Status == EventStatus.Published);
            if (ev == null || !ev.Venue!.IsSeated) return NotFound();

            // Release any expired holds before rendering (10-minute cart hold expiry)
            var expiredHolds = await _db.Seats
                .Where(s => s.EventId == ev.EventId && s.Status == SeatStatus.Held && s.HeldUntilUtc < DateTime.UtcNow)
                .ToListAsync();
            foreach (var s in expiredHolds) { s.Status = SeatStatus.Available; s.HeldUntilUtc = null; }
            if (expiredHolds.Any()) await _db.SaveChangesAsync();

            // Include seats already priced for this event AND house seats (which
            // have no TicketType) so the map still shows a complete venue layout.
            var seats = await _db.Seats
                .Include(s => s.TicketType)
                .Where(s => s.EventId == ev.EventId && (s.TicketTypeId != null || s.IsHouseSeat))
                .OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber)
                .ToListAsync();

            var vm = new SeatMapViewModel
            {
                EventId = id,
                EventTitle = ev.Title,
                IsSeated = true,
                Rows = ev.Venue.Rows,
                SeatsPerRow = ev.Venue.SeatsPerRow,
                Seats = seats.Select(s => new SeatCellViewModel
                {
                    SeatId = s.SeatId,
                    RowLabel = s.RowLabel,
                    SeatNumber = s.SeatNumber,
                    Status = s.IsHouseSeat ? "House" : s.Status.ToString(),
                    TicketTypeName = s.IsHouseSeat ? "House Seat (not for sale)" : s.TicketType!.Name,
                    Price = s.TicketType?.Price ?? 0
                }).ToList()
            };
            return View(vm);
        }

        private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
