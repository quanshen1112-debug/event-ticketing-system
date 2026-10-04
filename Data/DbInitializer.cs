using EventXpress.Models;
using EventXpress.Services;

namespace EventXpress.Data
{
    public static class DbInitializer
    {
        public static void Seed(ApplicationDbContext db)
        {
            db.Database.EnsureCreated();

            if (!db.Users.Any())
            {
                var (hash, salt) = PasswordHasher.HashPassword("Admin@12345");
                db.Users.Add(new User
                {
                    FullName = "System Admin",
                    Email = "admin@eventxpress.local",
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Role = UserRole.Admin,
                    Status = UserStatus.Active,
                    IsEmailVerified = true
                });

                var (orgHash, orgSalt) = PasswordHasher.HashPassword("Organizer@123");
                var organizer = new User
                {
                    FullName = "Demo Organizer",
                    Email = "organizer@eventxpress.local",
                    PasswordHash = orgHash,
                    PasswordSalt = orgSalt,
                    Role = UserRole.Organizer,
                    Status = UserStatus.Active,
                    IsEmailVerified = true
                };
                db.Users.Add(organizer);

                var (custHash, custSalt) = PasswordHasher.HashPassword("Customer@123");
                db.Users.Add(new User
                {
                    FullName = "Demo Customer",
                    Email = "customer@eventxpress.local",
                    PasswordHash = custHash,
                    PasswordSalt = custSalt,
                    Role = UserRole.Customer,
                    Status = UserStatus.Active,
                    IsEmailVerified = true
                });

                db.SaveChanges();
            }

            if (!db.Categories.Any())
            {
                db.Categories.AddRange(
                    new Category { Name = "Concert", NameZh = "演唱会" },
                    new Category { Name = "Conference", NameZh = "会议" },
                    new Category { Name = "Sports", NameZh = "体育赛事" },
                    new Category { Name = "Workshop", NameZh = "工作坊" }
                );
                db.SaveChanges();
            }

            if (!db.Venues.Any())
            {
                db.Venues.AddRange(
                    new Venue { Name = "Axiata Arena", Address = "Bukit Jalil, Kuala Lumpur", Capacity = 16000, IsSeated = true, Rows = 8, SeatsPerRow = 10, Latitude = 3.0546, Longitude = 101.6924 },
                    new Venue { Name = "KLCC Convention Centre Hall 3", Address = "Kuala Lumpur City Centre", Capacity = 3000, IsSeated = false, Latitude = 3.1579, Longitude = 101.7123 },
                    new Venue { Name = "TAR UMT Auditorium", Address = "Batu Pahat, Johor", Capacity = 500, IsSeated = true, Rows = 5, SeatsPerRow = 10, Latitude = 1.8548, Longitude = 102.9325 }
                );
                db.SaveChanges();
            }
        }
    }
}
