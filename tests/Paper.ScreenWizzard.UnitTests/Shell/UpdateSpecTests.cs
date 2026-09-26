using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Shell.Fakes;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.UseCases;

namespace Paper.ScreenWizzard.UnitTests.Shell;

/// <summary>SPEC shell, "Báo bản mới" and F9: the app says once that a newer version is out, and never bothers the user when it cannot tell.</summary>
[TestFixture]
public sealed class UpdateSpecTests
{
    // NUnit runs every test of a fixture on one instance: the fakes are made again for each test.
    private FakeReleaseFeed _feed = null!;
    private FakeBrowser _browser = null!;
    private FakeLog _log = null!;

    [SetUp]
    public void SetUp()
    {
        _feed = new FakeReleaseFeed();
        _browser = new FakeBrowser();
        _log = new FakeLog();
    }

    private UpdateInteractor Create() => new(_feed, _browser, _log);

    private static AppSettings On() => ShellData.SpecDefaults();

    [Test]
    public void TheCheckIsOnForANewUser()
    {
        Assert.That(SettingsDefaults.Create(@"C:\Users\An\Pictures").CheckForUpdates, Is.True, "SPEC shell Inputs: check for a new version, on");
    }

    [Test]
    public async Task ANewerRelease_IsOffered_WithItsReleasePage_AndSaidOnce()
    {
        _feed.Answer = new ReleaseFeedResult("v0.1.3", null);
        var updates = Create();

        var first = await updates.CheckAsync(On(), "0.1.2", CancellationToken.None);
        var second = await updates.CheckAsync(On(), "0.1.2", CancellationToken.None);

        Assert.That(first.Offer, Is.EqualTo(new UpdateOffer(new AppVersion(0, 1, 3), "https://github.com/Cazorlas/Paper.ScreenWizzard/releases/tag/v0.1.3")));
        Assert.That(first.Notice?.Key, Is.EqualTo("Shell.UpdateAvailable"));
        Assert.That(first.Notice!.Arguments, Is.EqualTo(new[] { "0.1.3" }), "the notice names the version");
        Assert.That(second.Offer, Is.EqualTo(first.Offer), "the tray line stays");
        Assert.That(second.Notice, Is.Null, "the same version is said once while the app runs");
    }

    [Test]
    public async Task AVersionNewerStill_IsSaidAgain()
    {
        var updates = Create();
        _feed.Answer = new ReleaseFeedResult("v0.1.3", null);
        await updates.CheckAsync(On(), "0.1.2", CancellationToken.None);

        _feed.Answer = new ReleaseFeedResult("v0.2.0", null);
        var later = await updates.CheckAsync(On(), "0.1.2", CancellationToken.None);

        Assert.That(later.Offer?.Version, Is.EqualTo(new AppVersion(0, 2, 0)));
        Assert.That(later.Notice, Is.Not.Null);
    }

    [TestCase("v0.1.2", "0.1.2")]
    [TestCase("v0.1.1", "0.1.2")]
    [TestCase("v0.9.9", "1.0.0")]
    public async Task TheSameOrAnOlderRelease_IsNotOffered(string tag, string running)
    {
        _feed.Answer = new ReleaseFeedResult(tag, null);

        var result = await Create().CheckAsync(On(), running, CancellationToken.None);

        Assert.That(result.Offer, Is.Null);
        Assert.That(result.Notice, Is.Null);
    }

    [Test]
    public async Task TheCheckOff_DoesNotAskGitHub()
    {
        _feed.Answer = new ReleaseFeedResult("v9.0.0", null);

        var result = await Create().CheckAsync(On() with { CheckForUpdates = false }, "0.1.2", CancellationToken.None);

        Assert.That(_feed.Calls, Is.EqualTo(0), "nothing leaves the PC when the user turned the check off");
        Assert.That(result.Offer, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("1.0")]
    public async Task ABuildWithoutAVersion_DoesNotAskGitHub(string? running)
    {
        _feed.Answer = new ReleaseFeedResult("v9.0.0", null);

        var result = await Create().CheckAsync(On(), running, CancellationToken.None);

        Assert.That(_feed.Calls, Is.EqualTo(0));
        Assert.That(result.Offer, Is.Null);
    }

    [Test]
    public async Task F9_NoAnswer_IsOnlyLogged()
    {
        _feed.Answer = new ReleaseFeedResult(null, "No such host is known.");

        var result = await Create().CheckAsync(On(), "0.1.2", CancellationToken.None);

        Assert.That(result, Is.EqualTo(UpdateCheckResult.None), "no offer, nothing said to the user");
        Assert.That(_log.Lines, Has.Some.Contains("No such host is known."));
    }

    [TestCase("latest")]
    [TestCase("v0.2.0-beta")]
    [TestCase("v0.2")]
    public async Task F9_ATagThatIsNotAVersion_IsNotOffered(string tag)
    {
        _feed.Answer = new ReleaseFeedResult(tag, null);

        var result = await Create().CheckAsync(On(), "0.1.2", CancellationToken.None);

        Assert.That(result.Offer, Is.Null);
        Assert.That(_log.Lines, Has.Some.Contains(tag));
    }

    [Test]
    public void TheDownloadPage_OpensInTheBrowser()
    {
        var offer = new UpdateOffer(new AppVersion(0, 1, 3), UpdateRules.ReleasePage(new AppVersion(0, 1, 3)));

        var message = Create().OpenDownloadPage(offer);

        Assert.That(message, Is.Null);
        Assert.That(_browser.Opened, Is.EqualTo(new[] { "https://github.com/Cazorlas/Paper.ScreenWizzard/releases/tag/v0.1.3" }));
    }

    [Test]
    public void APageThatDidNotOpen_IsSaid_WithItsAddress()
    {
        _browser.Result = PortResult.Fail("no browser");
        var offer = new UpdateOffer(new AppVersion(0, 1, 3), UpdateRules.ReleasePage(new AppVersion(0, 1, 3)));

        var message = Create().OpenDownloadPage(offer);

        Assert.That(message?.Key, Is.EqualTo("Shell.UpdatePageNotOpened"));
        Assert.That(message!.Arguments, Is.EqualTo(new[] { "no browser", offer.PageUrl }));
    }
}

/// <summary>The version of a release tag and of the exe, and which one is newer.</summary>
[TestFixture]
public sealed class AppVersionTests
{
    [TestCase("0.1.3", 0, 1, 3)]
    [TestCase("v0.1.3", 0, 1, 3)]
    [TestCase("V1.20.300", 1, 20, 300)]
    [TestCase("0.1.3.0", 0, 1, 3)]
    [TestCase("0.1.3+2f1c9e", 0, 1, 3)]
    [TestCase(" v2.0.0 ", 2, 0, 0)]
    public void AReleaseOrExeVersion_IsRead(string text, int major, int minor, int patch)
    {
        Assert.That(AppVersion.TryParse(text, out var version), Is.True);
        Assert.That(version, Is.EqualTo(new AppVersion(major, minor, patch)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("v")]
    [TestCase("0.1")]
    [TestCase("0.1.3.1")]
    [TestCase("0.1.3-beta")]
    [TestCase("0.1.x")]
    [TestCase("0..3")]
    [TestCase("+0.1.3")]
    [TestCase("0.-1.3")]
    [TestCase("99999999999.0.0")]
    public void AnythingElse_IsNotAVersion(string? text)
    {
        Assert.That(AppVersion.TryParse(text, out _), Is.False);
    }

    [TestCase("0.1.10", "0.1.9")]
    [TestCase("0.2.0", "0.1.99")]
    [TestCase("1.0.0", "0.99.99")]
    public void TheOrderIsByNumber_NotByText(string newer, string older)
    {
        Assert.That(AppVersion.TryParse(newer, out var a), Is.True);
        Assert.That(AppVersion.TryParse(older, out var b), Is.True);

        Assert.That(a > b, Is.True);
        Assert.That(b < a, Is.True);
        Assert.That(a.ToString(), Is.EqualTo(newer));
    }
}
