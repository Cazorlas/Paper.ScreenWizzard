# Paper.ScreenWizzard

Ứng dụng desktop Windows của Paper: chụp màn hình (vùng chữ nhật, vùng tự do, cửa sổ, toàn màn hình),
sửa ảnh vừa chụp, quay màn hình, và ghi chú lên mọi cửa sổ như EpicPen. Chạy độc lập, không cần host nào.

Đây là dự án độc lập. Không áp dụng quy ước của PaperPlus hay Paper.AutoCad cho nó, và **không tham chiếu
mã nguồn của chúng** (không `<Compile Include>` file của dự án khác; cần thì tham chiếu project hoặc tự giữ
một bản).

## Đọc theo thứ tự

1. Việc là một tính năng: `docs/features/<slug>/SPEC.md`, rồi brief và plan đang mở của nó.
2. `docs/roadmap.md` cho thứ tự các đợt và điều đã hoãn.
3. `docs/decisions/` cho lý do của các quyết định khó đảo ngược.

Vòng đời: task-spec (SPEC.md, brief, plan) -> **dừng chờ người dùng duyệt** (kèm liên kết mở được tới từng
tài liệu, lần nào cũng vậy) -> task-do -> task-verify, chạy bằng skill `paperflow`. Cổng:
`.claude/paperflow/paperflow.ps1 tasks -Path <plan>`.

## Tầng (ADR 0001, phương án A: mỗi hàng một project)

| Project | Target | Chứa | Được tham chiếu | ADR |
| --- | --- | --- | --- | --- |
| `Paper.ScreenWizzard.Domain` | `net10.0` | kiểu giá trị (điểm, hình chữ nhật, vùng chọn, ghi chú vẽ), luật thuần | không gì | 0001 |
| `Paper.ScreenWizzard.UseCases` | `net10.0` | interactor `I<Feature>Interactor`, port `I<X>Port`, record vào / plan ra | Domain | 0001 |
| `Paper.ScreenWizzard.Infrastructure` | `net10.0-windows10.0.19041.0` | adapter: chụp, danh sách cửa sổ / màn hình, clipboard, phím tắt, file, cài đặt, khởi động cùng Windows | UseCases, Domain | 0001 |
| `Paper.ScreenWizzard.Presentation` | `net10.0-windows10.0.19041.0` | Views, ViewModels, Commands, Resources (giao diện sáng/tối, chuỗi vi/en), `Mvvm/` | UseCases, Domain — **không Infrastructure** | 0001 |
| `Paper.ScreenWizzard.App` | `net10.0-windows10.0.19041.0` (WinExe) | entry host: `Program`/`App.xaml`, khay hệ thống, gốc ghép DI | mọi tầng | 0001 |

- **Mọi quyết định ở UseCases và Domain**, chạy trong unit test không cần Windows. Ví dụ: chuẩn hoá vùng kéo,
  chọn cửa sổ dưới con trỏ, kẹp vào mép desktop, đếm bước, undo/redo, đặt tên file, kiểm phím tắt.
- **Win32/GDI/WinRT để chụp, đọc hệ thống và ghi ra ngoài chỉ ở Infrastructure**, sau port; qua port chỉ đi số, chuỗi, mảng
  byte pixel, record. Ngoại lệ có chủ ý, mỗi cái nằm cạnh cửa sổ nó đặt và không quyết định gì: đặt cửa sổ theo pixel vật lý
  (`Presentation/Views/Capture/PhysicalWindowPlacer`), viền tối của cửa sổ, vị trí thanh chụp (`App/Startup/NativeMethods`).
  Toạ độ luôn là **pixel vật lý của desktop ảo** (app chạy per-monitor DPI v2), không bao giờ đơn vị hiển thị.
- **ViewModel không gọi Infrastructure**: project Presentation không tham chiếu nó, nên đó là lỗi biên dịch.
  Nó nhận interface UseCases từ DI. Test kiến trúc trong `tests/Paper.ScreenWizzard.UnitTests` canh hướng
  tham chiếu.
- `Domain` và `UseCases` cấm `System.Windows` và `System.Drawing`: hook `layer-guard` chặn lúc sửa, compiler chặn
  lúc build.
- **Đã chọn phương án nào thì làm đúng phương án đó** (skill `adr`, bước 7). Muốn gộp hai tầng hay hạ một tầng
  thành thư mục: ghi thành ngoại lệ trong ADR và chờ chủ dự án duyệt, không tự làm.

## WPF và MVVM

- `.NET 10`, WPF, một tiến trình, một instance (instance thứ hai chỉ đánh thức instance đầu).
- MVVM tối thiểu **tự giữ trong `Presentation/Mvvm/`**: `BindableBase`, `CommandBase`, `AsyncCommandBase`, tên và
  hình dạng như `paper-wpf-style`; không dùng CommunityToolkit.Mvvm, không viết `RelayCommand`. Chưa có source
  generator: property viết tay bằng `SetProperty`, đến khi có nhiều hơn ~30 property thì đánh giá lại
  bằng một ADR.
- Mỗi lệnh là một class có tên; logic quy trình nằm trong lệnh, không ở ViewModel hay code-behind.
- Chữ hiển thị qua `{DynamicResource <Khoá>}`, hai ngôn ngữ vi và en trong `Resources/Strings.<lang>.xaml`.
  Màu qua `DynamicResource`, không `#RRGGBB` cứng. Icon-only button có `AutomationProperties.Name`.
- Cài đặt người dùng: `%AppData%\Paper\ScreenWizzard\configs\settings.json`, `System.Text.Json`, file hỏng thì
  giữ bản `.bak` và dùng mặc định, có báo.
- Trường thành viên và **property** private đặt tên `_camelCase` (tiền tố gạch dưới).

## Lệnh

Khai ở `.claude/paper.profile.json` mục `verbs`; đi qua `.claude/paperflow/paperflow.ps1 <verb>`, không gọi
`dotnet build` hay `dotnet test` trần.

| Verb | Chạy | Ghi chú |
| --- | --- | --- |
| `build` | `dotnet build` cả solution, Debug | 0 cảnh báo (cảnh báo là lỗi) |
| `test` | project `UnitTests` (Domain, UseCases, kiến trúc) | không cần Windows tương tác |
| `ui` | project `UiTests`: view dựng trong tiến trình test trên dữ liệu giả, FlaUI lái | **cần màn hình rảnh**, không chạy cùng lúc với `e2e` |
| *(không có verb)* | project `E2eTests`: adapter thật và chính file exe, chạy bằng `dotnet test tests/Paper.ScreenWizzard.E2eTests -c Debug` | paperflow chỉ cấp verb cho lane mà host chứng minh được (`unit`, `ui`), nên không có verb `e2e`; cần màn hình rảnh, gửi phím tắt toàn cục thật, không chạy cùng lúc với `ui` |

## Deploy (CI, gói, phát hành)

Luật chung ở skill `paper-wpf-style`, `references/build-and-deploy.md`; đây là phần của dự án này.

- **CI:** `.github/workflows/ci.yml` — `windows-latest`, `dotnet build -c Release` (cảnh báo là lỗi) rồi test
  project `UnitTests`. **`ui` và `e2e` chạy ở máy người làm, không lên CI** (cần desktop tương tác); kết quả của
  chúng là dòng bằng chứng của plan. Đọc số test đã chạy, không chỉ exit code.
- **Gói:** `installer/build-package.ps1 -Version x.y.z` (verb `package`) ra ba file ở `artifacts/`:
  `Paper.ScreenWizzard-<x.y.z>-win-x64-Setup.exe` (file cài), `Paper.ScreenWizzard-<x.y.z>-win-x64.zip` (bản di động) và
  `SHA256SUMS.txt`. `win-x64`, self-contained, một file (`PublishSingleFile`, không nén), không `PublishTrimmed` (WPF không trim
  được), không cần quyền quản trị (manifest `asInvoker`; file cài theo người dùng).
- **File cài:** Inno Setup 6.7.3 (`installer/Setup.iss`, tự đủ: mở bằng Inno Setup 6.7 hay 7.1 rồi Compile là ra `Setup.exe`, thiếu exe thì tự publish;
  hoặc bấm đúp `installer/Build-Installer.cmd`), hai ngôn ngữ tiếng Anh và tiếng Việt theo Windows
  (`installer/Languages/Vietnamese.isl`); quyết định ở [ADR 0002](docs/decisions/0002-file-cai-setup-exe-inno.md) (`Proposed`). Bộ
  biên dịch do `installer/get-inno.ps1` tải và cài vào `.tools/inno` (ghim SHA-256, không đụng máy). Luật của file cài ở
  `docs/features/release/SPEC.md`. **Một hình biểu tượng** cho mọi nơi: `src/Paper.ScreenWizzard.App/app.ico` (cả khay đọc từ exe),
  vẽ bằng `installer/make-icon.ps1` (chỉ chạy khi hình đổi; nó cũng ra `installer/wizard-small.bmp`).
- **Kiểm file cài:** `installer/verify-installer.ps1 -Setup <file>` cài, chạy, cài đè, hạ bản, cài lại và gỡ **thật** trong thư mục
  thử rồi trả máy về như cũ; exit 0 chỉ khi mọi kiểm tra đạt và số kiểm tra > 0. Nó từ chối chạy khi một bản của ứng dụng đang
  chạy hoặc đã cài (nó sẽ đóng hay thay bản đó). Chạy sau mỗi lần đổi `installer/`.
- **Phát hành:** đẩy thẻ `v<x.y.z>` → `.github/workflows/release.yml` dựng, chạy unit, dựng gói, chạy `verify-installer.ps1` rồi tạo
  **bản nháp** release kèm ba file. Chạy tay (`workflow_dispatch`) chỉ dựng và kiểm, không tạo release. **Agent không tự đẩy thẻ,
  tạo release hay bấm Publish** — chỉ khi Hùng nói trong phiên đó (2026-09-21: Hùng nói làm bản đầu và thẻ đầu).
- **Chưa có (nợ, chưa quyết):** ký số (cần chứng chỉ), cập nhật tự động, MSI. Mỗi thứ là một ADR riêng.

## Việc đang làm

Đợt 1 (khung, chụp, sửa ảnh): plan `docs/features/shell/2026-09-20-m1-chup-va-sua-anh-plan.md`. Đợt 2 (quay,
kèm camera và âm thanh) và 3 (ghi chú lên màn hình): xem `docs/roadmap.md`.
