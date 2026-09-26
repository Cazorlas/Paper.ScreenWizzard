# Test kiến trúc C# (NUnit), đọc file như văn bản

Hook chặn lúc sửa; các test này chặn lúc build — kể cả file ghi bằng lệnh shell mà hook không thấy. Chúng
không nạp assembly nào: đọc `.sln`, `.csproj` và `.cs` như văn bản, nên chạy được trong project unit test
bình thường, không cần host.

Tên dưới đây là chỗ đứng: `MyApp.*` là project của dự án, `Vendor.HostApi` là namespace của host, `Vendor`
là tiền tố package của host. Danh sách thật lấy từ `architecture` trong `.claude/paper.profile.json`, để
hook và test cấm cùng một thứ.

## 1. Đọc repository

```csharp
internal static class Repository
{
    public static readonly string Root = FindRoot();
    public static readonly string SolutionPath = Directory.GetFiles(Root, "*.sln").Single();

    public sealed record Project(string Name, string Path)
    {
        public string Directory => System.IO.Path.GetDirectoryName(Path)!;
    }

    // Lines: Project("{type-guid}") = "Name", "relative\path.csproj", "{guid}"
    public static readonly IReadOnlyList<Project> Projects = Regex
        .Matches(File.ReadAllText(SolutionPath),
            @"^Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)"",\s*""([^""]+\.csproj)""", RegexOptions.Multiline)
        .Select(m => new Project(m.Groups[1].Value, Path.GetFullPath(Path.Combine(Root, m.Groups[2].Value))))
        .ToList();

    public static IEnumerable<string> ReferencesOf(Project project) => Regex
        .Matches(File.ReadAllText(project.Path), @"<ProjectReference\s+Include=""([^""]+)""")
        .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')));

    public static IEnumerable<string> SourceFiles => Directory
        .EnumerateFiles(Root, "*.cs", SearchOption.AllDirectories)
        .Where(p => !Regex.IsMatch(p, @"[\\/](bin|obj)[\\/]"));

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        // Walk up from the test binary to the folder holding the solution.
        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
        throw new InvalidOperationException("no .sln above " + TestContext.CurrentContext.TestDirectory);
    }
}
```

Trên .NET Framework không có `Path.GetRelativePath`: cắt `Root` khỏi đầu đường dẫn.

## 2. Mỗi project có tầng, và chỉ tham chiếu xuống

Bảng tầng **liệt kê tường minh**, không suy từ tên: project mới chưa được xếp thì test đỏ, buộc ai đó quyết
định nó thuộc đâu — đó chính là cơ chế. Bảng này và bảng tầng trong `CLAUDE.md` là cùng một danh sách.

**Phương án A — tầng là project.** Mỗi tầng một tập được phép tham chiếu:

```csharp
private enum Layer { Host, Presentation, Infrastructure, UseCases, Domain, Test }

private static readonly Dictionary<string, Layer> Layers = new(StringComparer.OrdinalIgnoreCase)
{
    ["MyApp"] = Layer.Host,
    ["MyApp.Presentation"] = Layer.Presentation,
    ["MyApp.Infrastructure"] = Layer.Infrastructure,
    ["MyApp.UseCases"] = Layer.UseCases,
    ["MyApp.Domain"] = Layer.Domain,
    ["MyApp.Tests"] = Layer.Test,
};

private static readonly Dictionary<Layer, Layer[]> Allowed = new()
{
    [Layer.Host] = new[] { Layer.Presentation, Layer.Infrastructure, Layer.UseCases, Layer.Domain },
    [Layer.Presentation] = new[] { Layer.UseCases, Layer.Domain },
    [Layer.Infrastructure] = new[] { Layer.UseCases, Layer.Domain },
    [Layer.UseCases] = new[] { Layer.Domain },
    [Layer.Domain] = Array.Empty<Layer>(),
};

[Test]
public void TheSolution_IsWhereWeThinkItIs() =>
    // Without this, every test below can pass by checking an empty list.
    Assert.That(Repository.Projects, Is.Not.Empty, Repository.SolutionPath);

[Test]
public void EveryProject_HasALayer()
{
    var unclassified = Repository.Projects.Select(p => p.Name).Where(n => !Layers.ContainsKey(n)).ToArray();
    Assert.That(unclassified, Is.Empty, "Add to Layers and to the layer table in CLAUDE.md: " + string.Join(", ", unclassified));
}

[Test]
public void TheLayerTable_HasNoProjectTheSolutionDropped()
{
    var present = Repository.Projects.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert.That(Layers.Keys.Where(n => !present.Contains(n)), Is.Empty);
}

[Test]
public void EveryReference_PointsDownward()
{
    var problems = new List<string>();
    foreach (var project in Repository.Projects)
    {
        if (!Layers.TryGetValue(project.Name, out var from) || from == Layer.Test) continue;
        foreach (var name in Repository.ReferencesOf(project))
        {
            if (!Layers.TryGetValue(name, out var to)) continue;
            if (!Allowed[from].Contains(to)) problems.Add($"{project.Name} ({from}) references {name} ({to})");
        }
    }
    Assert.That(problems, Is.Empty, string.Join("\n  ", problems));
}
```

**Phương án B — tầng là thư mục.** Tầng của **project** là hạng: entry host, module, phần dùng chung,
thư viện. So `to >= from` là đủ, vì trong module không còn tầng project nào:

```csharp
private const int Host = 1, Module = 2, Shared = 3, Library = 4, Test = 90;

private static readonly Dictionary<string, int> Ranks = new(StringComparer.OrdinalIgnoreCase)
{
    ["MyApp"] = Host,
    ["MyApp.Orders"] = Module,       // a feature module: Domain/, Infrastructure/, Presentation/ split by domain, Commands/
    ["MyApp.Billing"] = Module,
    ["MyApp.Pricing.Core"] = Shared, // host-free logic two modules need
    ["MyApp.Common"] = Library,
    ["MyApp.Tests"] = Test,
};
// EveryProject_HasALayer and TheLayerTable_HasNoProjectTheSolutionDropped as in option A, over Ranks.

[Test]
public void NoReference_PointsUpwards()
{
    var problems = new List<string>();
    foreach (var project in Repository.Projects)
    {
        if (!Ranks.TryGetValue(project.Name, out int from) || from == Test) continue;
        foreach (var name in Repository.ReferencesOf(project))
        {
            if (!Ranks.TryGetValue(name, out int to)) continue;
            if (to == Test) problems.Add($"{project.Name} references the test project {name}");
            else if (to < from) problems.Add($"{project.Name} references {name}, a layer above it");
        }
    }
    Assert.That(problems, Is.Empty, string.Join("\n  ", problems));
}
```

Để hạng của test ở 90 chứ không ở 0: phép so `to >= from` sẽ ngược nếu đánh số theo thứ tự hiển thị.

## 3. Không module nào tham chiếu module khác

Hai module biết nhau thì không ship, bật tắt hay gỡ riêng được — mất đúng lý do có module. Tham chiếu cùng
hạng giữa các project dùng chung thì được; giữa các module thì không.

```csharp
[Test]
public void NoModule_ReferencesAnotherModule()
{
    var problems = Repository.Projects
        .Where(p => Ranks.TryGetValue(p.Name, out int r) && r == Module)
        .SelectMany(p => Repository.ReferencesOf(p)
            .Where(name => Ranks.TryGetValue(name, out int r) && r == Module)
            .Select(name => $"{p.Name} references the module {name}"))
        .ToArray();
    Assert.That(problems, Is.Empty, string.Join("\n  ", problems));
}
```

Một module cố ý trải nhiều project thì khai nhóm đó, cho các thành viên tham chiếu nhau, và thêm một test
rằng **không gì ngoài nhóm** (trừ entry host và test) tham chiếu vào — nếu không, ngoại lệ đã tắt luật này
cho cả solution.

## 4. Thư mục không dùng host không nhắc namespace nào của host

Với phương án B đây là thứ thay compiler. Với phương án A, dùng cùng test cho project Domain và UseCases để
bắt cả tên đầy đủ trong code.

```csharp
// using Vendor.HostApi...; using static ...; using alias = Vendor.HostApi...; and a qualified Vendor.HostApi.X.
private static readonly Regex HostName = new(
    @"^\s*using\s+(?:static\s+)?(?:\w+\s*=\s*)?(Vendor\.HostApi|System\.Windows)\b|\bVendor\.HostApi\.[A-Z]",
    RegexOptions.Multiline);

// Option A: every file of a *.Domain project. Option B: the Domain/ folder of a module. And UseCases/
// anywhere, so a module still in the old UseCases/<Feature>/ tree stays guarded. Same list as the profile.
private static readonly Regex PlatformFreePath = new(@"(\.Domain|/Domain|/UseCases)/", RegexOptions.IgnoreCase);

private static IEnumerable<string> PlatformFreeFiles() => Repository.SourceFiles
    .Where(p => PlatformFreePath.IsMatch(p.Replace('\\', '/')));

[Test]
public void PlatformFreeFolders_Exist() =>
    // A renamed folder would leave the next test checking nothing, and passing.
    Assert.That(PlatformFreeFiles(), Is.Not.Empty);

[Test]
public void NoPlatformFreeFile_NamesTheHost()
{
    var offenders = PlatformFreeFiles()
        .Where(p => HostName.IsMatch(CodeLines(File.ReadAllText(p))))
        .Select(Repository.Relative)
        .ToArray();
    Assert.That(offenders, Is.Empty, "Move the host call into an adapter behind a port: " + string.Join(", ", offenders));
}

// A host type named in a comment is the opposite of a dependency - drop comment lines before matching.
private static string CodeLines(string text) => string.Join("\n", text.Split('\n').Where(line =>
{
    var t = line.TrimStart();
    return !t.StartsWith("//") && !t.StartsWith("*") && !t.StartsWith("/*");
}));
```

Giới hạn cần ghi trong ADR: kiểu host đi vào qua `var` từ một port rò host thì không có tên nào để bắt. Agent
review kiến trúc là lớp kiểm thứ hai.

## 5. Interactor lộ ra bằng interface, trong `Ports/` của domain nó

Lệnh và view model phụ thuộc `I<X>Interactor`, không phụ thuộc class — để test thay được và để lệnh
không lớn dần thành chỗ quyết định. **`Ports/` được chia theo domain, không phẳng: test quét mọi thư mục
`Ports/` ở mọi độ sâu.** Một test chỉ quét cấp đầu của một `Ports/` duy nhất sẽ ép mọi port về một chỗ.

```csharp
// Every file whose own folder is a role folder of that name, at any depth.
static IEnumerable<string> FilesInRole(string role) => Repository.SourceFiles
    .Where(p => string.Equals(Path.GetFileName(Path.GetDirectoryName(p)), role, StringComparison.OrdinalIgnoreCase));

[Test]
public void PortsFolders_Exist() =>
    Assert.That(FilesInRole("Ports"), Is.Not.Empty);

// Only contracts: the name starts with I, and nothing but an interface is declared.
private static readonly Regex NotAContract = new(
    @"^\s*(?:(?:public|internal|sealed|static|abstract|partial)\s+)*(?:class|record|struct|enum)\s+\w",
    RegexOptions.Multiline);

[Test]
public void EveryPortsFolder_HoldsOnlyInterfaces()
{
    var offenders = FilesInRole("Ports")
        .Where(p => !Regex.IsMatch(Path.GetFileName(p), @"^I[A-Z]") || NotAContract.IsMatch(CodeLines(File.ReadAllText(p))))
        .Select(Repository.Relative)
        .ToArray();
    Assert.That(offenders, Is.Empty,
        "Ports/ holds interfaces only; move records to Models/, classes to UseCases/: " + string.Join(", ", offenders));
}

// <D>/UseCases/<X>Interactor.cs needs <D>/Ports/I<X>Interactor.cs. A domain without an interactor needs nothing.
// Implements/ is the old name of UseCases/ - kept so a module not migrated yet is still checked.
[Test]
public void EveryInteractor_HasItsInterfaceInItsDomainsPorts()
{
    var missing = FilesInRole("UseCases").Concat(FilesInRole("Implements"))
        .Where(p => Path.GetFileName(p).EndsWith("Interactor.cs", StringComparison.Ordinal))
        .Where(p =>
        {
            var domain = Path.GetDirectoryName(Path.GetDirectoryName(p))!;
            return !File.Exists(Path.Combine(domain, "Ports", "I" + Path.GetFileName(p)));
        })
        .Select(Repository.Relative)
        .ToArray();
    Assert.That(missing, Is.Empty, "Add <Domain>/Ports/I<X>Interactor.cs for: " + string.Join(", ", missing));
}
```

Một test cần danh sách port (ví dụ: bên cài một port mang tên port đó) lấy nó từ `FilesInRole("Ports")`,
không từ một thư mục cố định.

**Tên thư mục là `Ports/`, không phải `Services/`: port là hợp đồng, service là bên tuân thủ nó.** Gần như repo
nào cũng đã có một thư mục `Services/` chứa bên cài đặt, nên hai thư mục cùng tên mang hai vai ngược nhau, và
đọc đường dẫn không biết mình đang ở bên nào. Đổi tên rồi thì một test giữ cho tên cũ không quay lại ở tầng
quyết định — nếu không, nửa cây code sẽ dừng ở tên cũ:

```csharp
[Test]
public void NoDomain_KeepsItsContractsInAServicesFolder()
{
    var offenders = PlatformFreeFiles()
        .Select(p => Path.GetDirectoryName(p)!)
        .Where(d => string.Equals(Path.GetFileName(d), "Services", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(Repository.Relative)
        .ToArray();
    Assert.That(offenders, Is.Empty,
        "Ports live in <Domain>/Ports/; what implements one is a service, in the outer layer: " + string.Join(", ", offenders));
}
```

## 6. Cấp một của mỗi tầng là domain

Trong mỗi tầng, thư mục cấp một là domain; vai (`Ports/`, `UseCases/`, `Models/`) là cấp hai; tầng ngoài dùng
lại đúng tên domain. Viết cho phương án A (`<M>.Domain`, `<M>.Infrastructure`, `<M>.Presentation`); phương án
B thay ba project đó bằng ba thư mục `Domain/`, `Infrastructure/`, `Presentation/` của mỗi module. Project
entry host / seam (`<M>`) không chia domain nên không nằm trong `EveryLayerFolder_NamesADomain`; nó có test
riêng `OuterProject_StaysThin`: cấp một chỉ là những thư mục có tên trong `OuterAllowed` — `Commands/`, thư
mục của assembly, thư mục cửa sổ host của màn hình — và file ở gốc là gốc ghép. Không thư mục vai
(`Services/`, `Helpers/`, `Utilities/`). Phương án B: gốc module là entry host, `OuterAllowed` thêm
`Domain`, `Infrastructure`, `Presentation`.

```csharp
// Named debt: a layer project not migrated yet, one line each, with the plan that migrates it.
// A module migrates by parity refactor (skill parity-refactor), layer by layer; its line goes when it has.
private static readonly HashSet<string> DomainDebt = new(StringComparer.OrdinalIgnoreCase)
{
    "MyApp.Billing.Domain",        // docs/features/billing/<date>-domain-folders-plan.md
    "MyApp.Billing.Infrastructure",
};

// A first-level folder that is not a domain, and why. Anything else must name a domain of <M>.Domain.
private static readonly Dictionary<string, string> NotADomain = new(StringComparer.OrdinalIgnoreCase)
{
    ["Shared"] = "what two or more domains need",
    ["Global"] = "polyfills and global usings",
    ["Properties"] = "assembly attributes",
    ["Shell"] = "Presentation: the window frame, a screen of no single domain",
};

private static readonly string[] RoleFolders = { "Ports", "UseCases", "Models", "Implements", "Services" };

private static IEnumerable<string> FirstLevelFolders(string dir) => Directory.EnumerateDirectories(dir)
    .Select(d => Path.GetFileName(d)!)
    .Where(n => n is not ("bin" or "obj") && !n.StartsWith("."));

private static IEnumerable<Repository.Project> DomainProjects() => Repository.Projects
    .Where(p => p.Name.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase));

[Test]
public void DomainProjects_Exist() => Assert.That(DomainProjects(), Is.Not.Empty);

[Test]
public void NoSplitDomain_KeepsARoleFolderAtItsTop()
{
    var offenders = DomainProjects()
        .Where(p => !DomainDebt.Contains(p.Name))
        .SelectMany(p => FirstLevelFolders(p.Directory)
            .Where(f => RoleFolders.Contains(f, StringComparer.OrdinalIgnoreCase))
            .Select(f => $"{p.Name}/{f}"))
        .ToArray();
    Assert.That(offenders, Is.Empty,
        "The first level is a domain: move each file into <Domain>/Ports|UseCases|Models, or list the project in DomainDebt: "
        + string.Join(", ", offenders));
}

[Test]
public void EveryLayerFolder_NamesADomain()
{
    var problems = new List<string>();
    foreach (var domain in DomainProjects())
    {
        var module = domain.Name.Substring(0, domain.Name.Length - ".Domain".Length);
        var domains = FirstLevelFolders(domain.Directory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in new[] { ".Infrastructure", ".Presentation" })
        {
            var project = Repository.Projects.SingleOrDefault(p => p.Name.Equals(module + layer, StringComparison.OrdinalIgnoreCase));
            if (project is null || DomainDebt.Contains(project.Name)) continue;
            problems.AddRange(FirstLevelFolders(project.Directory)
                .Where(f => !domains.Contains(f) && !NotADomain.ContainsKey(f))
                .Select(f => $"{project.Name}/{f}"));
        }
    }
    Assert.That(problems, Is.Empty,
        "Name the folder after a domain of the module's .Domain (adapters sit straight in it), or add it to NotADomain with its reason: "
        + string.Join(", ", problems));
}

// The outer project <M> only composes: composition files at its root, and only these first-level folders.
// In your project the window folder is whatever hosts the screen (a web page's window, a dockable pane).
private static readonly Dictionary<string, string> OuterAllowed = new(StringComparer.OrdinalIgnoreCase)
{
    ["Commands"] = "types the host calls by name - their namespace stays, the host's registration names it",
    ["Global"] = "polyfills and global usings",
    ["Properties"] = "assembly attributes",
    ["Web"] = "the screen's host window, when the screen is a web page",
};

[Test]
public void OuterProject_StaysThin()
{
    var outers = DomainProjects()
        .Select(d => d.Name.Substring(0, d.Name.Length - ".Domain".Length))
        .Select(m => Repository.Projects.SingleOrDefault(p => p.Name.Equals(m, StringComparison.OrdinalIgnoreCase)))
        .Where(p => p is not null && !DomainDebt.Contains(p.Name))
        .ToArray();
    Assert.That(outers, Is.Not.Empty, "sentinel: no outer project found next to a .Domain project");
    var problems = outers
        .SelectMany(p => FirstLevelFolders(p!.Directory)
            .Where(f => !OuterAllowed.ContainsKey(f))
            .Select(f => $"{p!.Name}/{f}"))
        .ToArray();
    Assert.That(problems, Is.Empty,
        "The outer project only composes - no role folder (Services/, Helpers/, Utilities/). Move a decision to "
        + "<M>.Domain/<Domain>/UseCases/, a host call to <M>.Infrastructure/<Domain>/, text and UI to <M>.Presentation; "
        + "composition files sit at the project root. Or add the folder to OuterAllowed with its reason: "
        + string.Join(", ", problems));
}
```

Một dòng trong `DomainDebt` là **nợ có tên**, không phải miễn trừ: nó nói project nào chưa chuyển và plan nào
chuyển nó. Chuyển xong thì xoá dòng — test đỏ nếu project vẫn còn thư mục vai ở cấp một. Entry host còn
`Services/` thì nằm cùng danh sách, tên project `<M>`, cho tới khi thư mục đó rỗng. Hướng giữa các thư mục
domain (domain chỉ nhìn `Shared/`) chưa có test ở đây; agent `architecture-reviewer` giữ nó.

## Đặt chúng ở đâu

- Một thư mục `Architecture/` trong project unit test, một category riêng để chạy nhanh.
- Mỗi test có một **test canh**: danh sách nó duyệt không rỗng. Test kiến trúc đỏ vì sai thì dễ thấy; xanh vì
  không duyệt gì thì không ai thấy.
- Thông báo lỗi nói **cách sửa** (xếp project vào tầng nào, dời lời gọi host ra adapter nào), không chỉ nói
  vi phạm.
