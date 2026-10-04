using System.ComponentModel.DataAnnotations;

namespace EventXpress.Models
{
    // Category Maintenance module (Fang Quan Shen)
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required, StringLength(60)]
        public string Name { get; set; } = string.Empty; // e.g. Concert, Conference, Sports

        [StringLength(250)]
        public string? Description { get; set; }

        // Multi-language label support (Additional Feature: multi-language UI labels)
        [StringLength(60)]
        public string? NameZh { get; set; }

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
