# /qa static analysis

The static lane is one build of the project with analyzers plugged in and one read of the SARIF files
the compiler wrote, by `scripts/qa.ps1` (`qa.ps1 static -Run <run>`). No token is spent. The project's
files are never edited; the analyzer packages only land in the machine's NuGet cache.

## Profile keys

Everything is optional; `.claude/paper.profile.json`, key `qa`:

```json
{
  "qa": {
    "lanes": { "static": true, "security": true, "bug": true, "architecture": true, "smell": true, "ui": false },
    "exclude": ["**/Migrations/**"],
    "report": "docs/qa",
    "verifyCap": 10,
    "batchTokens": 120000,
    "ui": { "screens": "tests/**/baselines/*.png" },
    "staticAnalysis": {
      "analyzers": { "SonarAnalyzer.CSharp": "10.30.0.0", "Roslynator.Analyzers": "off" },
      "globalconfig": ".qa.globalconfig",
      "sarifDir": "artifacts/sarif",
      "kinds": { "CA1822": "ignore", "RCS1163": "bug" }
    }
  }
}
```

| Key | Default | Meaning |
| --- | --- | --- |
| `qa.lanes.<lane>` | from the hosts and the profile | true runs the lane whatever the hosts say (static still needs a .NET project and the build verb); false turns it off |
| `qa.exclude` | none | globs left out of the scope, reason `profile exclude` |
| `qa.report` | `qa` beside the featureDocs folder | report folder inside the repository |
| `qa.verifyCap` | 10 | findings a second reader verifies; 0 verifies none |
| `qa.batchTokens` | 120000 (at least 20000) | content tokens one agent is handed |
| `qa.ui.screens` | none | screenshots for the ui lane |
| `qa.staticAnalysis.analyzers` | Microsoft.CodeAnalysis.NetAnalyzers 10.0.401, Roslynator.Analyzers 5.0.0, SonarAnalyzer.CSharp 10.35.0.4138 | id to version or "off", merged over the defaults: pinning one package is one line, a new id adds an analyzer |
| `qa.staticAnalysis.globalconfig` | the kit's `data/qa.globalconfig` | a .globalconfig used instead of the kit's |
| `qa.staticAnalysis.sarifDir` | the run folder only | where the SARIF files are also copied |
| `qa.staticAnalysis.kinds` | none | rule id to bug, vulnerability, smell or ignore |

A wrong key stops the run before anything is planned or built, one line per key (F67).

## How the analyzers get into the build

1. NetAnalyzers ship with the .NET SDK: SDK-style projects get them by `EnableNETAnalyzers=true`. The
   package is downloaded only when the repository has a project of the old format (no `Sdk`), and only
   those projects get its DLLs - never two copies in one compiler.
2. The other packages are downloaded with `dotnet restore` of a throwaway project in the run folder that
   only has `PackageDownload` items; the DLLs taken from each package are the C# folder of its highest
   `roslyn<X.Y>`, else `analyzers/dotnet/cs`, `analyzers/cs`, `analyzers`, plus the language-neutral DLLs
   beside a cs folder.
3. A `.targets` file written for the run turns analyzers on, keeps warnings warnings, writes one SARIF per
   project and target framework, adds the .globalconfig, passes each DLL as an `Analyzer` item, lists
   the analyzers the compiler got after `CoreCompile`, and names itself a compile input so the compiler
   runs again even when nothing changed.
4. The build is the profile's own `build` verb (`paperflow.ps1 build -Full`), started with these
   environment variables: `CustomAfterMicrosoftCommonTargets` (that `.targets`), `MSBUILDDISABLENODEREUSE=1`
   and `EnableNETAnalyzers=true` (`false` when NetAnalyzers is off: left out, the SDK runs them anyway). MSBuild reads environment variables as properties; arguments are not
   used because PowerShell 5.1 cannot pass MSBuild switches through the verb (measured: `-v:n` arrives
   split at the colon).

## Configuration: the project wins

`data/qa.globalconfig` sets the NetAnalyzers categories Security, Reliability, Usage, Performance,
Design, Maintainability, Globalization and Interoperability to warning and Naming, Style and
Documentation to none, at `global_level = -1`: a project's own `.globalconfig` or `.editorconfig` wins
over it. Roslynator and Sonar keep their own default rule sets. The file reaches the compiler as an
`EditorConfigFiles` item (an item of the other name, added this late, is never copied there - measured).

## Kind and severity

First match wins: the profile line (`qa.staticAnalysis.kinds`), the kit's table (the Sonar table below,
and a short list of CA and RCS rules that are always bugs), the kind the SARIF category names (Sonar's
`<Severity> <Type>`, NetAnalyzers' Security and Reliability), then smell. The report says which source
decided each group. Severity: Sonar's own, else bug and vulnerability major, smell minor (info for a
note). A former security hotspot shows as `hotspot: review`.

Not listed, only counted: compiler warnings (CS), analyzer load problems (CS8032, CS8034, CS9057, AD0001),
suppressed results, results in generated files or outside the repository, and - for a branch or folder
scope - results outside the files in scope (F68). A result with no location belongs to the project and is
listed in a project scope.

Each rule links to its page: Sonar `rules.sonarsource.com/csharp/RSPEC-<n>`, CA and IDE
`learn.microsoft.com`, Roslynator `josefpihrt.github.io`.

## The Sonar table

`data/sonar-cs-rules.tsv`: id, kind, severity, title, CWE, OWASP of every C# rule, generated from
`analyzers/rspec/cs/*.json` of SonarSource/sonar-dotnet at the tag of the default version (the first line
names the tag and the commit). Nothing else is taken from that repository. In 10.35 no rule is a hotspot
any more; the former ones are vulnerabilities.

## Not verifiable, never "0 findings"

- No dotnet SDK, a package that cannot be downloaded (the first NU line), a package with no analyzer
  DLL, or an analyzer that never reached the compiler or failed to load: exit 4, naming the analyzer
  (F57).
- The build red, or no SARIF file at all: exit 4 with the command and its last five lines, and no finding
  list from that build (F58).
- Another build holds the project: exit 4; run `static` again once it finishes.

## Limits

- Old-format projects build with the MSBuild of Visual Studio; the NetAnalyzers and Sonar versions a
  compiler that old can load are measured per project, not assumed.
- A build verb that is a script wrapping MSBuild must pass the environment through.
- Languages other than C# and VB have no static lane; their agent lanes still run.
