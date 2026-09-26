using System.Text;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Capture;
using Paper.ScreenWizzard.Infrastructure.Shared;
using Paper.ScreenWizzard.Infrastructure.Shell;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>The file store, the settings store, the log and the clock, in a throw-away folder under the temp folder.</summary>
[TestFixture]
public sealed class StoresTests
{
    private ScratchFolder _scratch = null!;

    [SetUp]
    public void NewScratch() => _scratch = new ScratchFolder();

    [TearDown]
    public void DeleteScratch() => _scratch.Dispose();

    /// <summary>Every field different from the defaults, so a field the store forgets shows.</summary>
    internal static AppSettings EveryFieldChanged() => new(
        new Dictionary<CaptureKind, HotkeyChord>
        {
            [CaptureKind.Rectangle] = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "A"),
            [CaptureKind.Freeform] = new(HotkeyModifiers.Alt, "F9"),
            [CaptureKind.Window] = new(HotkeyModifiers.None, "PrintScreen"),
            [CaptureKind.FullScreen] = new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows, "7"),
        },
        AfterCaptureAction.ClipboardAndFile,
        @"D:\Ảnh chụp\Màn hình (đợt 1)",
        ImageFormat.Jpg,
        77,
        3,
        true,
        FullScreenScope.AllMonitors,
        true,
        AppLanguage.Vietnamese,
        AppTheme.Dark,
        new PixelPoint(-1200, 40));

    internal static void AssertSame(AppSettings actual, AppSettings expected)
    {
        Assert.That(actual.Hotkeys.OrderBy(h => h.Key).ToArray(), Is.EqualTo(expected.Hotkeys.OrderBy(h => h.Key).ToArray()), "hotkeys");
        Assert.That(actual.AfterCapture, Is.EqualTo(expected.AfterCapture), "AfterCapture");
        Assert.That(actual.SaveFolder, Is.EqualTo(expected.SaveFolder), "SaveFolder");
        Assert.That(actual.Format, Is.EqualTo(expected.Format), "Format");
        Assert.That(actual.JpgQuality, Is.EqualTo(expected.JpgQuality), "JpgQuality");
        Assert.That(actual.DelaySeconds, Is.EqualTo(expected.DelaySeconds), "DelaySeconds");
        Assert.That(actual.IncludeCursor, Is.EqualTo(expected.IncludeCursor), "IncludeCursor");
        Assert.That(actual.FullScreenScope, Is.EqualTo(expected.FullScreenScope), "FullScreenScope");
        Assert.That(actual.StartWithWindows, Is.EqualTo(expected.StartWithWindows), "StartWithWindows");
        Assert.That(actual.Language, Is.EqualTo(expected.Language), "Language");
        Assert.That(actual.Theme, Is.EqualTo(expected.Theme), "Theme");
        Assert.That(actual.CaptureBarPosition, Is.EqualTo(expected.CaptureBarPosition), "CaptureBarPosition");
    }

    // ---- file store ----

    [Test]
    public void FileStore_WritesReadsAndReportsTimes()
    {
        var store = new FileStore();
        var folder = Path.Combine(_scratch.Path, "a", "b");
        Assert.That(store.DirectoryExists(folder), Is.False);
        Assert.That(store.CreateDirectory(folder).Success, Is.True);
        Assert.That(store.DirectoryExists(folder), Is.True);

        var file = Path.Combine(folder, "picture.png");
        var bytes = new byte[] { 1, 2, 3, 250 };
        Assert.That(store.FileExists(file), Is.False);
        Assert.That(store.GetLastWriteTimeUtc(file), Is.Null, "a file that is not there has no write time");
        Assert.That(store.WriteAllBytes(file, bytes).Success, Is.True);

        Assert.That(store.FileExists(file), Is.True);
        Assert.That(store.ReadAllBytes(file).Bytes, Is.EqualTo(bytes));
        Assert.That(store.GetLastWriteTimeUtc(file), Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void FileStore_ReadingAMissingFile_FailsWithThePathAndTheReason()
    {
        var missing = Path.Combine(_scratch.Path, "nope.png");

        var read = new FileStore().ReadAllBytes(missing);

        Assert.That(read.Success, Is.False);
        Assert.That(read.Bytes, Is.Null);
        Assert.That(read.Detail, Does.StartWith(missing + ": "));
    }

    // ---- settings store ----

    [Test]
    public void Settings_NoFileYet_IsMissing()
    {
        var load = new SettingsStore(_scratch.Path).Load();

        Assert.That(load.Status, Is.EqualTo(SettingsLoadStatus.Missing));
        Assert.That(load.Settings, Is.Null);
    }

    [Test]
    public void Settings_EveryFieldSurvivesSaveAndLoad_AndTheFileIsWhereTheDocsSayItIs()
    {
        var store = new SettingsStore(_scratch.Path);
        var settings = EveryFieldChanged();

        var saved = store.Save(settings);
        var load = store.Load();

        Assert.That(saved.Success, Is.True, saved.Detail);
        Assert.That(load.Status, Is.EqualTo(SettingsLoadStatus.Loaded), load.Detail);
        AssertSame(load.Settings!, settings);
        var file = Path.Combine(_scratch.Path, "configs", "settings.json");
        Assert.That(File.Exists(file), Is.True, "<data root>\\configs\\settings.json");
        var text = File.ReadAllText(file, Encoding.UTF8);
        Assert.That(text, Does.Contain("\"Dark\"").And.Contain("\"ClipboardAndFile\""), "enums are written as names, not numbers");
        Assert.That(text, Does.Contain("\n"), "the file is indented so a person can read it");
        Assert.That(Directory.EnumerateFiles(Path.Combine(_scratch.Path, "configs")).Select(Path.GetFileName), Is.EqualTo(new[] { "settings.json" }), "no temp file is left behind");
    }

    [Test]
    public void Settings_SavingTwice_ReplacesTheFile()
    {
        var store = new SettingsStore(_scratch.Path);
        var first = EveryFieldChanged();
        var second = first with { JpgQuality = 12, Theme = AppTheme.Light, CaptureBarPosition = null };

        Assert.That(store.Save(first).Success, Is.True);
        Assert.That(store.Save(second).Success, Is.True);

        AssertSame(store.Load().Settings!, second);
    }

    [Test]
    public void Settings_ACorruptFile_IsReportedCorrupt_AndTheBadBytesAreKeptAsBak()
    {
        var folder = Path.Combine(_scratch.Path, "configs");
        Directory.CreateDirectory(folder);
        var bad = Encoding.UTF8.GetBytes("{ \"Hotkeys\": [ this is not json");
        File.WriteAllBytes(Path.Combine(folder, "settings.json"), bad);

        var load = new SettingsStore(_scratch.Path).Load();

        Assert.That(load.Status, Is.EqualTo(SettingsLoadStatus.Corrupt));
        Assert.That(load.Settings, Is.Null);
        Assert.That(File.Exists(Path.Combine(folder, "settings.json.bak")), Is.True, "the bad file is kept next to it");
        Assert.That(File.ReadAllBytes(Path.Combine(folder, "settings.json.bak")), Is.EqualTo(bad), "the bad file is kept as settings.json.bak");
    }

    [Test]
    public void Settings_ValidJsonOfTheWrongShape_IsCorruptToo_AndAnOlderBakIsOverwritten()
    {
        var folder = Path.Combine(_scratch.Path, "configs");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "settings.json.bak"), "old backup");
        var wrongShape = Encoding.UTF8.GetBytes("[1, 2, 3]");
        File.WriteAllBytes(Path.Combine(folder, "settings.json"), wrongShape);

        var load = new SettingsStore(_scratch.Path).Load();

        Assert.That(load.Status, Is.EqualTo(SettingsLoadStatus.Corrupt));
        Assert.That(File.Exists(Path.Combine(folder, "settings.json.bak")), Is.True);
        Assert.That(File.ReadAllBytes(Path.Combine(folder, "settings.json.bak")), Is.EqualTo(wrongShape));
    }

    [Test]
    public void Settings_AnEmptyFile_IsCorrupt()
    {
        var folder = Path.Combine(_scratch.Path, "configs");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "settings.json"), []);

        Assert.That(new SettingsStore(_scratch.Path).Load().Status, Is.EqualTo(SettingsLoadStatus.Corrupt));

        // Control: the same store reads a good file, so "Corrupt" above is a judgement about the empty file and not the store's only answer.
        using var other = new ScratchFolder();
        var good = new SettingsStore(other.Path);
        Assert.That(good.Save(EveryFieldChanged()).Success, Is.True);
        Assert.That(good.Load().Status, Is.EqualTo(SettingsLoadStatus.Loaded));
    }

    [Test]
    public void Settings_AFileWithAUtf8ByteOrderMark_IsReadNotCalledCorrupt()
    {
        // Notepad and PowerShell 5 put EF BB BF at the start of a file they save; a file that opens fine in an editor is not corrupt.
        var store = new SettingsStore(_scratch.Path);
        var settings = EveryFieldChanged();
        Assert.That(store.Save(settings).Success, Is.True);
        var file = Path.Combine(_scratch.Path, "configs", "settings.json");
        File.WriteAllBytes(file, [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(file)]);

        var load = new SettingsStore(_scratch.Path).Load();

        Assert.That(load.Status, Is.EqualTo(SettingsLoadStatus.Loaded), load.Detail);
        AssertSame(load.Settings!, settings);
    }

    [TestCase("{\"hotkeys\": null}")]
    [TestCase("{\"hotkeys\": {\"Rectangle\": null}}")]
    public void Settings_ANullHotkeyTableOrChord_IsCorruptWithABak_NotACrash(string json)
    {
        var folder = Path.Combine(_scratch.Path, "configs");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "settings.json"), json);

        var load = new SettingsStore(_scratch.Path).Load();

        Assert.That(load.Status, Is.EqualTo(SettingsLoadStatus.Corrupt));
        Assert.That(File.Exists(Path.Combine(folder, "settings.json.bak")), Is.True);
    }

    [Test]
    public void Settings_AFileLockedByAnotherProgram_IsUnreadableNotCorrupt_AndASaveWhileLockedFailsWithThePath()
    {
        var store = new SettingsStore(_scratch.Path);
        var good = EveryFieldChanged();
        Assert.That(store.Save(good).Success, Is.True);
        var folder = Path.Combine(_scratch.Path, "configs");
        var file = Path.Combine(folder, "settings.json");
        var fresh = new SettingsStore(_scratch.Path);

        SettingsLoadResult load;
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            load = fresh.Load();
            Assert.That(fresh.Save(good with { JpgQuality = 5 }).Success, Is.False, "still locked: nothing is written");
        }

        Assert.That(load.Status, Is.EqualTo(SettingsLoadStatus.Unreadable));
        Assert.That(File.Exists(file + ".bak"), Is.False, "a locked file is not a corrupt one, so no backup");
        Assert.That(load.Detail, Does.StartWith(file + ": "));

        // Whether a save may replace a file the app never saw is the use case's decision (ShellInteractor); the adapter just writes.
        Assert.That(fresh.Save(good with { JpgQuality = 5 }).Success, Is.True);
    }

    [Test]
    public void Settings_SavingOverAReadOnlyFile_FailsWithTheFullPathFirst()
    {
        var store = new SettingsStore(_scratch.Path);
        Assert.That(store.Save(EveryFieldChanged()).Success, Is.True);
        var file = Path.Combine(_scratch.Path, "configs", "settings.json");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        var refused = store.Save(EveryFieldChanged() with { JpgQuality = 5 });

        Assert.That(refused.Success, Is.False, "a read-only settings file is not silently overwritten");
        Assert.That(refused.Detail, Does.StartWith(file + ": "));
        Assert.That(store.Load().Settings!.JpgQuality, Is.EqualTo(77), "the file on disk is unchanged");
    }

    // ---- log ----

    [Test]
    public void Log_AppendsToADailyFile_AndNeverThrows()
    {
        var logger = new FileLogger(_scratch.Path);

        logger.Info("hello");
        logger.Warning("careful");
        logger.Error("broken", new InvalidOperationException("because"));

        var file = Path.Combine(_scratch.Path, "logs", DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        var text = File.ReadAllText(file);
        Assert.That(text, Does.Contain("INFO hello").And.Contain("WARN careful").And.Contain("ERROR broken").And.Contain("because"));

        // The log folder cannot be made (its parent is a file): still no exception.
        var blocker = Path.Combine(_scratch.Path, "blocker");
        File.WriteAllText(blocker, "x");
        Assert.DoesNotThrow(() => new FileLogger(blocker).Error("no place to write", null));
    }

    [Test]
    public void Clock_IsLocalTime_AndDelayWaits()
    {
        Assert.That(new SystemClock().Now, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(2)));
        Assert.That(new SystemClock().Now.Kind, Is.EqualTo(DateTimeKind.Local));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        new TaskDelay().DelayAsync(TimeSpan.FromMilliseconds(150), CancellationToken.None).Wait();
        Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(120));
    }
}
