using System.Text.RegularExpressions;

namespace Core.Services;

public sealed class CensorService
{
    private readonly object _lock = new();
    private readonly HashSet<string> _bannedWords = new(StringComparer.OrdinalIgnoreCase);

    public CensorService()
    {
        foreach (string w in DefaultWords) _bannedWords.Add(w);
    }

    public static readonly string[] DefaultWords = ["fuck", "shit", "damn", "bitch", "asshole", "bastard", "cunt"];

    public void AddWord(string word)
    {
        lock (_lock) _bannedWords.Add(word);
    }

    public (string filtered, bool wasCensored) Filter(string text)
    {
        string result = text;
        bool censored = false;

        lock (_lock)
        {
            foreach (string word in _bannedWords)
            {
                string pattern = $@"\b{Regex.Escape(word)}\b";
                if (Regex.IsMatch(result, pattern, RegexOptions.IgnoreCase))
                {
                    result = Regex.Replace(result, pattern, "###", RegexOptions.IgnoreCase);
                    censored = true;
                }
            }
        }

        return (result, censored);
    }
}
