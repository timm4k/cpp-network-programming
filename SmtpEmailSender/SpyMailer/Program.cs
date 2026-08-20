namespace SpyMailer;

internal static class Program
{
    private const string ConfigPath = "config.json";
    private const string MessagesPath = "messages.txt";
    private const string LogPath = "sent_emails.log";
    private static readonly ConsoleColor Purple = ConsoleColor.DarkMagenta;

    static async Task Main()
    {
        SmtpConfig config = EmailService.LoadConfig(ConfigPath);
        int shift = CryptoHelper.GetKeyFromStudentNumber(config.StudentNumber);

        WritePurple("=== SpyMailer ===");

        while (true)
        {
            Console.WriteLine();
            WritePurple("[1] Send All Emails");
            WritePurple("[2] Test Single");
            WritePurple("[3] Encrypt Text");
            WritePurple("[0] Exit");
            Console.Write("Select: ");

            string? choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await SendAllAsync(config, shift);
                    break;
                case "2":
                    await TestSingleAsync(config, shift);
                    break;
                case "3":
                    EncryptText(shift);
                    break;
                case "0":
                    return;
                default:
                    WritePurple("Invalid option");
                    break;
            }
        }
    }

    private static void WritePurple(string text)
    {
        ConsoleColor prev = Console.ForegroundColor;
        Console.ForegroundColor = Purple;
        Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }

    private static async Task SendAllAsync(SmtpConfig config, int shift)
    {
        Console.Write("Recipient email: ");
        string? recipientEmail = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(recipientEmail)) return;

        List<string> messages = EmailService.LoadMessages(MessagesPath);

        EmailService service = new(config);
        int sent = await service.SendAllAsync(recipientEmail, messages, shift);

        WritePurple($"Done: {sent}/{messages.Count} emails sent");
        EmailService.LogSentEmail(LogPath, recipientEmail, "Bulk", sent == messages.Count);
    }

    private static async Task TestSingleAsync(SmtpConfig config, int shift)
    {
        Console.Write("Recipient email: ");
        string? recipientEmail = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(recipientEmail)) return;

        Console.Write("Recipient name: ");
        string? name = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(name)) name = "Agent";

        Console.Write("Message: ");
        string? message = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(message)) return;

        EmailService service = new(config);
        bool success = await service.SendSingleAsync(recipientEmail, name, message, shift);

        if (success) { WritePurple("Sent"); } else { WritePurple("Failed to send"); }
        EmailService.LogSentEmail(LogPath, recipientEmail, "Test", success);
    }

    private static void EncryptText(int shift)
    {
        Console.Write("Text to encrypt: ");
        string? text = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(text)) return;

        string encrypted = CryptoHelper.Encrypt(text, shift);
        string decrypted = CryptoHelper.Decrypt(encrypted, shift);

        Console.Write("Original:  "); WritePurple(text);
        Console.Write("Encrypted: "); WritePurple(encrypted);
        Console.Write("Decrypted: "); WritePurple(decrypted);
        Console.Write("Key: "); WritePurple(shift.ToString());
    }
}
