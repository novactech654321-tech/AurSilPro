using System.Net;
using System.Net.Mail;

namespace AurSil.Api.Services;

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "AurSil";
}

public class EmailSender(SmtpOptions options, IHostEnvironment env, ILogger<EmailSender> logger)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Host) &&
        !string.IsNullOrWhiteSpace(options.FromEmail) &&
        !string.IsNullOrWhiteSpace(options.Password);

    public async Task SendOtpAsync(string toEmail, string name, string code, string purpose, int expiryMinutes)
    {
        var action = purpose == Models.OtpPurpose.Register ? "verify your AurSil account" : "reset your AurSil password";
        var body = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto;border:1px solid #e5e5e5;border-radius:12px;overflow:hidden">
              <div style="background:#0B1F3A;padding:20px;text-align:center">
                <span style="color:#F5B82E;font-size:26px;font-weight:700">AurSil</span>
                <div style="color:#cfd6e4;font-size:12px">Invest | Grow | Secure</div>
              </div>
              <div style="padding:24px;color:#222">
                <p>Hi {WebUtility.HtmlEncode(name)},</p>
                <p>Use this one-time password to {action}:</p>
                <p style="font-size:32px;font-weight:700;letter-spacing:8px;text-align:center;color:#0B1F3A">{code}</p>
                <p style="color:#666;font-size:13px">This code expires in {expiryMinutes} minutes. Do not share it with anyone.</p>
              </div>
            </div>
            """;

        // Show OTPs in the console during development, or when email isn't set up yet
        if (env.IsDevelopment() || !IsConfigured)
            logger.LogWarning("OTP for {Email} ({Purpose}): {Code}", toEmail, purpose, code);
        await SendAsync(toEmail, $"AurSil OTP: {code}", body);
    }

    public async Task SendAsync(string toEmail, string subject, string html)
    {
        if (!IsConfigured)
        {
            logger.LogInformation("SMTP not configured, skipped email to {Email}: {Subject}", toEmail, subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(options.Host, options.Port)
            {
                EnableSsl = options.EnableSsl,
                Credentials = new NetworkCredential(options.UserName, options.Password),
            };
            using var message = new MailMessage
            {
                From = new MailAddress(options.FromEmail, options.FromName),
                Subject = subject,
                Body = html,
                IsBodyHtml = true,
            };
            message.To.Add(toEmail);
            await client.SendMailAsync(message);
            logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // A mail failure must not break registration or payments
            logger.LogError("Email to {Email} failed: {Message}", toEmail, ex.Message);
        }
    }
}
