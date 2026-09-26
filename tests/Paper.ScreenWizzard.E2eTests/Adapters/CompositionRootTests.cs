using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Paper.ScreenWizzard.App.Startup;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Shell;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Editor.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// The wiring of the exe without running it: the container of <see cref="CompositionRoot"/> is built with a throw-away data folder and a
/// throw-away instance name (so nothing of the real app is touched), and every port, interactor and flow is asked for. Nothing is started:
/// no window, no tray icon, no hotkey is registered and no mutex is taken, because those happen in <c>AppShell.Start</c>.
/// </summary>
[TestFixture]
public sealed class CompositionRootTests
{
    [Test]
    public void EveryPortInteractorAndFlow_IsResolvableFromTheContainer()
    {
        using var scratch = new ScratchFolder();
        var options = new StartupOptions(scratch.Path, "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N"), false);

        // The container is built and used on the UI thread, like the exe: the hotkey window and the presenters belong to it.
        StaHost.Instance.Invoke(() =>
        {
            using var provider = CompositionRoot.Build(options, () => { });

            Assert.That(provider.GetRequiredService<IShellInteractor>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<ICaptureInteractor>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IEditorInteractor>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IScreenSourcePort>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IWindowCatalogPort>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IMonitorCatalogPort>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IClipboardPort>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IAutostartPort>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<CaptureFlow>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<EditorFlow>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IHotkeyPort>(), Is.SameAs(provider.GetRequiredService<HotkeyService>()), "one hotkey service, so the container disposes the one that is used");
            Assert.That(provider.GetRequiredService<ISingleInstancePort>(), Is.SameAs(provider.GetRequiredService<SingleInstance>()));
            Assert.That(provider.GetRequiredService<AppShell>(), Is.Not.Null, "the shell that connects them all");
        });
        Assert.That(Directory.Exists(Path.Combine(scratch.Path, "configs")), Is.False, "resolving the graph writes nothing");
    }

    [Test]
    public void StartupOptions_UseTheRealDefaultsWhenTheVariablesAreNotSet_AndTheVariablesWhenTheyAre()
    {
        var defaults = StartupOptions.From([], _ => null);
        var overridden = StartupOptions.From(["--autostart"], name => name switch
        {
            StartupOptions.DataVariable => @"C:\Temp\wizzard-data",
            StartupOptions.InstanceVariable => "Paper.ScreenWizzard.Test",
            _ => null,
        });

        Assert.That(defaults.DataRoot, Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Paper", "ScreenWizzard")));
        Assert.That(defaults.InstanceName, Is.EqualTo("Paper.ScreenWizzard"));
        Assert.That(defaults.Autostart, Is.False);
        Assert.That(overridden.DataRoot, Is.EqualTo(@"C:\Temp\wizzard-data"));
        Assert.That(overridden.InstanceName, Is.EqualTo("Paper.ScreenWizzard.Test"));
        Assert.That(overridden.Autostart, Is.True);
        Assert.That(StartupOptions.From([], name => name == StartupOptions.DataVariable ? "  " : null).DataRoot, Is.EqualTo(defaults.DataRoot), "a blank variable is not a folder");
    }
}
