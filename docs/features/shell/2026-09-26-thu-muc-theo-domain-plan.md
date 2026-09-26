# Thư mục theo domain (ADR-0003) — plan — 2026-09-26

**Trạng thái:** chờ duyệt
**Loại việc:** code

Chuyển năm project `src/` sang hình "thư mục cấp một là domain, vai là cấp hai" của kit 1.32.0, đổi tên port bỏ hậu tố
`Port`, entry host không còn `Startup/`; refactor thuần theo skill `parity-refactor`, không đổi hành vi.
Brief: [2026-09-26-thu-muc-theo-domain.md](2026-09-26-thu-muc-theo-domain.md) · Luật: [SPEC.md](SPEC.md) ·
Quyết định: [ADR-0003](../../decisions/0003-thu-muc-cap-mot-la-domain.md)

## Context

- Chạm cả năm project `src/Paper.ScreenWizzard.*` và ba project test (chỉ `using` và tên fake). Không đổi `.csproj`
  nào ngoài thư mục (SDK tự gom file `.cs` và `.xaml`).
- Có sẵn: `tests/Paper.ScreenWizzard.UnitTests/Architecture/LayerTests.cs` (hướng tham chiếu, host-free, interactor qua
  interface); test mẫu của kit cho hình mới ở `.claude/skills/clean-architecture/references/architecture-tests-csharp.md`
  mục 5 và 6.
- **Dịch vụ dùng chung:** không cần dịch vụ mới; refactor không đổi transaction, thông báo, cài đặt, log hay lỗi.
- **Bài học đã nhận áp dụng:** không có (`docs/retro/` chưa có). **Bug liên quan:** không có (`docs/bugs/` chưa có).
- **Môi trường:** phiên cloud này **không có .NET SDK** (tải bị chính sách mạng chặn, 403) và không có `pwsh`. Ở đây
  chỉ chạy được script Python (so sánh parity, `check-spec`, `check-code-map`). Dựng và unit chạy trên CI
  `windows-latest`; `ui` và e2e chạy trên máy Hùng. Nhóm 3 ghi rõ điều đó.

### Bảng chuyển

| Project | Hiện tại | Sau |
| --- | --- | --- |
| Domain | `Capture/`, `Editor/`, `Shell/`, `Common/`, `Geometry/` | `Capture/`, `Editor/`, `Shell/`, `Shared/` (= `Common/` + `Geometry/`) |
| UseCases | `<D>/{Ports, Implements, Models}`, `Common/…` | `<D>/{Ports, UseCases, Models}`, `Shared/{Ports, UseCases, Models}` |
| Infrastructure | `Capture/`, `Shell/`, `Common/`, `NativeMethods.cs` | `Capture/`, `Shell/`, `Shared/`, `NativeMethods.cs` |
| Presentation | `ViewModels/<D>/[Commands]`, `Views/<D>/[Services]`, `Rendering/`, `Mvvm/`, `Resources/` | `<D>/{ViewModels, Commands, Views}`, `Editor/Rendering/`, `Mvvm/`, `Resources/` (thư mục `Services/` gộp vào `Views/`) |
| App | `Startup/*.cs`, `App.xaml(.cs)` | mọi file ở gốc project |

Namespace đi theo thư mục (ví dụ `Paper.ScreenWizzard.UseCases.Capture.Implements` → `…UseCases.Capture.UseCases`,
`…Domain.Geometry` → `…Domain.Shared`, `…Presentation.ViewModels.Shell` → `…Presentation.Shell.ViewModels`,
`…App.Startup` → `…App`).

### Port: một file một interface, đúng domain, bỏ hậu tố

| Cũ (file cũ) | Mới (thư mục) |
| --- | --- |
| `IScreenSourcePort`, `IWindowCatalogPort` (`Capture/Ports/CapturePorts.cs`) | `IScreenSource`, `IWindowCatalog` (`Capture/Ports/`) |
| `IMonitorCatalogPort` (`Shell/Ports/ShellPorts.cs`, chỉ lõi Capture gọi) | `IMonitorCatalog` (`Capture/Ports/`) |
| `ISettingsStorePort`, `IHotkeyPort`, `IAutostartPort`, `ISingleInstancePort` | `ISettingsStore`, `IHotkeys`, `IAutostart`, `ISingleInstance` (`Shell/Ports/`) |
| `IClockPort`, `IDelayPort`, `ILogPort`, `INotificationPort`, `IClipboardPort`, `IFileStorePort`, `IImageCodecPort` | `IClock`, `IDelay`, `ILog`, `INotifications`, `IClipboard`, `IFileStore`, `IImageCodec` (`Shared/Ports/`) |
| `ICaptureInteractor`, `ICaptureSession`, `IEditorInteractor`, `IEditorSession`, `IShellInteractor`, `IImageDelivery` | giữ tên, mỗi cái một file trong `Ports/` của domain nó |
| `ScreenCaptureIssue`, `ScreenCaptureResult` (trong `CapturePorts.cs`) | `Capture/Models/` |
| `DeliveryIssue`, `DeliveryResult` (trong `IImageDelivery.cs`) | `Shared/Models/` |
| Fake `FakeDelayPort`, `FakeHotkeyPort`, `FakeAutostartPort` | `FakeDelay`, `FakeHotkeys`, `FakeAutostart` |

Bên cài (`ScreenSource`, `SettingsStore`, `HotkeyService`, `ClipboardService`…) giữ tên.

## Rules that apply

- `CLAUDE.md`, bảng tầng và ADR-0001: năm project, hướng tham chiếu, Presentation không tham chiếu Infrastructure —
  **không đổi**; `LayerTests` phải xanh sau mỗi bước.
- ADR-0003 (Proposed): hình bên trong project. Plan này làm theo nó; duyệt plan không phải duyệt ADR.
- Skill `parity-refactor`: không đổi hành vi, so trước và sau, `SPEC.md` không đổi.
- `CLAUDE.md`: 0 cảnh báo (cảnh báo là lỗi); `_camelCase` cho thành viên private; namespace dạng file-scoped.

## Decisions

- **Mốc so sánh là commit gốc của nhánh, đọc bằng `git show`, không chép `Baseline/`.** Refactor này chỉ dời file và
  đổi tên; chép hơn trăm file vào `Baseline/` rồi dựng cả hai bản là thừa. Thay vào đó script
  `docs/features/shell/harness/parity.py` so **mọi dòng code** của `src/` và `tests/` ở commit gốc với cây làm việc,
  sau khi bỏ dòng `using`/`namespace`, bỏ chú thích và áp bảng đổi tên port ở trên: hai tập phải bằng nhau từng dòng
  (bước 5.4 của `parity-refactor`). Nó cũng so **danh sách binding, `x:Name` và `AutomationId` của mọi file XAML**
  (mục 1b, "What cannot be Baselined"): chỉ `x:Class` và `clr-namespace` được khác. Rejected: `Baseline/` chép đủ,
  vì không có logic nào đổi để so bằng cách chạy.
- **Đổi tên port bỏ hậu tố `Port`.** Kit nói tên theo cái cần; hai tên dạng số nhiều (`IHotkeys`, `INotifications`) vì
  `IHotkey` hay `INotification` đọc như một phím hay một thông báo. Rejected: giữ `I<X>Port` như `CLAUDE.md` cũ ghi,
  vì reviewer của kit sẽ báo mãi; `CLAUDE.md` đổi theo ở T11.
- **`Mvvm/` và `Resources/` ở lại cấp một của Presentation**, khai trong `NotADomain` của test kèm lý do (ADR-0003,
  Decision 1). Rejected: dời vào `Shared/`, vì đường dẫn pack URI của từ điển XAML đổi mà không mua được gì.
- **`App/Startup/` dồn về gốc `App/`.** Test `OuterProject_StaysThin` của kit cho phép file ghép ở gốc; `AppShell` còn giữ
  vài quyết định (nợ đã ghi ở plan đợt 1, "Nợ kiến trúc sau T21") — **không** sửa trong refactor này, vì đó là đổi logic.
- **Không sửa lỗi dọc đường.** Thấy lỗi thì ghi vào báo cáo; sửa trong plan riêng, nơi phép so thấy đúng khác biệt đó.
- **Việc sửa đọc `settings.json` (trường thiếu lấy mặc định) tạm dừng** tới khi plan này xong; nó sẽ có SPEC, brief và
  plan riêng, viết trên hình mới.

## Tasks

Lane `unit` chạy nối tiếp trong một lane agent. Task không tag lane do phiên chính làm. Không task nào thuộc lane `ui`:
refactor không có mock UI mới để viết đỏ trước; bộ `ui` có sẵn (441 test) là phép kiểm lại ở T10.

### 1. Test hình thư mục và phép so — đỏ trước

- [ ] T1 [red][unit] `Architecture/FolderShapeTests.cs` theo mục 5 và 6 của test mẫu kit, đọc `.slnx`: `NoSplitDomain_KeepsARoleFolderAtItsTop` (Domain, UseCases), `EveryLayerFolder_NamesADomain` (UseCases, Infrastructure, Presentation; `NotADomain` = `Shared`, `Mvvm`, `Resources`), `OuterProject_StaysThin` (App), `EveryPortsFolder_HoldsOnlyInterfaces`, `EveryInteractor_HasItsInterfaceInItsDomainsPorts`, `NoPort_EndsInPort`, `NoDomain_KeepsItsContractsInAServicesFolder`, mỗi test có test canh danh sách không rỗng; sửa `LayerTests.EveryInteractor_…` nhận `Ports` ở cấp hai — xong khi, trên cây hiện tại, các test mới đỏ ở **assertion** (CI hoặc máy Hùng) {files: tests/Paper.ScreenWizzard.UnitTests/Architecture/**}
- [ ] T2 Script `parity.py` (so dòng code và kho XAML như Decisions) — xong khi (a) chạy trên cây chưa đổi báo 0 khác biệt, và (b) đổi thử một hằng số trong một file thì nó báo đúng dòng đó, rồi hoàn lại {files: docs/features/shell/harness/**}

### 2. Chuyển từng tầng — lane unit, nối tiếp

- [ ] T3 [unit] Domain: `Common/` + `Geometry/` → `Shared/`, namespace `…Domain.Shared`; sửa `using` khắp `src/`, `tests/` — xong khi `parity.py` 0 khác biệt {files: src/**, tests/**}
- [ ] T4 [unit] UseCases: `Implements/` → `UseCases/`, `Common/` → `Shared/`, tách mỗi file `*Ports.cs` thành một file một interface, record/enum trong `Ports/` sang `Models/`, `IMonitorCatalogPort` sang `Capture/Ports/` — xong khi `parity.py` 0 khác biệt {files: src/**, tests/**}
- [ ] T5 [unit] Đổi tên port và fake theo bảng — xong khi `grep -rE "\bI\w+Port\b" src tests` rỗng và `parity.py` 0 khác biệt {files: src/**, tests/**}
- [ ] T6 [unit] Infrastructure: `Common/` → `Shared/` — xong khi `parity.py` 0 khác biệt {files: src/**, tests/**}
- [ ] T7 [unit] Presentation: `<D>/{ViewModels, Commands, Views}`, `Rendering/` → `Editor/Rendering/`, `Services/` gộp vào `Views/`; `x:Class` và `clr-namespace` trong XAML — xong khi `parity.py` 0 khác biệt cả phần XAML {files: src/**, tests/**}
- [ ] T8 [unit] App: `Startup/*` về gốc, namespace `Paper.ScreenWizzard.App` — xong khi `parity.py` 0 khác biệt và cây thư mục khớp bảng chuyển {files: src/**, tests/**}

### 3. Dựng và chạy trên Windows

- [ ] T9 Đẩy nhánh; CI `windows-latest` dựng Release (0 cảnh báo) và chạy unit — xong khi xanh, số test đã chạy ≥ 329 cộng số test T1 thêm, và mọi test của T1 xanh (cần một PR để CI chạy: hỏi Hùng trước khi mở)
- [ ] T10 Trên máy Hùng: `paperflow build`, `paperflow test`, `paperflow ui`, `dotnet test tests/Paper.ScreenWizzard.E2eTests -c Debug` — xong khi 0 cảnh báo, và số test đạt bằng lần chạy đầy đủ trước (ui 441, e2e 112), không test nào bị bỏ qua mới

### Last. Close

- [ ] T11 `CLAUDE.md` (bảng tầng: `I<Feature>Interactor`, port `I<X>`, đường dẫn mới; mục Việc đang làm), `CODEMAP.md` gốc và của năm project; `check-code-map` sạch {files: CLAUDE.md, CODEMAP.md, src/**/CODEMAP.md}
- [ ] T12 `find-bug`: **không áp dụng** — không dòng `SPEC.md` nào đổi; phép so của T2 và bộ test của T9, T10 thay nó. Xong khi dòng bằng chứng ghi lý do này
- [ ] T13 Agent `architecture-reviewer` trên danh sách file review (mọi file đổi) — xong khi đọc N/N và 0 vi phạm mục 1-10 (một vi phạm là fail: về `/task-do`)
- [ ] T14 `check-spec` sạch (không `SPEC.md` nào đổi); `parity.py` chạy lần cuối 0 khác biệt, rồi xoá nó cùng thư mục `harness/` — xong khi hai lệnh exit 0 {files: docs/features/shell/harness/**}

## API đã tra

Không gọi member mới nào của WPF hay Win32: chỉ dời file và đổi tên kiểu của chính dự án.

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|
| `x:Class` (XAML) | WPF .NET 10 | **chưa đọc**: `learn.microsoft.com` bị chính sách mạng của phiên cloud chặn (403, 2026-09-26); `/task-do` tra ở máy Hùng hay qua CI trước T7 | điều đang giả định: namespace trong `x:Class` phải trùng namespace của partial class trong `.xaml.cs`, không thì build lỗi |

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
