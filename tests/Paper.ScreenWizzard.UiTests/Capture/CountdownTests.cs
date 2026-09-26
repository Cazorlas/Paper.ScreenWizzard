using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;
using Paper.ScreenWizzard.Presentation.Capture.Views;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>SPEC capture, "Độ trễ và con trỏ": with a 5 s delay the number shows 5, 4, 3, 2, 1 (fed by IProgress from the use case).</summary>
[TestFixture]
public sealed class CountdownTests : UiTestBase
{
    [Test]
    public void ViewModel_EachReport_ShowsTheSecondsLeftAsText()
    {
        var viewModel = new CountdownViewModel();
        var seen = new List<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CountdownViewModel.Text))
            {
                seen.Add(viewModel.Text);
            }
        };

        foreach (var second in new[] { 5, 4, 3, 2, 1 })
        {
            viewModel.SecondsLeft = second;
        }

        Assert.That(seen, Is.EqualTo(new[] { "5", "4", "3", "2", "1" }));
    }

    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Light)]
    [TestCase(ResolvedLanguage.English, AppTheme.Dark)]
    public void Window_ShowsFiveFourThreeTwoOne_AlwaysOnTop(ResolvedLanguage language, AppTheme theme)
    {
        var viewModel = WpfHost.Instance.Invoke(() => new CountdownViewModel { SecondsLeft = 5 });
        using var session = WpfHost.Instance.Show(() => new CountdownWindow(viewModel), language, theme);

        Assert.That(session.Root.AutomationId, Is.EqualTo("CountdownWindow"));
        foreach (var second in new[] { 5, 4, 3, 2, 1 })
        {
            WpfHost.Instance.Invoke(() => viewModel.SecondsLeft = second);
            WpfHost.Instance.Settle();

            Assert.That(session.TextOf("CountdownText"), Is.EqualTo(second.ToString()));
            if (second == 5)
            {
                session.Screenshot($"capture-countdown-{theme.ToString().ToLowerInvariant()}");
            }
        }

        var (topmost, inTaskbar) = WpfHost.Instance.Invoke(() =>
        {
            var bare = new CountdownWindow(new CountdownViewModel());
            return (bare.Topmost, bare.ShowInTaskbar);
        });
        Assert.That(topmost, Is.True, "the number must stay visible above the app the user is preparing");
        Assert.That(inTaskbar, Is.False);
    }

    [Test]
    public void Window_NamesItselfForAScreenReader()
    {
        using var session = WpfHost.Instance.Show(() => new CountdownWindow(new CountdownViewModel { SecondsLeft = 3 }));

        Assert.That(session.Root.Name, Is.Not.Empty.And.Not.StartWith("Capture."));
        Assert.That(session.NameOf("CountdownText"), Is.EqualTo("3"), "the number is what a screen reader reads");
    }
}
