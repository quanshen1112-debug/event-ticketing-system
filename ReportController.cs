using System.Security.Claims;
using EventXpress.Data;
using EventXpress.Models;
using EventXpress.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Reporting module (Chua Chi Yang) - organizer/admin dashboard
    [Authorize(Roles = "Organizer,Admin")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ReportController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Dashboard(int? eventId)
        {
            bool isAdmin = User.IsInRole("Admin");
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var eventsQuery = _db.Events.Include(e => e.Category).AsQueryable();
            if (!isAdmin) eventsQuery = eventsQuery.Where(e => e.OrganizerId == userId);

            var events = await eventsQuery.ToListAsync();
            var eventIds = events.Select(e => e.EventId).ToList();

            var orderItemsQuery = _db.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.TicketType).ThenInclude(t => t!.Event).ThenInclude(e => e!.Category)
                .Where(oi => eventIds.Contains(oi.TicketType!.EventId) && oi.Order!.Status == OrderStatus.Paid);

            if (eventId.HasValue)
                orderItemsQuery = orderItemsQuery.Where(oi => oi.TicketType!.EventId == eventId);

            var orderItems = await orderItemsQuery.ToListAsync();

            var vm = new SalesReportViewModel
            {
                EventId = eventId,
                EventOptions = events.OrderByDescending(e => e.StartDateTime).ToList(),
                TotalRevenue = orderItems.Sum(oi => oi.Subtotal),
                TotalTicketsSold = orderItems.Sum(oi => oi.Quantity),
                TotalOrders = orderItems.Select(oi => oi.OrderId).Distinct().Count()
            };

            // Revenue by event (column chart)
            var revenueByEvent = orderItems
                .GroupBy(oi => oi.TicketType!.Event!.Title)
                .Select(g => new { Title = g.Key, Revenue = g.Sum(oi => oi.Subtotal) })
                .OrderByDescending(g => g.Revenue)
                .Take(10)
                .ToList();
            vm.ChartLabels = revenueByEvent.Select(r => r.Title).ToList();
            vm.RevenueByEvent = revenueByEvent.Select(r => r.Revenue).ToList();

            // Tickets sold by category (pie chart)
            var byCategory = orderItems
                .GroupBy(oi => oi.TicketType!.Event!.Category!.Name)
                .Select(g => new { Category = g.Key, Count = g.Sum(oi => oi.Quantity) })
                .ToList();
            vm.CategoryLabels = byCategory.Select(c => c.Category).ToList();
            vm.TicketsByCategory = byCategory.Select(c => c.Count).ToList();

            return View(vm);
        }
    }
}
