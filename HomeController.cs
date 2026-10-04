using EventXpress.Data;
using EventXpress.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var upcoming = await _db.Events
                .Include(e => e.Category).Include(e => e.Venue)
                .Where(e => e.Status == EventStatus.Published && e.StartDateTime >= DateTime.Now)
                .OrderBy(e => e.StartDateTime)
                .Take(6)
                .ToListAsync();
            return View(upcoming);
        }

        public IActionResult Error() => View();
    }
}
