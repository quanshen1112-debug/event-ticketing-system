using EventXpress.Data;
using EventXpress.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Venue Maintenance (Event & Catalog Management module - Fang Quan Shen)
    [Authorize(Roles = "Admin")]
    public class VenueController : Controller
    {
        private readonly ApplicationDbContext _db;
        public VenueController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index() => View(await _db.Venues.OrderBy(v => v.Name).ToListAsync());

        [HttpGet]
        public IActionResult Create() => View(new Venue());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Venue model)
        {
            if (!ModelState.IsValid) return View(model);

            _db.Venues.Add(model);
            await _db.SaveChangesAsync();

            TempData["Message"] = "Venue created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var venue = await _db.Venues.FindAsync(id);
            if (venue == null) return NotFound();
            return View(venue);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Venue model)
        {
            if (id != model.VenueId) return NotFound();
            if (!ModelState.IsValid) return View(model);

            _db.Venues.Update(model);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Venue updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var venue = await _db.Venues.FindAsync(id);
            if (venue == null) return NotFound();

            bool inUse = await _db.Events.AnyAsync(e => e.VenueId == id);
            if (inUse)
            {
                TempData["Error"] = "Cannot delete a venue that has events assigned to it.";
                return RedirectToAction(nameof(Index));
            }

            _db.Venues.Remove(venue);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Venue deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
