using System.ComponentModel.DataAnnotations;

namespace EventXpress.Models.ViewModels
{
    // Admin & User Maintenance module — lets Admin modify an Organizer/Customer's
    // basic profile info. Role and password are intentionally not editable here:
    // role changes and password resets are handled through their own dedicated
    // flows, not lumped into this form.
    public class AdminEditUserViewModel
    {
        public int UserId { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, StringLength(150), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(30)]
        public string? Phone { get; set; }

        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
    }
}
