namespace SpyMailer;

internal static class CryptoHelper
{
    public static string Encrypt(string text, int shift)
    {
        shift = ((shift % 26) + 26) % 26;
        char[] result = new char[text.Length];

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsAsciiLetter(c))
            {
                char baseChar = char.IsUpper(c) ? 'A' : 'a';
                result[i] = (char)((c - baseChar + shift) % 26 + baseChar);
            }
            else
            {
                result[i] = c;
            }
        }

        return new string(result);
    }

    public static string Decrypt(string text, int shift)
    {
        return Encrypt(text, -shift);
    }

    public static int GetKeyFromStudentNumber(int studentNumber)
    {
        return studentNumber % 25;
    }
}
