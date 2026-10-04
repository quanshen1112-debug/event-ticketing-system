namespace EventXpress.Services
{
    public interface ICaptchaService
    {
        (string Question, int Answer) Generate();
    }

    // Additional Feature: CAPTCHA on login/registration (anti-bot).
    // Implemented as a simple arithmetic challenge; the correct answer is
    // stored server-side in Session and compared on submit so it can't be
    // read or tampered with from the client.
    public class CaptchaService : ICaptchaService
    {
        private static readonly Random _rng = new();

        public (string Question, int Answer) Generate()
        {
            int a = _rng.Next(1, 10);
            int b = _rng.Next(1, 10);
            string op = _rng.Next(0, 2) == 0 ? "+" : "-";

            int answer = op == "+" ? a + b : a - b;
            string question = $"{a} {op} {b} = ?";
            return (question, answer);
        }
    }
}
