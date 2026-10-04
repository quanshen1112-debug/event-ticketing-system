using System.Security.Claims;
using EventXpress.Data;
using EventXpress.Models;
using EventXpress.Models.ViewModels;
using EventXpress.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EventXpress.Controllers
{
    // Security module (Lee Yu Zhe): manual cookie authentication, role-based
    // authorization, CAPTCHA, login lockout, email verification.
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICaptchaService _captcha;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;

        public AccountController(ApplicationDbContext db, ICaptchaService captcha, IEmailService email, IConfiguration config)
        {
            _db = db;
            _captcha = captcha;
            _email = email;
            _config = config;
        }

        private void GenerateCaptchaIntoSession(string sessionKey)
        {
            var (question, answer) = _captcha.Generate();
            HttpContext.Session.SetString(sessionKey, answer.ToString());
            ViewBag.CaptchaQuestion = question;
        }

        // ---------------- LOGIN ----------------

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            GenerateCaptchaIntoSession("LoginCaptcha");
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Verify CAPTCHA first
            var expected = HttpContext.Session.GetString("LoginCaptcha");
            if (expected == null || model.CaptchaAnswer != expected)
            {
                ModelState.AddModelError(nameof(model.CaptchaAnswer), "Incorrect answer to the security question.");
            }

            if (!ModelState.IsValid)
            {
                GenerateCaptchaIntoSession("LoginCaptcha");
                return View(model);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                GenerateCaptchaIntoSession("LoginCaptcha");
                return View(model);
            }

            // Additional Feature: temporary login blocking after 3 failed attempts
            if (user.IsLockedOut)
            {
                var minutesLeft = Math.Ceiling((user.LockoutEndUtc!.Value - DateTime.UtcNow).TotalMinutes);
                ModelState.AddModelError(string.Empty, $"Account temporarily locked. Try again in {minutesLeft} minute(s).");
                GenerateCaptchaIntoSession("LoginCaptcha");
                return View(model);
            }

            bool validPassword = PasswordHasher.Verify(model.Password, user.PasswordHash, user.PasswordSalt);

            int maxAttempts = _config.GetValue<int>("AppSettings:MaxFailedLoginAttempts", 3);
            int lockoutMinutes = _config.GetValue<int>("AppSettings:LockoutMinutes", 5);

            if (!validPassword)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= maxAttempts)
                {
                    user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                    user.FailedLoginAttempts = 0;
                    ModelState.AddModelError(string.Empty, $"Too many failed attempts. Account locked for {lockoutMinutes} minutes.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, $"Invalid email or password. {maxAttempts - user.FailedLoginAttempts} attempt(s) remaining.");
                }
                await _db.SaveChangesAsync();
                GenerateCaptchaIntoSession("LoginCaptcha");
                return View(model);
            }

            if (!user.IsEmailVerified)
            {
                ModelState.AddModelError(string.Empty, "Please verify your email before logging in. Check your inbox for the verification link.");
                GenerateCaptchaIntoSession("LoginCaptcha");
                return View(model);
            }

            if (user.Status == UserStatus.Suspended)
            {
                ModelState.AddModelError(string.Empty, "Your account has been suspended. Please contact the platform administrator.");
                GenerateCaptchaIntoSession("LoginCaptcha");
                return View(model);
            }

            if (user.Status == UserStatus.PendingApproval)
            {
                ModelState.AddModelError(string.Empty, "Your organizer account is awaiting admin approval.");
                GenerateCaptchaIntoSession("LoginCaptcha");
                return View(model);
            }

            // Success: reset failed attempts, issue cookie
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            await _db.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return user.Role switch
            {
                UserRole.Admin => RedirectToAction("Index", "Admin"),
                UserRole.Organizer => RedirectToAction("Dashboard", "Report"),
                _ => RedirectToAction("Index", "Event")
            };
        }

        // ---------------- REGISTER ----------------

        [HttpGet]
        public IActionResult Register()
        {
            GenerateCaptchaIntoSession("RegisterCaptcha");
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            var expected = HttpContext.Session.GetString("RegisterCaptcha");
            if (expected == null || model.CaptchaAnswer != expected)
            {
                ModelState.AddModelError(nameof(model.CaptchaAnswer), "Incorrect answer to the security question.");
            }

            if (model.Role == UserRole.Admin)
            {
                ModelState.AddModelError(nameof(model.Role), "Admin accounts cannot self-register.");
            }

            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            }

            if (!ModelState.IsValid)
            {
                GenerateCaptchaIntoSession("RegisterCaptcha");
                return View(model);
            }

            var (hash, salt) = PasswordHasher.HashPassword(model.Password);
            var token = Guid.NewGuid().ToString("N");

            var user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = model.Role,
                // Organizers require admin approval on top of email verification
                Status = model.Role == UserRole.Organizer ? UserStatus.PendingApproval : UserStatus.PendingVerification,
                EmailVerificationToken = token
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var link = Url.Action("VerifyEmail", "Account", new { token }, Request.Scheme);
            await _email.SendVerificationEmailAsync(user.Email, link!);

            TempData["Message"] = "Registration successful! Please check your email (see application logs in this dev build) to verify your account before logging in.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
            if (user == null)
            {
                TempData["Error"] = "Invalid or expired verification link.";
                return RedirectToAction("Login");
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            await _db.SaveChangesAsync();

            TempData["Message"] = "Email verified! You can now log in.";
            return RedirectToAction("Login");
        }

        // ---------------- LOGOUT ----------------

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        // ---------------- PASSWORD RECOVERY ----------------
        // Additional Feature: password recovery via email, available to
        // Customer, Organizer, and Admin accounts alike (same shared login).

        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email);

            // Always show the same message whether or not the email exists —
            // otherwise this form could be used to check which emails are
            // registered on the platform.
            if (user != null)
            {
                user.PasswordResetToken = Guid.NewGuid().ToString("N");
                user.PasswordResetTokenExpiresUtc = DateTime.UtcNow.AddMinutes(30);
                await _db.SaveChangesAsync();

                var link = Url.Action("ResetPassword", "Account", new { token = user.PasswordResetToken }, Request.Scheme);
                await _email.SendPasswordResetEmailAsync(user.Email, link!);
            }

            TempData["Message"] = "If that email is registered, a password reset link has been sent. Please check your inbox (and the application logs in this dev build).";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token
                && u.PasswordResetTokenExpiresUtc > DateTime.UtcNow);
            if (user == null)
            {
                TempData["Error"] = "That password reset link is invalid or has expired. Please request a new one.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == vm.Token
                && u.PasswordResetTokenExpiresUtc > DateTime.UtcNow);
            if (user == null)
            {
                TempData["Error"] = "That password reset link is invalid or has expired. Please request a new one.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var (hash, salt) = PasswordHasher.HashPassword(vm.NewPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresUtc = null;
            // A successful reset is as good as proving the account isn't
            // being brute-forced right now — clear any active lockout too.
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            await _db.SaveChangesAsync();

            TempData["Message"] = "Your password has been reset. You can now log in.";
            return RedirectToAction(nameof(Login));
        }

        // AJAX endpoint to refresh the CAPTCHA image/question without a full postback
        [HttpGet]
        public IActionResult RefreshCaptcha(string target)
        {
            var key = target == "register" ? "RegisterCaptcha" : "LoginCaptcha";
            var (question, answer) = _captcha.Generate();
            HttpContext.Session.SetString(key, answer.ToString());
            return Json(new { question });
        }

        // ---------------- PROFILE (User profile management) ----------------

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string fullName, string phone)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.FullName = fullName;
            user.Phone = phone;
            await _db.SaveChangesAsync();

            TempData["Message"] = "Profile updated.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (!PasswordHasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt))
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction(nameof(Profile));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8 || newPassword != confirmPassword)
            {
                TempData["Error"] = "New password must be at least 8 characters and match confirmation.";
                return RedirectToAction(nameof(Profile));
            }

            var (hash, salt) = PasswordHasher.HashPassword(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _db.SaveChangesAsync();

            TempData["Message"] = "Password changed successfully.";
            return RedirectToAction(nameof(Profile));
        }
    }
}
