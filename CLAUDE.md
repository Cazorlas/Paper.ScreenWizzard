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

Vòng đời: task-spec (SPEC.md, brief, plan) -> **dừng chờ người dùng duyệt** -> task-do -> task-verify, chạy
bằng skill `paperflow`. Cổng: `.claude/paperflow/paperflow.ps1 tasks -Path <plan>`.

## Tầng (ADR 0001, phương án A)

| Project | Target | Chứa | Được tham chiếu |
| --- | --- | --- | --- |
| `Paper.ScreenWizzard.Domain` | `net10.0` | kiểu giá trị (điểm, hình chữ nhật, vùng chọn, ghi chú vẽ), luật thuần | không gì |
| `Paper.ScreenWizzard.UseCases` | `net10.0` | interactor `I<Feature>Interactor`, port `I<X>Port`, record vào / plan ra | Domain |
| `Paper.ScreenWizzard.Infrastructure` | `net10.0-windows10.0.19041.0` | adapter: chụp, danh sách cửa sổ / màn hình, clipboard, phím tắt, file, cài đặt, khởi động cùng Windows | UseCases, Domain |
| `Paper.ScreenWizzard.App` | `net10.0-windows10.0.19041.0` (WinExe) | Views, ViewModels, Commands, gốc ghép DI | mọi tầng |

- **Mọi quyết định ở UseCases và Domain**, chạy trong unit test không cần Windows. Ví dụ: chuẩn hoá vùng kéo,
  chọn cửa sổ dưới con trỏ, kẹp vào mép desktop, đếm bước, undo/redo, đặt tên file, kiểm phím tắt.
- **Win32/GDI/WinRT chỉ ở Infrastructure**, sau port; qua port chỉ đi số, chuỗi, mảng byte pixel, record.
  Toạ độ luôn là **pixel vật lý của desktop ảo** (app chạy per-monitor DPI v2), không bao giờ đơn vị hiển thị.
- **ViewModel không gọi Infrastructure**; nó nhận interface UseCases từ DI. Test kiến trúc trong
  `tests/Paper.ScreenWizzard.UnitTests` canh điều này, cùng với hướng tham chiếu.
- `Domain` và `UseCases` cấm `System.Windows` và `System.Drawing`: hook `layer-guard` chặn lúc sửa, compiler chặn
  lúc build.

## WPF và MVVM

- `.NET 10`, WPF, một tiến trình, một instance (instance thứ hai chỉ đánh thức instance đầu).
- MVVM tối thiểu **tự giữ trong `.App/Mvvm/`**: `BindableBase`, `CommandBase`, `AsyncCommandBase`, tên và
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

Khai ở `.claude/paper.profile.json` mục `verbs` (task T1 điền). Không gọi `dotnet build` trần: đi qua verb.

## Việc chưa có

Chưa có solution, chưa có project nào. Task T1 của plan đầu dựng khung.
