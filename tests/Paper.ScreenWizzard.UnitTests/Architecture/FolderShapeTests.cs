using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Paper.ScreenWizzard.UnitTests.Architecture;

/// <summary>
/// ADR 0003: inside every layer project the first-level folders are domains (the same names in every layer) and the roles (Ports,
/// UseCases, Models, ViewModels, Views, Commands) are the second level; a port sits in the domain whose core calls it, in Shared when two
/// domains do; the entry host has no folder of its own. The tests read the folders and sources as text, so they run without Windows.
/// Every test first checks that the list it walks is not empty: a renamed folder must fail here, not pass by checking nothing.
/// </summary>
[TestFixture]
public sealed class FolderShapeTests
{
    private const string Prefix = "Paper.ScreenWizzard.";

    private static readonly string[] _roleFolders = ["Ports", "UseCases", "Models", "Implements", "Services", "Adapters", "Helpers", "Utilities"];

    // A first-level folder that is not a domain, and why. Anything else must name a domain of the Domain project.
    private static readonly Dictionary<string, string> _notADomain = new()
    {
        ["Mvvm"] = "Presentation: the project's own MVVM base, named by CLAUDE.md",
        ["Resources"] = "Presentation: XAML dictionaries addressed by pack URI",
    };

    // The only first-level folders the entry host may have. It has none today: its files sit at the project root.
    private static readonly Dictionary<string, string> _outerAllowed = new()
    {
        ["Commands"] = "types the host calls by name",
        ["Properties"] = "assembly attributes",
    };

    private static readonly Regex _notAContract = new(
        @"^\s*(?:(?:public|internal|sealed|static|abstract|partial|readonly)\s+)*(?:class|record|struct|enum)\s+\w",
        RegexOptions.Multiline);

    private static readonly Regex _interfaceDeclaration = new(@"^\s*(?:(?:public|internal|partial)\s+)*interface\s+(\w+)", RegexOptions.Multiline);

    private static readonly string _root = FindRepositoryRoot();

    [TestCase("Domain")]
    [TestCase("UseCases")]
    public void DecisionLayers_KeepNoRoleFolderAtTheirTop(string layer)
    {
        var folders = FirstLevelFolders(layer);

        var roles = folders.Where(f => _roleFolders.Contains(f, StringComparer.OrdinalIgnoreCase)).ToArray();

        Assert.That(folders, Is.Not.Empty, $"{Prefix}{layer} has no folders at all: is it where the test thinks it is?");
        Assert.That(roles, Is.Empty, $"{Prefix}{layer}: the first level is a domain; move each file into <Domain>/Ports|UseCases|Models");
    }

    [TestCase("UseCases")]
    [TestCase("Infrastructure")]
    [TestCase("Presentation")]
    public void EveryLayerFolder_NamesADomainOfTheDomainProject(string layer)
    {
        var domains = FirstLevelFolders("Domain");

        var strangers = FirstLevelFolders(layer).Where(f => !domains.Contains(f) && !_notADomain.ContainsKey(f)).ToArray();

        Assert.That(domains, Is.Not.Empty, "the Domain project has no domain folders");
        Assert.That(strangers, Is.Empty,
            $"{Prefix}{layer}: name the folder after a domain of {Prefix}Domain (adapters and views sit straight in it), or add it to _notADomain with its reason");
    }

    [Test]
    public void TheEntryHost_StaysThin()
    {
        Assert.That(File.Exists(ProjectFile("App")), Is.True, "the entry host project is missing");

        var folders = FirstLevelFolders("App").Where(f => !_outerAllowed.ContainsKey(f)).ToArray();

        Assert.That(folders, Is.Empty,
            "the entry host only composes: its files sit at the project root. Move a decision to UseCases/<Domain>/UseCases, a host call to Infrastructure/<Domain>, text and UI to Presentation");
    }

    [Test]
    public void NoFolder_AnywhereInSrc_IsARoleFolderForImplementations()
    {
        var offenders = Directory.EnumerateDirectories(Path.Combine(_root, "src"), "*", SearchOption.AllDirectories)
            .Where(d => !IsBuildOutput(d))
            .Where(d => Path.GetFileName(d) is "Services" or "Adapters" or "Helpers" or "Utilities" or "Implements" or "Startup")
            .Select(d => Path.GetRelativePath(_root, d))
            .ToArray();

        Assert.That(offenders, Is.Empty, "an implementation sits straight in its domain's folder, not in a folder named after its role");
    }

    [Test]
    public void EveryPortsFolder_HoldsOneInterfacePerFile_NamedAfterIt()
    {
        var ports = PortFiles();
        var problems = new List<string>();
        foreach (var file in ports)
        {
            var text = CodeLines(File.ReadAllText(file));
            var interfaces = _interfaceDeclaration.Matches(text).Select(m => m.Groups[1].Value).ToArray();
            if (_notAContract.IsMatch(text))
            {
                problems.Add($"{Relative(file)} declares a class, record, struct or enum: those go to Models/ or UseCases/");
            }

            if (interfaces.Length != 1 || interfaces[0] != Path.GetFileNameWithoutExtension(file))
            {
                problems.Add($"{Relative(file)} declares {string.Join(", ", interfaces)}: one interface, and the file is named after it");
            }
        }

        Assert.That(ports, Is.Not.Empty, "no Ports folder found under src");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    [Test]
    public void NoPort_IsNamedPort()
    {
        var names = PortFiles().SelectMany(f => _interfaceDeclaration.Matches(CodeLines(File.ReadAllText(f))).Select(m => m.Groups[1].Value)).ToArray();

        var offenders = names.Where(n => n.EndsWith("Port", StringComparison.Ordinal)).ToArray();

        Assert.That(names, Is.Not.Empty, "no interface found in a Ports folder");
        Assert.That(offenders, Is.Empty, "a port is named after what it provides (IClock), not after its role (IClockPort)");
    }

    [Test]
    public void EveryInteractor_HasItsInterfaceInItsDomainsPorts()
    {
        var interactors = SourcesOf("UseCases")
            .Where(f => Path.GetFileName(f).EndsWith("Interactor.cs", StringComparison.Ordinal) && !Path.GetFileName(f).StartsWith("I", StringComparison.Ordinal))
            .ToArray();

        var missing = interactors
            .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) != "UseCases"
                || !File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(f)!)!, "Ports", "I" + Path.GetFileName(f))))
            .Select(Relative)
            .ToArray();

        Assert.That(interactors, Has.Length.EqualTo(3), "the shell, the capture and the editor interactor");
        Assert.That(missing, Is.Empty, "put <Domain>/UseCases/<X>Interactor.cs beside <Domain>/Ports/I<X>Interactor.cs");
    }

    [Test]
    public void EveryPort_SitsInTheDomainWhoseCoreCallsIt()
    {
        var cores = SourcesOf("UseCases")
            .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "UseCases")
            .Select(f => (Domain: DomainOf("UseCases", f), Text: File.ReadAllText(f)))
            .ToArray();
        var problems = new List<string>();
        foreach (var port in PortFiles())
        {
            var name = Path.GetFileNameWithoutExtension(port);
            var home = DomainOf("UseCases", port);
            var callers = cores.Where(c => Regex.IsMatch(c.Text, $@"\b{name}\b")).Select(c => c.Domain).Distinct().ToArray();
            if (home != "Shared" && callers.Any(c => c != home))
            {
                problems.Add($"{name} sits in {home} but the {string.Join(", ", callers)} core calls it: move it to Shared/Ports");
            }

            if (home == "Shared" && callers.Length == 1)
            {
                problems.Add($"{name} sits in Shared but only the {callers[0]} core calls it: move it to {callers[0]}/Ports");
            }
        }

        Assert.That(cores, Is.Not.Empty, "no <Domain>/UseCases folder found");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    [Test]
    public void EveryAdapter_SitsInItsPortsDomain()
    {
        var ports = PortFiles().ToDictionary(f => Path.GetFileNameWithoutExtension(f)!, f => DomainOf("UseCases", f));
        var adapters = SourcesOf("Infrastructure")
            .Select(f => (File: f, Implements: Regex.Matches(File.ReadAllText(f), @"class\s+\w+\s*:\s*([^{\n]+)")
                .SelectMany(m => m.Groups[1].Value.Split(','))
                .Select(i => i.Trim())
                .Where(ports.ContainsKey)
                .ToArray()))
            .Where(a => a.Implements.Length > 0)
            .ToArray();

        var misplaced = adapters
            .SelectMany(a => a.Implements.Where(i => ports[i] != DomainOf("Infrastructure", a.File)).Select(i => $"{Relative(a.File)} implements {i} of {ports[i]}"))
            .ToArray();

        Assert.That(adapters, Is.Not.Empty, "no Infrastructure class implements a port");
        Assert.That(misplaced, Is.Empty, "an adapter sits in Infrastructure/<the domain of its port>");
    }

    private static string[] FirstLevelFolders(string layer) =>
        Directory.EnumerateDirectories(Path.Combine(_root, "src", Prefix + layer))
            .Select(d => Path.GetFileName(d)!)
            .Where(n => n is not ("bin" or "obj") && !n.StartsWith('.'))
            .ToArray();

    private static string[] PortFiles() =>
        SourcesOf("UseCases").Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "Ports").ToArray();

    // The first folder under the layer's project: src/<Prefix><layer>/<Domain>/...
    private static string DomainOf(string layer, string file) =>
        Path.GetRelativePath(Path.Combine(_root, "src", Prefix + layer), file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

    private static IEnumerable<string> SourcesOf(string layer) =>
        Directory.EnumerateFiles(Path.Combine(_root, "src", Prefix + layer), "*.cs", SearchOption.AllDirectories).Where(f => !IsBuildOutput(f));

    private static bool IsBuildOutput(string path) =>
        Regex.IsMatch(path.Replace('\\', '/'), @"/(bin|obj)(/|$)");

    private static string ProjectFile(string layer) => Path.Combine(_root, "src", Prefix + layer, Prefix + layer + ".csproj");

    private static string Relative(string path) => Path.GetRelativePath(_root, path).Replace('\\', '/');

    // A type named in a comment is not a declaration.
    private static string CodeLines(string text) => string.Join("\n", text.Split('\n').Where(line =>
    {
        var trimmed = line.TrimStart();
        return !trimmed.StartsWith("//", StringComparison.Ordinal) && !trimmed.StartsWith('*') && !trimmed.StartsWith("/*", StringComparison.Ordinal);
    }));

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
