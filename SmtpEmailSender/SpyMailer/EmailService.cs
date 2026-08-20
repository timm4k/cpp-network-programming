using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SpyMailer;

internal sealed class SmtpConfig
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public int StudentNumber { get; init; }
}

internal sealed class RecipientInfo
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

internal sealed class EmailService
{
    private const int MaxRetries = 3;
    private readonly SmtpConfig _config;

    public EmailService(SmtpConfig config)
    {
        _config = config;
    }

    public static SmtpConfig LoadConfig(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SmtpConfig>(json)
            ?? throw new InvalidOperationException("Failed to load config");
    }

    public static List<string> LoadMessages(string path)
    {
        return File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    public async Task<int> SendAllAsync(string recipientEmail, List<string> messages, int shift)
    {
        int sent = 0;

        for (int i = 0; i < messages.Count; i++)
        {
            string encrypted = CryptoHelper.Encrypt(messages[i], shift);
            string decrypted = CryptoHelper.Decrypt(encrypted, shift);
            string name = recipientEmail.Split('@')[0];

            bool success = await SendWithRetryAsync(new RecipientInfo { Email = recipientEmail, Name = name }, messages[i], encrypted, decrypted);
            if (success) sent++;

            int percent = (int)((i + 1.0) / messages.Count * 100);
            Console.Write($"\rSending... {percent}%");
        }

        Console.WriteLine();
        return sent;
    }

    public async Task<bool> SendSingleAsync(string email, string name, string message, int shift)
    {
        string encrypted = CryptoHelper.Encrypt(message, shift);
        string decrypted = CryptoHelper.Decrypt(encrypted, shift);
        RecipientInfo recipient = new() { Email = email, Name = name };
        return await SendWithRetryAsync(recipient, message, encrypted, decrypted);
    }

    private async Task<bool> SendWithRetryAsync(RecipientInfo recipient, string original, string encrypted, string decrypted)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await SendEmailAsync(recipient, original, encrypted, decrypted);
                return true;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Console.WriteLine($"  Attempt {attempt} failed: {ex.Message}. Retrying...");
                await Task.Delay(1000 * attempt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Failed after {MaxRetries} attempts: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    private async Task SendEmailAsync(RecipientInfo recipient, string original, string encrypted, string decrypted)
    {
        var emailMessage = new MimeMessage();
        emailMessage.From.Add(new MailboxAddress(_config.FromName, _config.Email));
        emailMessage.To.Add(new MailboxAddress(recipient.Name, recipient.Email));
        emailMessage.Subject = $"Spy Mail: {CryptoHelper.Encrypt("Secret", 3)}";

        int key = CryptoHelper.GetKeyFromStudentNumber(_config.StudentNumber);
        string body = BuildHtmlBody(recipient.Name, original, encrypted, decrypted, key);

        var bodyBuilder = new BodyBuilder { HtmlBody = body };

        if (Directory.Exists("images"))
        {
            string[] imageFiles = Directory.GetFiles("images")
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (imageFiles.Length > 0)
            {
                Random rng = new();
                string randomImage = imageFiles[rng.Next(imageFiles.Length)];
                var image = bodyBuilder.LinkedResources.Add(randomImage);
                image.ContentId = "spyimage";
                body = body.Replace("</div>", $"""
                    <div style="margin-top:16px;text-align:center">
                      <img src="cid:spyimage" alt="Secret" style="border:3px solid #7c3aed;border-radius:8px;max-width:400px"/>
                    </div>
                    </div>
                    """);
                bodyBuilder.HtmlBody = body;
                Console.WriteLine($"  Attached: {Path.GetFileName(randomImage)}");
            }
        }

        emailMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_config.Host, _config.Port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_config.Email, _config.Password);
        await client.SendAsync(emailMessage);
        await client.DisconnectAsync(true);
    }

    private static string BuildHtmlBody(string name, string original, string encrypted, string decrypted, int key)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"/></head>
            <body style="font-family:Arial,sans-serif;background:#1e1b2e;color:#d8d0e8;padding:20px">
              <div style="max-width:500px;margin:0 auto;background:#2a2440;border:2px solid #7c3aed;border-radius:12px;padding:24px">
                <h2 style="color:#a78bfa;text-align:center;margin-bottom:16px">Encrypted Spy Message</h2>
                <p style="color:#c4b5fd">Hello, <strong style="color:#e9d5ff">{name}</strong></p>
                <p style="font-style:italic;color:#c4b5fd">{original}</p>
                <hr style="border-color:#7c3aed"/>
                <p><strong style="color:#a78bfa">Encrypted:</strong> <code style="background:#3b2d5e;color:#e9d5ff;padding:6px 10px;border-radius:6px">{encrypted}</code></p>
                <p><strong style="color:#a78bfa">Key:</strong> <span style="color:#c084fc;font-weight:bold">{key}</span></p>
                <p><strong style="color:#a78bfa">Decrypted:</strong> <em style="color:#c4b5fd">{decrypted}</em></p>
              </div>
            </body>
            </html>
            """;
    }

    public static void LogSentEmail(string path, string email, string subject, bool success)
    {
        string status = success ? "OK" : "FAIL";
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        File.AppendAllText(path, $"[{timestamp}] {status} {email} | {subject}{Environment.NewLine}");
    }
}
