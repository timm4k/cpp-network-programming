using Xunit;
using Core.Services;
using Core.Models;

namespace Tests;

static class TestHelper
{
    public static AuthService NewAuth() => new(Path.Combine(Path.GetTempPath(), "nctest_" + Guid.NewGuid().ToString("N")[..8]));
    public static RoomService NewRoom() => new(Path.Combine(Path.GetTempPath(), "nctest_" + Guid.NewGuid().ToString("N")[..8]));
}

public class AuthServiceTests
{
    [Fact]
    public void Register_ValidUser_ReturnsSuccess()
    {
        AuthService auth = TestHelper.NewAuth();
        var (success, error, user) = auth.Register("testuser", "pass123");

        Assert.True(success);
        Assert.Empty(error);
        Assert.NotNull(user);
        Assert.Equal("testuser", user!.Login);
    }

    [Fact]
    public void Register_DuplicateLogin_ReturnsError()
    {
        AuthService auth = TestHelper.NewAuth();
        auth.Register("testuser", "pass123");
        var (success, error, _) = auth.Register("testuser", "pass456");

        Assert.False(success);
        Assert.Contains("taken", error);
    }

    [Fact]
    public void Register_ShortLogin_ReturnsError()
    {
        AuthService auth = TestHelper.NewAuth();
        var (success, error, _) = auth.Register("a", "pass123");

        Assert.False(success);
        Assert.Contains("2 characters", error);
    }

    [Fact]
    public void Login_CorrectCredentials_ReturnsSuccess()
    {
        AuthService auth = TestHelper.NewAuth();
        auth.Register("testuser", "pass123");
        var (success, error, user) = auth.Login("testuser", "pass123");

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal("testuser", user!.Login);
    }

    [Fact]
    public void Login_WrongPassword_ReturnsError()
    {
        AuthService auth = TestHelper.NewAuth();
        auth.Register("testuser", "pass123");
        var (success, error, _) = auth.Login("testuser", "wrong");

        Assert.False(success);
        Assert.Contains("password", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Login_NonExistentUser_ReturnsError()
    {
        AuthService auth = TestHelper.NewAuth();
        var (success, error, _) = auth.Login("nobody", "pass");

        Assert.False(success);
        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Admin_LoginByDefault()
    {
        AuthService auth = TestHelper.NewAuth();
        var (_, _, user) = auth.Login("admin", "admin");

        Assert.NotNull(user);
        Assert.True(user!.IsAdmin);
    }

    [Fact]
    public void DeleteUser_RemovesUser()
    {
        AuthService auth = TestHelper.NewAuth();
        auth.Register("testuser", "pass123");
        bool result = auth.DeleteUser("testuser");

        Assert.True(result);
        Assert.Null(auth.GetUser("testuser"));
    }

    [Fact]
    public void DeleteUser_CannotDeleteAdmin()
    {
        AuthService auth = TestHelper.NewAuth();
        bool result = auth.DeleteUser("admin");

        Assert.False(result);
    }

    [Fact]
    public void BanUser_SetsBan()
    {
        AuthService auth = TestHelper.NewAuth();
        auth.Register("testuser", "pass123");
        bool result = auth.BanUser("testuser", TimeSpan.FromMinutes(5), "spam");

        Assert.True(result);
        User? user = auth.GetUser("testuser");
        Assert.True(user!.IsBanned);
    }

    [Fact]
    public void UnbanUser_RemovesBan()
    {
        AuthService auth = TestHelper.NewAuth();
        auth.Register("testuser", "pass123");
        auth.BanUser("testuser", TimeSpan.FromMinutes(5), "spam");
        auth.UnbanUser("testuser");

        User? user = auth.GetUser("testuser");
        Assert.False(user!.IsBanned);
    }
}

public class CensorServiceTests
{
    [Fact]
    public void Filter_NoBannedWords_ReturnsOriginal()
    {
        CensorService censor = new();
        var (filtered, wasCensored) = censor.Filter("Hello world");

        Assert.Equal("Hello world", filtered);
        Assert.False(wasCensored);
    }

    [Fact]
    public void Filter_WithBannedWord_ReplacesWithHashes()
    {
        CensorService censor = new();
        var (filtered, wasCensored) = censor.Filter("You are a bastard");

        Assert.Contains("###", filtered);
        Assert.True(wasCensored);
    }

    [Fact]
    public void Filter_CaseInsensitive()
    {
        CensorService censor = new();
        var (filtered, wasCensored) = censor.Filter("FUCK this");

        Assert.Contains("###", filtered);
        Assert.True(wasCensored);
    }

    [Fact]
    public void AddWord_NewWordIsFiltered()
    {
        CensorService censor = new();
        censor.AddWord("custom");
        var (filtered, wasCensored) = censor.Filter("custom word");

        Assert.Contains("###", filtered);
        Assert.True(wasCensored);
    }
}

public class RoomServiceTests
{
    [Fact]
    public void CreateRoom_AddsRoom()
    {
        RoomService rooms = TestHelper.NewRoom();
        Room? room = rooms.CreateRoom("test");

        Assert.NotNull(room);
        Assert.Equal("test", room!.Name);
        Assert.Contains("test", rooms.GetAllRoomNames());
    }

    [Fact]
    public void GeneralRoom_ExistsByDefault()
    {
        RoomService rooms = TestHelper.NewRoom();
        Assert.Contains("general", rooms.GetAllRoomNames());
    }

    [Fact]
    public void DeleteGeneral_ReturnsFalse()
    {
        RoomService rooms = TestHelper.NewRoom();
        Assert.False(rooms.DeleteRoom("general"));
    }

    [Fact]
    public void JoinRoom_AddsUser()
    {
        RoomService rooms = TestHelper.NewRoom();
        rooms.JoinRoom("general", "alice");

        Room? room = rooms.GetRoom("general");
        Assert.Contains("alice", room!.GetUsers());
    }

    [Fact]
    public void LeaveRoom_RemovesUser()
    {
        RoomService rooms = TestHelper.NewRoom();
        rooms.JoinRoom("general", "alice");
        rooms.LeaveRoom("general", "alice");

        Room? room = rooms.GetRoom("general");
        Assert.DoesNotContain("alice", room!.GetUsers());
    }
}
