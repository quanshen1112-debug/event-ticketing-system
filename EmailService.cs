using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventXpress.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string verificationLink);
        Task SendOrderConfirmationAsync(string toEmail, string orderNumber, string viewLink);
        Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    }

    // Sends real emails via the Resend HTTP API (https://resend.com).
    // Uses HTTPS on port 443, which avoids the SMTP port blocking that
    // many networks / hosts / school Wi-Fi impose on ports 25/587/465.
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public EmailService(ILogger<EmailService> logger, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            var subject = "Verify your EventXpress account";
            var body = $@"
                <p>Hi,</p>
                <p>Thanks for registering with EventXpress. Please verify your email address by clicking the link below:</p>
                <p><a href=""{verificationLink}"">Verify my email</a></p>
                <p>If the link doesn't work, copy and paste this URL into your browser:</p>
                <p>{verificationLink}</p>
                <p>If you didn't create this account, you can safely ignore this email.</p>";

            return SendEmailAsync(toEmail, subject, body);
        }

        public Task SendOrderConfirmationAsync(string toEmail, string orderNumber, string viewLink)
        {
            var subject = $"Order {orderNumber} confirmed";
            var body = $@"
                <p>Hi,</p>
                <p>Your order <strong>{orderNumber}</strong> has been confirmed.</p>
                <p><a href=""{viewLink}"">View your order</a></p>";

            return SendEmailAsync(toEmail, subject, body);
        }

        // Additional Feature: password recovery — same email flow used by
        // Customer, Organizer, and Admin accounts alike.
        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            var subject = "Reset your EventXpress password";
            var body = $@"
                <p>Hi,</p>
                <p>We received a request to reset your EventXpress password. Click the link below to choose a new one:</p>
                <p><a href=""{resetLink}"">Reset my password</a></p>
                <p>If the link doesn't work, copy and paste this URL into your browser:</p>
                <p>{resetLink}</p>
                <p>This link expires in 30 minutes. If you didn't request a password reset, you can safely ignore this email — your password will not be changed.</p>";

            return SendEmailAsync(toEmail, subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var settings = _config.GetSection("EmailSettings");
            var apiKey = settings["ResendApiKey"];
            var senderName = settings["SenderName"] ?? "EventXpress";
            var senderEmail = settings["SenderEmail"];

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
            {
                // Config not set up yet — fall back to logging so registration
                // still works in dev instead of throwing an exception.
                _logger.LogWarning(
                    "EmailSettings not configured. [WOULD SEND] To: {Email} | Subject: {Subject}",
                    toEmail, subject);
                return;
            }

            var payload = new
            {
                from = $"{senderName} <{senderEmail}>",
                to = new[] { toEmail },
                subject = subject,
                html = htmlBody
            };

            var json = JsonSerializer.Serialize(payload);

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("emails", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Resend API returned {StatusCode} sending to {Email}: {Body}",
                        response.StatusCode, toEmail, responseBody);
                    response.EnsureSuccessStatusCode();
                }

                _logger.LogInformation("Email sent to {Email} | Subject: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }
    }
}
