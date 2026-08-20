using Xunit;

namespace SpyMailer.Tests;

public class CryptoHelperTests
{
    [Theory]
    [InlineData("ohayosiki", 3, "rkdbrvlnl")]
    [InlineData("ohayosiki dattebayosiki", 3, "rkdbrvlnl gdwwhedbrvlnl")]
    [InlineData("ABC", 1, "BCD")]
    [InlineData("xyz", 1, "yza")]
    [InlineData("abc", 25, "zab")]
    public void Encrypt_ShiftsCharactersCorrectly(string input, int shift, string expected)
    {
        string result = CryptoHelper.Encrypt(input, shift);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Khoor", 3, "Hello")]
    [InlineData("rkdbrvlnl gdwwhedbrvlnl", 3, "ohayosiki dattebayosiki")]
    public void Decrypt_ReversesEncryption(string input, int shift, string expected)
    {
        string result = CryptoHelper.Decrypt(input, shift);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ohayosiki", 0, "ohayosiki")]
    [InlineData("Test", 26, "Test")]
    public void Encrypt_ZeroShiftReturnsOriginal(string input, int shift, string expected)
    {
        string result = CryptoHelper.Encrypt(input, shift);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(42, 17)]
    [InlineData(25, 0)]
    [InlineData(1, 1)]
    [InlineData(50, 0)]
    public void GetKeyFromStudentNumber_ReturnsModulo25(int studentNumber, int expected)
    {
        int result = CryptoHelper.GetKeyFromStudentNumber(studentNumber);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_PreservesOriginal()
    {
        string original = "quick brown fox jumps over the lazy dog";
        int shift = 7;

        string encrypted = CryptoHelper.Encrypt(original, shift);
        string decrypted = CryptoHelper.Decrypt(encrypted, shift);

        Assert.NotEqual(original, encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_PreservesNonLetterCharacters()
    {
        string input = "ohayosiki dattebayosiki 123 @#$";
        int shift = 5;

        string result = CryptoHelper.Encrypt(input, shift);

        Assert.Equal(' ', result[9]);
        Assert.Equal('1', result[24]);
        Assert.Equal('2', result[25]);
        Assert.Equal('@', result[28]);
        Assert.Equal('#', result[29]);
        Assert.Equal('$', result[30]);
    }
}
