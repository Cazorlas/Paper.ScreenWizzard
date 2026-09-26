# Báo bản mới — plan — 2026-09-26

**Trạng thái:** đã duyệt 2026-09-26 ("ok làm nốt lun đi, mà nếu có bản update mới thì có tự động biết để báo user cài ko"; trước đó "cho toàn quyền với bạn, bạn cứ quyết", "bạn tự làm hết các bước lun đi")
**Loại việc:** code

Ứng dụng hỏi GitHub bản phát hành mới nhất: một phút sau khi mở, rồi mỗi ngày một lần. Luật "mới hơn không, báo chưa" nằm ở
`UpdateInteractor`. Adapter chỉ đọc `tag_name`; App hiện thông báo của Windows và dòng khay. Có một cài đặt mới, `checkForUpdates`
(mặc định bật); tập tin cũ không có mục này thì lấy mặc định (F8).
Brief: [2026-09-26-bao-ban-moi.md](2026-09-26-bao-ban-moi.md) · Luật: [SPEC.md](SPEC.md)

## Context

- **Chạm:**
  - Domain/Shell: thêm `AppVersion` và `UpdateRules`; `AppSettings`, `StoredSettings`, `SettingsDefaults`, `SettingsRules` có
    thêm `CheckForUpdates`.
  - UseCases/Shell:
    - port mới `IReleaseFeed`, `IBrowser` và `IUpdateInteractor`;
    - model trong `UpdateModels.cs`;
    - `UpdateInteractor`.
  - Infrastructure/Shell: `GitHubReleaseFeed`, `Browser`; `SettingsStore` có thêm một mục.
  - Presentation/Shell:
    - dòng khay chỉ có khi có bản mới (`TrayUpdateCommand`);
    - ô "Bản mới" ở Cài đặt;
    - sáu chuỗi vi/en.
  - App: `AppShell` (hẹn giờ, mở trang), `TrayIcon.ShowNotice`, `CompositionRoot`.
- **Dịch vụ dùng chung:**
  - thông báo: có, `INotifications.ShowError` cho F10; thông báo của Windows qua `NotifyIcon` có sẵn;
  - log: có;
  - lưu cài đặt: có.
- **Bài học đã nhận áp dụng:** không có. **Bug liên quan:** không có.
- **Môi trường:**
  - phiên cloud không có .NET SDK (proxy chặn `builds.dotnet.microsoft.com`), nên biên dịch và unit chạy trên CI;
  - `ui` và E2E cần máy Windows có màn hình.

## Rules that apply

- Mọi quyết định ở UseCases và Domain; adapter không quyết. `App` chỉ ghép và hẹn giờ theo hằng của Domain.
- Port nằm trong domain dùng nó (`Shell/Ports`); mỗi file một interface (ADR 0003, `FolderShapeTests`).
- Chữ qua khoá, đủ vi và en; menu khay giữ tám dòng khi không có bản mới (`TrayMenuTests`).
- Test không gọi mạng thật.

## Decisions

- **Chỉ báo, không tự cài.** Một trình cài chưa ký số, tự tải rồi tự chạy là một quyết định an ninh riêng (ADR); SPEC giữ nó ở
  "What it does not do yet".
  Rejected: tải `Setup.exe` vào thư mục tạm rồi chạy `/SILENT`.
- **Trang mở được dựng từ số bản** (`UpdateRules.ReleasePage`), không lấy `html_url` từ câu trả lời, nên ứng dụng chỉ mở trang
  phát hành của chính kho này.
- **Báo một lần cho mỗi bản trong một lần chạy.** Lần mở sau thì báo lại, vì người dùng chưa cài. Không ghi gì thêm vào cài đặt.
- **Thông báo là `NotifyIcon.ShowBalloonTip`.** Trên Windows 10 và 11 nó thành toast, bấm vào được, không cần thư viện mới.
  Rejected: hộp thoại (chặn người dùng giữa lúc làm việc).
- **Bản dựng không mang x.y.z** (bản dựng của người phát triển là `1.0.0`, luôn mới hơn) thì không báo gì.

## Tasks

### 1. Luật (unit)

- [x] T1 `AppVersion` và `UpdateRules`; test `AppVersionTests` (đọc tag và số bản của exe, từ chối bản thử, so theo số) {files: src/Paper.ScreenWizzard.Domain/Shell/AppVersion.cs, src/Paper.ScreenWizzard.Domain/Shell/UpdateRules.cs, tests/Paper.ScreenWizzard.UnitTests/Shell/UpdateSpecTests.cs}
- [x] T2 `UpdateInteractor` với port giả; test `UpdateSpecTests`: bản mới hơn thì báo một lần, bản bằng hay cũ hơn thì không báo, tắt thì không hỏi, F9, F10 {files: src/Paper.ScreenWizzard.UseCases/Shell/**, tests/Paper.ScreenWizzard.UnitTests/Shell/**}
- [x] T3 Cài đặt `checkForUpdates`: mặc định bật, thiếu thì lấy mặc định (F8); test trong `SettingsRulesTests` {files: src/Paper.ScreenWizzard.Domain/Shell/**, src/Paper.ScreenWizzard.Infrastructure/Shell/SettingsStore.cs, tests/Paper.ScreenWizzard.UnitTests/Shell/SettingsRulesTests.cs, tests/Paper.ScreenWizzard.E2eTests/Support/AppRun.cs}

### 2. Adapter và màn hình

- [x] T4 `GitHubReleaseFeed`, `Browser`; ghép trong `CompositionRoot` {files: src/Paper.ScreenWizzard.Infrastructure/Shell/**, src/Paper.ScreenWizzard.App/CompositionRoot.cs}
- [x] T5 Dòng khay "Tải bản mới…", ô "Bản mới" ở Cài đặt, chuỗi vi/en; test `TrayMenuTests`, `LanguageAndThemeTests` {files: src/Paper.ScreenWizzard.Presentation/**, tests/Paper.ScreenWizzard.UiTests/Shell/**}
- [x] T6 `AppShell`: hẹn giờ, thông báo, mở trang {files: src/Paper.ScreenWizzard.App/**}
- [ ] T7 `ui` trên máy Hùng (test khay mới, khoá chuỗi); thử tay: mở bản 0.1.2 khi đã có 0.1.3 → một phút sau có thông báo, bấm vào mở đúng trang

### Last. Close

- [x] T8 SPEC hai ngôn ngữ, CODEMAP, `check-spec` và `check-code-map` sạch; CI xanh trên nhánh {files: docs/**, src/**/CODEMAP.md}

## API đã tra

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|
| GitHub REST `GET /repos/{owner}/{repo}/releases/latest` | API 2022-11-28 | đo từ phiên cloud: 200 khi không đăng nhập | không trả bản nháp hay pre-release; không có release thì 404; bắt buộc có `User-Agent`; không đăng nhập thì giới hạn 60 lần một giờ cho mỗi IP, nên kiểm mỗi ngày một lần là dư |
| `NotifyIcon.ShowBalloonTip`, `BalloonTipClicked`, `BalloonTipClosed` | .NET 10 WinForms | **chưa đọc** ở phiên này (learn.microsoft.com chưa thử); theo tài liệu quen dùng. Đo bằng T7 | Windows 10 và 11 hiện nó thành toast; bấm vào thì có `BalloonTipClicked` |
| `Process.Start` với `UseShellExecute = true` | .NET 10 | theo tài liệu quen dùng | mở địa chỉ https bằng trình duyệt mặc định; không có trình duyệt thì ném `Win32Exception` |

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
