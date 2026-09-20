using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.UnitTests.Architecture;

/// <summary>
/// ADR 0001 (alternative A, one project per layer) kept by tests that read the real project files and sources: the reference
/// direction, the two host-free projects staying host-free, Presentation never touching Infrastructure, and every interactor being
/// reached through its interface. The compiler already refuses most of this; these tests are the second key, and they say which rule broke.
/// </summary>
[TestFixture]
public sealed class LayerTests
{
    private const string Prefix = "Paper.ScreenWizzard.";

    // What each src project may reference (ADR 0001, the layer table). Anything else, and any cycle, is a violation.
    private static readonly Dictionary<string, string[]> _allowedReferences = new()
    {
        ["Domain"] = [],
        ["UseCases"] = ["Domain"],
        ["Infrastructure"] = ["UseCases", "Domain"],
        ["Presentation"] = ["UseCases", "Domain"],
        ["App"] = ["Presentation", "Infrastructure", "UseCases", "Domain"],
    };

    private static string Root { get; } = FindRepositoryRoot();

    [TestCaseSource(nameof(Layers))]
    public void EachProject_ReferencesOnlyTheLayersItMay(string layer)
    {
        var references = ProjectReferencesOf(layer);

        var forbidden = references.Where(r => !_allowedReferences[layer].Contains(r)).ToArray();

        Assert.That(forbidden, Is.Empty, $"{Prefix}{layer} references {string.Join(", ", forbidden)}, which ADR 0001's layer table does not allow");
    }

    [Test]
    public void EveryLayerOfTheAdr_IsItsOwnProject()
    {
        // Alternative A: each row of the layer table is a project. Presentation as a folder inside the exe was the exception nobody approved.
        foreach (var layer in _allowedReferences.Keys)
        {
            Assert.That(File.Exists(ProjectFile(layer)), Is.True, $"the project {Prefix}{layer} is missing");
        }
    }

    [TestCase("Domain")]
    [TestCase("UseCases")]
    public void DecisionLayers_TargetPlainNet_SoTheCompilerRefusesWindows(string layer)
    {
        var project = XDocument.Load(ProjectFile(layer));
        var target = project.Descendants("TargetFramework").Single().Value;

        Assert.That(target, Does.Not.Contain("windows"), $"{Prefix}{layer} must target plain net10.0 (ADR 0001 decision 4)");
        Assert.That(project.Descendants("UseWPF").Any(e => string.Equals(e.Value, "true", StringComparison.OrdinalIgnoreCase)), Is.False, $"{Prefix}{layer} must not use WPF");
        Assert.That(project.Descendants("UseWindowsForms").Any(), Is.False, $"{Prefix}{layer} must not use Windows Forms");
    }

    [TestCase("Domain")]
    [TestCase("UseCases")]
    public void DecisionLayers_NameNoHostNamespace(string layer)
    {
        var offenders = SourcesOf(layer)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\b(System\.Windows|System\.Drawing|Microsoft\.Win32|System\.Runtime\.InteropServices)\b"))
            .Select(f => Path.GetRelativePath(Root, f))
            .ToArray();

        Assert.That(offenders, Is.Empty, "host namespaces in a layer that must not see the host");
    }

    [Test]
    public void Presentation_HasNoTraceOfInfrastructure_InCodeOrXaml()
    {
        // A view model calling an adapter is the mistake this layer split exists to stop: the project reference is missing, and no file
        // may name the namespace either (a copy of an adapter's type would defeat the compiler).
        var offenders = SourcesOf("Presentation", "*.cs", "*.xaml")
            .Where(f => File.ReadAllText(f).Contains(Prefix + "Infrastructure", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(Root, f))
            .ToArray();

        Assert.That(offenders, Is.Empty, "Presentation names the Infrastructure namespace");
    }

    [Test]
    public void EveryInteractor_IsReachedThroughItsInterface_AndTheInterfaceLivesInPorts()
    {
        var assembly = typeof(ICaptureInteractor).Assembly;

        var interactors = assembly.GetExportedTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Interactor", StringComparison.Ordinal)).ToArray();

        Assert.That(interactors, Has.Length.EqualTo(3), "the shell, the capture and the editor interactor");
        foreach (var interactor in interactors)
        {
            var contract = interactor.GetInterfaces().SingleOrDefault(i => i.Name == "I" + interactor.Name);
            Assert.That(contract, Is.Not.Null, $"{interactor.Name} must implement I{interactor.Name}");
            Assert.That(contract!.Namespace, Does.EndWith(".Ports"), $"{contract.Name} belongs with the ports of its feature");
        }
    }

    [Test]
    public void NoSourceProject_ReferencesATestProject()
    {
        foreach (var layer in _allowedReferences.Keys)
        {
            var references = XDocument.Load(ProjectFile(layer)).Descendants("ProjectReference").Select(r => (string?)r.Attribute("Include") ?? string.Empty);
            Assert.That(references.Any(r => r.Contains("Tests", StringComparison.Ordinal)), Is.False, $"{Prefix}{layer} references a test project");
        }
    }

    public static IEnumerable<string> Layers() => _allowedReferences.Keys;

    private static string ProjectFile(string layer) => Path.Combine(Root, "src", Prefix + layer, Prefix + layer + ".csproj");

    private static string[] ProjectReferencesOf(string layer) =>
        XDocument.Load(ProjectFile(layer))
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension((string?)r.Attribute("Include") ?? string.Empty))
            .Select(name => name.StartsWith(Prefix, StringComparison.Ordinal) ? name[Prefix.Length..] : name)
            .ToArray();

    private static IEnumerable<string> SourcesOf(string layer, params string[] patterns)
    {
        var folder = Path.Combine(Root, "src", Prefix + layer);
        return (patterns.Length == 0 ? ["*.cs"] : patterns)
            .SelectMany(p => Directory.EnumerateFiles(folder, p, SearchOption.AllDirectories))
            .Where(f => !f.Contains(@"\obj\", StringComparison.Ordinal) && !f.Contains(@"\bin\", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Paper.ScreenWizzard.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Paper.ScreenWizzard.slnx not found above " + AppContext.BaseDirectory);
    }
}
