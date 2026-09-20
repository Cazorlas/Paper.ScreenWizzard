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
    ["MyApp.Orders"] = Module,       // a feature module: UseCases/, Adapters/, Commands/, ViewModels/ inside
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

private static IEnumerable<string> PlatformFreeFiles() => Repository.SourceFiles
    .Where(p => p.Replace('\\', '/').IndexOf("/UseCases/", StringComparison.OrdinalIgnoreCase) >= 0);

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

## 5. Interactor lộ ra bằng interface, trong `Ports/`

Lệnh và view model phụ thuộc `I<Feature>Interactor`, không phụ thuộc class — để test thay được và để lệnh
không lớn dần thành chỗ quyết định.

```csharp
static IEnumerable<string> FeatureFolders()
{
    const string marker = "/UseCases/";
    return PlatformFreeFiles()
        .Select(p => p.Replace('\\', '/'))
        .Select(p =>
        {
            int at = p.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length;
            return p.Substring(0, at) + p.Substring(at).Split('/')[0];   // .../UseCases/<Feature>
        })
        .Distinct(StringComparer.OrdinalIgnoreCase);
}

[Test]
public void EveryUseCase_ExposesItsInteractorAsAnInterface()
{
    var missing = FeatureFolders()
        .Where(feature => !Directory.Exists(Path.Combine(feature, "Ports"))
                          || !Directory.EnumerateFiles(Path.Combine(feature, "Ports"), "I*Interactor.cs").Any())
        .Select(Repository.Relative)
        .ToArray();
    Assert.That(missing, Is.Empty, "Each UseCases/<Feature> needs Ports/I<Feature>Interactor.cs: " + string.Join(", ", missing));
}
```

**Tên thư mục là `Ports/`, không phải `Services/`: port là hợp đồng, service là bên tuân thủ nó.** Gần như repo
nào cũng đã có một thư mục `Services/` chứa bên cài đặt, nên hai thư mục cùng tên mang hai vai ngược nhau, và
đọc đường dẫn không biết mình đang ở bên nào. Đổi tên rồi thì một test thứ hai giữ cho tên cũ không quay lại —
nếu không, nửa cây code sẽ dừng ở tên cũ:

```csharp
[Test]
public void NoUseCase_KeepsItsContractsInAServicesFolder()
{
    var offenders = FeatureFolders()
        .Where(feature => Directory.Exists(Path.Combine(feature, "Services")))
        .Select(Repository.Relative)
        .ToArray();
    Assert.That(offenders, Is.Empty,
        "Ports live in Ports/; what implements one is a service: " + string.Join(", ", offenders));
}
```

Một test thứ ba giữ `Ports/` sạch — chỉ hợp đồng, không lớp cài đặt, không record: mọi file trong đó tên bắt
đầu bằng `I`.

## Đặt chúng ở đâu

- Một thư mục `Architecture/` trong project unit test, một category riêng để chạy nhanh.
- Mỗi test có một **test canh**: danh sách nó duyệt không rỗng. Test kiến trúc đỏ vì sai thì dễ thấy; xanh vì
  không duyệt gì thì không ai thấy.
- Thông báo lỗi nói **cách sửa** (xếp project vào tầng nào, dời lời gọi host ra adapter nào), không chỉ nói
  vi phạm.
