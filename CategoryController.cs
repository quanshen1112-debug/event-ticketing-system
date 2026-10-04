using EventXpress.Data;
using EventXpress.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventXpress.Controllers
{
    // Category Maintenance (Event & Catalog Management module - Fang Quan Shen)
    [Authorize(Roles = "Admin")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CategoryController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index() => View(await _db.Categories.OrderBy(c => c.Name).ToListAsync());

        [HttpGet]
        public IActionResult Create() => View(new Category());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Categories.Add(model);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Category created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category model)
        {
            if (id != model.CategoryId) return NotFound();
            if (!ModelState.IsValid) return View(model);

            _db.Categories.Update(model);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Category updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null) return NotFound();

            bool inUse = await _db.Events.AnyAsync(e => e.CategoryId == id);
            if (inUse)
            {
                TempData["Error"] = "Cannot delete a category that has events assigned to it.";
                return RedirectToAction(nameof(Index));
            }

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Category deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
