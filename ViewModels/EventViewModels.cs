using System.ComponentModel.DataAnnotations;

namespace EventXpress.Models.ViewModels
{
    // Powers the AJAX-based search/filter/sort/paging catalog listing (Additional Feature)
    public class EventCatalogViewModel
    {
        public string? Keyword { get; set; }
        public int? CategoryId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Location { get; set; }
        public string SortBy { get; set; } = "date_asc"; // date_asc, date_desc, price_asc, price_desc, popularity

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 9;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        public List<Category> Categories { get; set; } = new();
        public List<Event> Events { get; set; } = new();
    }

    public class EventFormViewModel
    {
        public int EventId { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartDateTime { get; set; } = DateTime.Now.AddDays(7);

        [Required]
        public DateTime EndDateTime { get; set; } = DateTime.Now.AddDays(7).AddHours(3);

        public IFormFile? BannerImage { get; set; }
        public string? ExistingBannerPath { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int VenueId { get; set; }

        public EventStatus Status { get; set; } = EventStatus.Draft;

        public List<Category> CategoryOptions { get; set; } = new();
        public List<Venue> VenueOptions { get; set; } = new();
    }
}
