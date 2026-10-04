using EventXpress.Data;
using EventXpress.Models;
using EventXpress.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Admin & User Maintenance module (Lee Yu Zhe)
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _db.Users
                .Where(u => u.Role != UserRole.Admin)
                .OrderBy(u => u.Role).ThenBy(u => u.FullName)
                .ToListAsync();

            ViewBag.PendingOrganizers = users.Count(u => u.Role == UserRole.Organizer && u.Status == UserStatus.PendingApproval);
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null || user.Role != UserRole.Organizer) return NotFound();

            user.Status = UserStatus.Active;
            await _db.SaveChangesAsync();
            TempData["Message"] = $"{user.FullName}'s organizer account has been approved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suspend(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null || user.Role == UserRole.Admin) return NotFound();

            user.Status = UserStatus.Suspended;
            await _db.SaveChangesAsync();
            TempData["Message"] = $"{user.FullName}'s account has been suspended.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null || user.Role == UserRole.Admin) return NotFound();

            user.Status = UserStatus.Active;
            await _db.SaveChangesAsync();
            TempData["Message"] = $"{user.FullName}'s account has been reactivated.";
            return RedirectToAction(nameof(Index));
        }

        // Admin & User Maintenance module (CRUD): modify an organizer/customer's
        // basic profile info. Admin accounts are never listed or editable here.
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null || user.Role == UserRole.Admin) return NotFound();

            var vm = new AdminEditUserViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                Status = user.Status
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminEditUserViewModel vm)
        {
            var user = await _db.Users.FindAsync(vm.UserId);
            if (user == null || user.Role == UserRole.Admin) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Role = user.Role;
                vm.Status = user.Status;
                return View(vm);
            }

            bool emailTaken = await _db.Users.AnyAsync(u => u.UserId != vm.UserId && u.Email == vm.Email);
            if (emailTaken)
            {
                ModelState.AddModelError(nameof(vm.Email), "That email is already used by another account.");
                vm.Role = user.Role;
                vm.Status = user.Status;
                return View(vm);
            }

            user.FullName = vm.FullName;
            user.Email = vm.Email;
            user.Phone = vm.Phone;
            await _db.SaveChangesAsync();

            TempData["Message"] = $"{user.FullName}'s account details have been updated.";
            return RedirectToAction(nameof(Index));
        }

        // Admin & User Maintenance module (CRUD): permanently remove an
        // organizer/customer account. Blocked when the account has dependent
        // records (events run, orders placed) so history/reporting stays
        // intact — Suspend is the right tool for those accounts instead.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null || user.Role == UserRole.Admin) return NotFound();

            if (user.Role == UserRole.Organizer && await _db.Events.AnyAsync(e => e.OrganizerId == id))
            {
                TempData["Error"] = $"Cannot delete {user.FullName}: this organizer has events on the platform. Suspend the account instead.";
                return RedirectToAction(nameof(Index));
            }

            if (user.Role == UserRole.Customer && await _db.Orders.AnyAsync(o => o.CustomerId == id))
            {
                TempData["Error"] = $"Cannot delete {user.FullName}: this customer has order history on the platform. Suspend the account instead.";
                return RedirectToAction(nameof(Index));
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            TempData["Message"] = $"{user.FullName}'s account has been deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Platform-wide category & venue maintenance are handled by
        // CategoryController / VenueController, which Admin also has access to.
    }
}
