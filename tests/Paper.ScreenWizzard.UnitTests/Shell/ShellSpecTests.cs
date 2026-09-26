using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Shell.Fakes;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UnitTests.Shell;

/// <summary>SPEC shell: Inputs, "Chỉ một bản chạy", "Cài đặt được giữ lại", "Khởi động cùng Windows", "Ngôn ngữ", F1-F3, F5-F7.</summary>
[TestFixture]
public sealed class ShellSpecTests
{
    private static readonly PixelSize _barSize = new(400, 48);

    private ShellFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new ShellFixture();

    private static ShellStartInput StartInput(
        bool first = true,
        string culture = "en-US",
        IReadOnlyList<MonitorInfo>? monitors = null) =>
        new(first, ShellData.Pictures, culture, monitors ?? ShellData.OnePrimary, _barSize);

    // ---- Inputs: the defaults ----

    [Test]
    public void DefaultSettingsAreTheOnesOfTheInputsTable()
    {
        var settings = _fixture.Create().CreateDefaultSettings(ShellData.Pictures);

        Assert.That(settings.Hotkeys.GetValueOrDefault(CaptureKind.Rectangle), Is.EqualTo(new HotkeyChord(HotkeyModifiers.None, "PrintScreen")));
        Assert.That(settings.Hotkeys.GetValueOrDefault(CaptureKind.Freeform), Is.EqualTo(new HotkeyChord(HotkeyModifiers.Shift, "PrintScreen")));
        Assert.That(settings.Hotkeys.GetValueOrDefault(CaptureKind.Window), Is.EqualTo(new HotkeyChord(HotkeyModifiers.Alt, "PrintScreen")));
        Assert.That(settings.Hotkeys.GetValueOrDefault(CaptureKind.FullScreen), Is.EqualTo(new HotkeyChord(HotkeyModifiers.Control, "PrintScreen")));
        Assert.That(settings.Hotkeys, Has.Count.EqualTo(4));
        Assert.That(settings.AfterCapture, Is.EqualTo(AfterCaptureAction.ShowDialog));
        Assert.That(settings.SaveFolder, Is.EqualTo(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard"));
        Assert.That(settings.Format, Is.EqualTo(ImageFormat.Png));
        Assert.That(settings.JpgQuality, Is.EqualTo(90));
        Assert.That(settings.DelaySeconds, Is.EqualTo(0));
        Assert.That(settings.IncludeCursor, Is.False);
        Assert.That(settings.FullScreenScope, Is.EqualTo(FullScreenScope.MonitorUnderCursor));
        Assert.That(settings.StartWithWindows, Is.False);
        Assert.That(settings.Language, Is.EqualTo(AppLanguage.System));
        Assert.That(settings.Theme, Is.EqualTo(AppTheme.System));
        Assert.That(settings.CaptureBarPosition, Is.Null);
    }

    // ---- Start ----

    [Test]
    public void FirstRunUsesDefaultsSavesThemRegistersFourHotkeysAndShowsTheBar()
    {
        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Missing, null);

        var result = _fixture.Create().Start(StartInput(culture: "vi-VN"));

        Assert.That(result.ContinueRunning, Is.True);
        Assert.That(result.Settings.SaveFolder, Is.EqualTo(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard"));
        Assert.That(result.Settings.Hotkeys[CaptureKind.FullScreen], Is.EqualTo(ShellData.CtrlPrintScreen));
        Assert.That(_fixture.Store.Saved, Has.Count.EqualTo(1));
        Assert.That(_fixture.Store.Saved[0].SaveFolder, Is.EqualTo(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard"));
        Assert.That(
            result.HotkeysRegistered,
            Is.EqualTo(new[] { CaptureKind.Rectangle, CaptureKind.Freeform, CaptureKind.Window, CaptureKind.FullScreen }));
        Assert.That(_fixture.Hotkeys.Registered, Has.Count.EqualTo(4));
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(result.ShowCaptureBar, Is.True);
        Assert.That(result.Notices, Is.Empty);
        Assert.That(result.Language, Is.EqualTo(ResolvedLanguage.Vietnamese));
        Assert.That(result.CaptureBarPosition, Is.EqualTo(new PixelPoint(1504, 16)));
    }

    [Test]
    public void SavedSettingsAreUsedAsTheyWereSaved()
    {
        var saved = ShellData.SpecDefaults() with
        {
            SaveFolder = @"D:\Shots",
            Language = AppLanguage.English,
            CaptureBarPosition = new PixelPoint(2500, 300),
        };
        ((Dictionary<CaptureKind, HotkeyChord>)saved.Hotkeys)[CaptureKind.Rectangle] =
            ShellData.Chord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "1");
        _fixture.Store.LoadResult = new SettingsLoadResult(saved, SettingsLoadStatus.Loaded, null);
        var monitors = new[]
        {
            ShellData.Monitor(0, 0, 0, 1920, 1080, true),
            ShellData.Monitor(1, 1920, 0, 1920, 1080, false),
        };

        var result = _fixture.Create().Start(StartInput(culture: "vi-VN", monitors: monitors));

        Assert.That(result.Settings.SaveFolder, Is.EqualTo(@"D:\Shots"));
        Assert.That(
            _fixture.Hotkeys.Registered[CaptureKind.Rectangle],
            Is.EqualTo(ShellData.Chord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "1")));
        Assert.That(result.Language, Is.EqualTo(ResolvedLanguage.English));
        Assert.That(result.CaptureBarPosition, Is.EqualTo(new PixelPoint(2500, 300)));
        Assert.That(result.ShowCaptureBar, Is.True);
        Assert.That(result.Notices, Is.Empty);
        Assert.That(_fixture.Store.Saved, Is.Empty);
    }

    // Cho ứng dụng đã chạy, mở ứng dụng lần nữa -> không có bản thứ hai
    [Test]
    public void OnlyOneCopyASecondCopyDoesNotContinueAndTouchesNothing()
    {
        var result = _fixture.Create().Start(StartInput(first: false));

        Assert.That(result.ContinueRunning, Is.False);
        Assert.That(result.ShowCaptureBar, Is.False);
        Assert.That(_fixture.Store.LoadCalls, Is.EqualTo(0));
        Assert.That(_fixture.Hotkeys.RegisterCalls, Is.Empty);
        Assert.That(_fixture.Store.Saved, Is.Empty);
    }

    // F1: tập tin cài đặt hỏng -> chạy với mặc định; thông báo nói cài đặt cũ bị hỏng (bản .bak do adapter giữ)
    [Test]
    public void F1_CorruptSettingsFileStartsWithDefaultsAndANotice()
    {
        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Corrupt, "Unexpected end of JSON");

        var result = _fixture.Create().Start(StartInput());

        Assert.That(result.ContinueRunning, Is.True);
        Assert.That(result.Settings.SaveFolder, Is.EqualTo(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard"));
        Assert.That(result.Settings.Hotkeys[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(result.Notices.Select(n => n.Key), Is.EqualTo(new[] { "Shell.SettingsCorrupt" }));
        Assert.That(_fixture.Hotkeys.Registered, Has.Count.EqualTo(4));
    }

    // F7: không đăng ký được một phím lúc khởi động -> thông báo nêu phím nào; ứng dụng và các phím khác vẫn chạy
    [Test]
    public void F7_HotkeyThatCannotBeRegisteredAtStartIsNamedAndTheOthersStay()
    {
        _fixture.Hotkeys.HeldByOtherPrograms.Add(ShellData.AltPrintScreen);
        _fixture.Hotkeys.HeldByOtherPrograms.Add(ShellData.CtrlPrintScreen);

        var result = _fixture.Create().Start(StartInput());

        Assert.That(result.ContinueRunning, Is.True);
        Assert.That(result.HotkeysRegistered, Is.EqualTo(new[] { CaptureKind.Rectangle, CaptureKind.Freeform }));
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Freeform], Is.EqualTo(ShellData.ShiftPrintScreen));
        Assert.That(result.Notices, Has.Count.EqualTo(1));
        Assert.That(result.Notices[0].Key, Is.EqualTo("Shell.HotkeyUnavailableAtStart"));
        Assert.That(result.Notices[0].Arguments, Is.EqualTo(new[] { "Alt+PrintScreen, Ctrl+PrintScreen" }));
    }

    // F6: mở ứng dụng lần hai -> bản đầu hiện thanh chụp; không có thông báo lỗi
    [Test]
    public void F6_SecondLaunchMakesTheFirstCopyShowItsCaptureBar()
    {
        var result = _fixture.Create().OnSecondInstanceLaunched();

        Assert.That(result.ShowCaptureBar, Is.True);
    }

    // ---- Cài đặt được giữ lại ----

    // Cho đổi thư mục lưu rồi đóng ứng dụng, mở lại -> thư mục lưu vẫn là thư mục mới
    [Test]
    public void ChangedSaveFolderSurvivesAClose()
    {
        var edited = ShellData.SpecDefaults() with { SaveFolder = @"D:\Shots" };
        var applied = _fixture.Create().Apply(edited);

        var reopened = _fixture.Create().Start(StartInput());

        Assert.That(applied.Saved, Is.True);
        Assert.That(applied.Message, Is.Null);
        Assert.That(applied.Settings, Is.SameAs(edited));
        Assert.That(reopened.Settings.SaveFolder, Is.EqualTo(@"D:\Shots"));
    }

    // Cho kéo thanh chụp tới một chỗ rồi đóng, mở lại -> ở đúng chỗ đó
    [Test]
    public void CaptureBarSavedInsideAMonitorOpensAtTheSamePlace()
    {
        var monitors = new[]
        {
            ShellData.Monitor(0, 0, 0, 1920, 1080, true),
            ShellData.Monitor(1, 1920, 0, 1920, 1080, false),
        };

        var place = _fixture.Create().PlaceCaptureBar(new PixelPoint(2500, 300), monitors, _barSize);

        Assert.That(place, Is.EqualTo(new PixelPoint(2500, 300)));
    }

    // A bar that only half shows on a monitor is still on that monitor.
    [Test]
    public void CaptureBarPartlyOnAMonitorIsKept()
    {
        var monitors = new[]
        {
            ShellData.Monitor(0, 0, 0, 1920, 1080, true),
            ShellData.Monitor(1, 1920, 0, 1920, 1080, false),
        };

        var place = _fixture.Create().PlaceCaptureBar(new PixelPoint(1900, 10), monitors, _barSize);

        Assert.That(place, Is.EqualTo(new PixelPoint(1900, 10)));
    }

    // ... nếu chỗ đó nay nằm ngoài mọi màn hình (đã rút màn hình) -> góc trên phải màn hình chính
    [Test]
    public void CaptureBarSavedOnAnUnpluggedMonitorGoesToTheTopRightOfThePrimary()
    {
        var place = _fixture.Create().PlaceCaptureBar(new PixelPoint(2500, 300), ShellData.OnePrimary, _barSize);

        Assert.That(place, Is.EqualTo(new PixelPoint(1504, 16)));
    }

    [Test]
    public void CaptureBarJustPastTheEdgeOfTheOnlyMonitorIsNotOnAnyMonitor()
    {
        var place = _fixture.Create().PlaceCaptureBar(new PixelPoint(1920, 0), ShellData.OnePrimary, _barSize);

        Assert.That(place, Is.EqualTo(new PixelPoint(1504, 16)));
    }

    [Test]
    public void TopRightUsesThePrimaryMonitorEvenWhenItIsNotAtTheOrigin()
    {
        var monitors = new[]
        {
            ShellData.Monitor(0, 0, 0, 1920, 1080, false),
            ShellData.Monitor(1, 1920, 0, 2560, 1440, true),
        };

        var place = _fixture.Create().PlaceCaptureBar(new PixelPoint(-500, 100), monitors, _barSize);

        Assert.That(place, Is.EqualTo(new PixelPoint(4064, 16)));
    }

    [Test]
    public void CaptureBarNeverPlacedGoesToTheTopRightOfThePrimary()
    {
        var place = _fixture.Create().PlaceCaptureBar(null, ShellData.OnePrimary, _barSize);

        Assert.That(place, Is.EqualTo(new PixelPoint(1504, 16)));
    }

    // F2: không ghi được tập tin cài đặt -> thông báo nêu lý do; dùng cài đặt trong phiên này
    [Test]
    public void F2_SettingsThatCannotBeWrittenAreReportedAndStillUsedThisSession()
    {
        const string Reason = @"Access to the path 'C:\Users\Hung\AppData\Roaming\Paper\ScreenWizzard\configs\settings.json' is denied.";
        _fixture.Store.SaveResult = PortResult.Fail(Reason);
        var edited = ShellData.SpecDefaults() with { SaveFolder = @"D:\Shots" };

        var result = _fixture.Create().Apply(edited);

        Assert.That(result.Saved, Is.False);
        Assert.That(result.Settings, Is.SameAs(edited));
        Assert.That(result.Message?.Key, Is.EqualTo("Shell.SettingsNotSaved"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { Reason }));
    }

    // ---- Khởi động cùng Windows ----

    // Cho bật -> ứng dụng có mặt trong danh sách khởi động
    [Test]
    public void AutostartOnWritesTheEntryAndIsRemembered()
    {
        var result = _fixture.Create().SetAutostart(ShellData.SpecDefaults(), true);

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.EqualTo(new[] { true }));
        Assert.That(result.Enabled, Is.True);
        Assert.That(result.Settings.StartWithWindows, Is.True);
        Assert.That(result.Message, Is.Null);
    }

    // Cho tắt -> ứng dụng không chạy khi đăng nhập (mục bị gỡ)
    [Test]
    public void AutostartOffRemovesTheEntry()
    {
        var on = ShellData.SpecDefaults() with { StartWithWindows = true };
        _fixture.Autostart.Enabled = true;

        var result = _fixture.Create().SetAutostart(on, false);

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.EqualTo(new[] { false }));
        Assert.That(result.Enabled, Is.False);
        Assert.That(result.Settings.StartWithWindows, Is.False);
        Assert.That(result.Message, Is.Null);
    }

    // F3: không ghi được mục khởi động -> công tắc trở về trạng thái cũ, thông báo nêu lý do
    [Test]
    public void F3_AutostartEntryThatCannotBeWrittenRevertsTheSwitchWithTheReason()
    {
        const string Reason = "Requested registry access is not allowed.";
        _fixture.Autostart.Result = PortResult.Fail(Reason);

        var result = _fixture.Create().SetAutostart(ShellData.SpecDefaults(), true);

        Assert.That(result.Enabled, Is.False);
        Assert.That(result.Settings.StartWithWindows, Is.False);
        Assert.That(result.Message?.Key, Is.EqualTo("Shell.AutostartFailed"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { Reason }));
    }

    // The switch goes back to its old state whichever way it was thrown.
    [Test]
    public void AutostartOffThatFailsStaysOn()
    {
        _fixture.Autostart.Result = PortResult.Fail("locked");
        var on = ShellData.SpecDefaults() with { StartWithWindows = true };

        var result = _fixture.Create().SetAutostart(on, false);

        Assert.That(result.Enabled, Is.True);
        Assert.That(result.Settings.StartWithWindows, Is.True);
        Assert.That(result.Message?.Key, Is.EqualTo("Shell.AutostartFailed"));
    }

    // ---- Ngôn ngữ ----

    // Cho ngôn ngữ "theo Windows" trên máy Windows tiếng Việt -> tiếng Việt; trên máy tiếng khác -> tiếng Anh
    [TestCase("vi-VN", ResolvedLanguage.Vietnamese)]
    [TestCase("VI-vn", ResolvedLanguage.Vietnamese)]
    [TestCase("vi", ResolvedLanguage.Vietnamese)]
    [TestCase("en-US", ResolvedLanguage.English)]
    [TestCase("fr-FR", ResolvedLanguage.English)]
    [TestCase("", ResolvedLanguage.English)]
    public void FollowWindowsPicksVietnameseOnlyOnAVietnameseMachine(string culture, ResolvedLanguage expected)
    {
        Assert.That(_fixture.Create().ResolveLanguage(AppLanguage.System, culture), Is.EqualTo(expected));
    }

    // Cho đổi ngôn ngữ trong Cài đặt -> chọn tường minh thắng ngôn ngữ của máy
    [TestCase(AppLanguage.Vietnamese, "en-US", ResolvedLanguage.Vietnamese)]
    [TestCase(AppLanguage.English, "vi-VN", ResolvedLanguage.English)]
    public void AnExplicitLanguageBeatsTheMachineOne(AppLanguage chosen, string culture, ResolvedLanguage expected)
    {
        Assert.That(_fixture.Create().ResolveLanguage(chosen, culture), Is.EqualTo(expected));
    }

    // ---- Thư mục lưu ----

    [Test]
    public void ExistingSaveFolderIsExists()
    {
        _fixture.Files.Directories.Add(@"D:\Shots");

        Assert.That(_fixture.Create().CheckSaveFolder(@"D:\Shots"), Is.EqualTo(FolderCheck.Exists));
    }

    // F5: thư mục lưu không tồn tại -> Cài đặt hỏi có tạo không (chưa tạo gì trước khi người dùng đồng ý)
    [Test]
    public void F5_MissingSaveFolderIsOfferedForCreationAndNothingIsCreatedYet()
    {
        var check = _fixture.Create().CheckSaveFolder(@"D:\Shots\New");

        Assert.That(check, Is.EqualTo(FolderCheck.MissingCanCreate));
        Assert.That(_fixture.Files.CreateDirectoryCalls, Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(@"Shots\New")]
    [TestCase(@"D:\Sh|ots")]
    [TestCase(@"D:\Shots\what?")]
    public void ATextThatIsNoAbsolutePathIsInvalid(string folder)
    {
        Assert.That(_fixture.Create().CheckSaveFolder(folder), Is.EqualTo(FolderCheck.Invalid));
    }

    [Test]
    public void CreatingTheFolderTheUserAgreedToMakesItExist()
    {
        var result = _fixture.Create().CreateSaveFolder(@"D:\Shots\New");

        Assert.That(result.Created, Is.True);
        Assert.That(_fixture.Files.CreateDirectoryCalls, Is.EqualTo(new[] { @"D:\Shots\New" }));
        Assert.That(_fixture.Files.Directories, Does.Contain(@"D:\Shots\New"));
    }

    [Test]
    public void AFolderThatCannotBeCreatedReportsTheReason()
    {
        _fixture.Files.CreateResult = PortResult.Fail("Access to the path 'D:\\' is denied.");

        var result = _fixture.Create().CreateSaveFolder(@"D:\Shots\New");

        Assert.That(result.Created, Is.False);
        Assert.That(result.Detail, Is.EqualTo("Access to the path 'D:\\' is denied."));
    }
}
