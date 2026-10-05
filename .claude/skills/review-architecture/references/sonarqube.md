# /review-architecture and a self-hosted SonarQube

Optional. A project that runs its own SonarQube Community server can let `/review-architecture` read the server's numbers
and open issues instead of reading every file blind. The layer is for `/review-architecture` only (ADR-0044); the other
reviews never call it. Nothing here installs or configures a server: the owner installs it and sets the machine variables.

## What the project and the machine declare

The project, in `.claude/paper.profile.json`, key `review.sonarqube`:

| Key | Default | Meaning |
| --- | --- | --- |
| `projectKey` | none | the project's key on the server; without it the layer is off and nothing is said |
| `url` | `PAPER_SONARQUBE_URL`, else `http://localhost:9000` | the server |
| `hotspots` | 20 (1 to 500) | how many files `plan` hands the architecture and smell lanes |
| `timeoutSec` | 600 (60 to 3600) | how long to wait for the server to come up and for the analysis |
| `scanner` | `auto` | `auto` (`dotnet-sonarscanner` when the repository has .NET code, else the SonarScanner CLI), `dotnet` or `cli` |
| `sources` | `src`, else `.` | SonarScanner CLI only: the folders to analyse, a text or a list, relative to the repository root |
| `tests` | `tests` or `test` when there, none when `sources` is `.` | SonarScanner CLI only: the test folders, outside the sources (SonarQube indexes a file once) |
| `exclusions` | none | SonarScanner CLI only: file patterns to leave out, one per item (`sonar.exclusions`) |

The machine, as variables of the user (never in a repository):

| Variable | Meaning |
| --- | --- |
| `PAPER_SONARQUBE_TOKEN` | the access key (a user token). Needed. Never printed, written to a file or put on a command line |
| `PAPER_SONARQUBE_URL` | the server, when the profile has none |
| `PAPER_SONARQUBE_HOME` | the unzipped server folder, so the command can start a server on this machine that is not running |
| `PAPER_SONARQUBE_JAVA` | `java.exe`, or the folder of a JRE, for that server; also the `JAVA_HOME` of the SonarScanner CLI |
| `PAPER_SONARQUBE_SCANNER` | the SonarScanner CLI: `sonar-scanner.bat`, or its unzipped folder (the one with `bin\sonar-scanner.bat`); else `sonar-scanner` on PATH |
| `PAPER_SONARQUBE_KEEP` | `1`: do not stop a server this run started |

## Two scanners

A repository with .NET code (a `.csproj`, `.vbproj`, `.fsproj`, `.sln` or `.slnx` file, or a `.cs` or `.vb` source) is scanned
by `dotnet-sonarscanner` around the project's own build, as before. Any other repository (Python, TypeScript...) is scanned by
the SonarScanner CLI, which needs no build: the command passes `sonar.projectKey`, `sonar.host.url`, `sonar.sources`,
`sonar.tests`, `sonar.exclusions` and a work folder, `<runs>/sonarqube/cli-work`, inside the run folder (.paper/review) that git always
ignores - so the repository needs no `.gitignore` line for the CLI (the `.sonarqube/` line is for `dotnet-sonarscanner` only).
`review.sonarqube.scanner` overrides the choice (`cli` for a repository that mixes .NET and TypeScript). The command prints the
scanner it chose and why. A `sonar-project.properties` file of the repository is still read by the CLI for the keys the kit does
not pass.

## Install the SonarScanner CLI (once per machine)

The kit does not download or install it. Download the Windows x64 zip from the SonarScanner CLI page of SonarSource, unzip it
into a folder of the machine (for example `D:\Apps\SonarScanner`), and set the user variable `PAPER_SONARQUBE_SCANNER` to that
folder (or add its `bin` folder to PATH). A JRE comes in the zip; `PAPER_SONARQUBE_JAVA`, when set, is used as the `JAVA_HOME`.

## What `review.ps1 sonar -Kind architecture` does

1. Applicability. When it does not apply: exit 5 with the first reason - no `projectKey`, an external repository, a
   `branch` or `files` scope (the Community edition keeps one branch per project and reads the whole project), the
   checkout not on the base branch, uncommitted work (the analysis would publish it as the base), `.sonarqube/` not
   ignored by git (`dotnet-sonarscanner` only), no `PAPER_SONARQUBE_TOKEN`, no scanner: no `dotnet-sonarscanner` on PATH, or no
   SonarScanner CLI (a wrong `PAPER_SONARQUBE_SCANNER` is a reason of its own and the PATH is not tried), `scanner: dotnet` in
   a repository with no .NET code, a `sources` or `tests` folder that is not there or a test folder inside the sources.
2. The server. `GET /api/system/status` is `UP`: use it. Not up, the address is this machine's and
   `PAPER_SONARQUBE_HOME` has `bin\windows-x86-64\StartSonar.bat`: start it, wait for `UP` (at most `timeoutSec`), and stop
   it at the end unless `PAPER_SONARQUBE_KEEP=1`. Otherwise exit 4 with what the server said.
3. The analysis. The server's latest analysis of the project has the commit you are on: reuse it. Otherwise
   `dotnet-sonarscanner begin`, the project's own build with `PaperDeployDebug=false` (it never deploys), `end` - or, with the
   SonarScanner CLI, one run with no build; the token reaches the scanner only as `SONAR_TOKEN` of its process. Then wait for the compute-engine task: `SUCCESS`
   reads; `FAILED` or `CANCELED` is exit 4, and no older result stands in. The scanner log, with the key removed, is in
   `<runs>/sonarqube/`.
4. The numbers. File measures (cognitive complexity, cyclomatic complexity, duplicated lines, lines) and the open issues,
   paged, are saved to `<runs>/sonarqube/<projectKey>-<commit12>.json` (the runs folder is git-ignored).
5. `plan` reads that file for the same commit: the architecture and smell lanes get the top `hotspots` files, ranked by
   cognitive complexity, then weighted open issues, then duplicated lines; the plan and the report say how many files were left out - a file the server measured is "not in the top N", a file it
   has no number for (a test file, a file outside `sources`) is "not measured by the SonarQube analysis". `-AllFiles`
   (`review.ps1 plan -AllFiles`) reads every file. The report gets a `## SonarQube` section: the numbers, the top issues
   and the hot spots. On a repository with no .NET project the static lane is not applicable, so the `## Complexity` table is
   built from the server's issues S3776, S1541 and S1067 (the function name at the place the server reports) and says so.

Exit codes: 0 saved, 4 not verifiable (the server does not answer or come up, the scanner or the build failed, the
analysis failed or timed out, the CLI left no task id), 5 not applicable. For 4 and 5 go on with `plan`: it reads every file, as it does without the
layer.

## Never

- Print, save or pass the access key on a command line. Every line the command prints or writes goes through a filter that
  removes it.
- Install, upgrade or configure a server, create a project or a token, or change a quality profile.
- Scan a branch, uncommitted work or an external repository.
