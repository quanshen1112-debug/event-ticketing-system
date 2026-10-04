using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventXpress.Models
{
    public enum UserRole
    {
        Admin,
        Organizer,
        Customer
    }

    public enum UserStatus
    {
        PendingVerification, // registered but hasn't clicked email verification link
        Active,
        Suspended,           // admin-suspended organizer/customer account
        PendingApproval      // organizer awaiting admin approval
    }

    // Single table (with discriminator Role) rather than ASP.NET Identity,
    // per team decision to implement authentication manually (Lee Yu Zhe - Security module).
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, StringLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string PasswordSalt { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        [Required]
        public UserStatus Status { get; set; } = UserStatus.PendingVerification;

        [StringLength(30)]
        public string? Phone { get; set; }

        // --- Security / brute force protection ---
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEndUtc { get; set; }

        // --- Email verification (Additional Feature) ---
        public string? EmailVerificationToken { get; set; }
        public bool IsEmailVerified { get; set; } = false;

        // --- Password recovery (Additional Feature) ---
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiresUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public bool IsLockedOut => LockoutEndUtc.HasValue && LockoutEndUtc.Value > DateTime.UtcNow;

        // Navigation
        public ICollection<Event> OrganizedEvents { get; set; } = new List<Event>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
