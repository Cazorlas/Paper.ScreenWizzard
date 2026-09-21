# Paper.ScreenWizzard file cài đặt — plan — 2026-09-21

**Trạng thái:** xong 2026-09-21 (đã duyệt: Hùng nói "setup đầy đủ file cài lun nha")
**Loại việc:** code

Làm file cài `.msi` theo người dùng, bản zip di động, mã SHA-256, script dựng và script kiểm chạy thật, workflow phát hành.
Brief: [2026-09-21-file-cai.md](2026-09-21-file-cai.md) · Luật: [SPEC.md](SPEC.md)

## Context

- Đợt 1 xong; app là một exe WPF `net10.0-windows10.0.19041.0`. Deploy hiện có: CI dựng và chạy unit, chưa có gói, chưa có
  bộ cài (`CLAUDE.md`, mục Deploy).
- **Công nghệ cài: MSI dựng bằng WiX** (xem [ADR 0002](../../decisions/0002-file-cai-msi-wix.md), `Proposed`). Lý do gọn:
  dựng và kiểm được bằng lệnh `dotnet` ở cả máy người làm lẫn runner GitHub, không cần cài công cụ ngoài; theo người dùng
  nên không cần quyền quản trị; chuẩn MSI nên người quản trị cài hàng loạt được. Chọn bản 6.x (giấy phép MS-RL), không chọn
  7.x vì bản đó đòi chấp nhận một điều khoản phí bảo trì.
- **Dịch vụ dùng chung:** không cần (không có mã ứng dụng mới ngoài một file biểu tượng). Lỗi của trình cài là lỗi của
  Windows Installer, hiện bằng hộp thoại của nó.
- **Bài học đã nhận áp dụng:** không có. **Bug liên quan:** không có.
- Không host nào: thứ chạy thật là trình cài `msiexec` trên máy này, cài vào một thư mục thử rồi gỡ; **không đụng** bản
  của Hùng nếu có (kiểm trước khi chạy).

## Rules that apply

- `CLAUDE.md`: mã nguồn theo tầng; script và WiX không phải mã ứng dụng nên không đụng ranh giới tầng.
- [SPEC release](SPEC.md): mọi dòng "Cho … →" là một kiểm tra của script `verify`; mỗi dòng F là một kiểm tra tên bắt đầu
  bằng mã.
- Agent không đẩy thẻ phát hành và không tạo release (`CLAUDE.md`): workflow chỉ tạo **bản nháp** khi Hùng đẩy thẻ.

## Decisions

- **Theo người dùng (`Scope="perUser"`)**, thư mục mặc định `%LocalAppData%\Programs\Paper.ScreenWizzard`: khớp manifest `asInvoker` và
  luật "không cần quyền quản trị" của `CLAUDE.md`. Rejected: theo máy (Program Files), vì đòi quyền quản trị.
- **Self-contained, một file, `win-x64`, không trim** — đúng dòng Gói của `CLAUDE.md` (WPF không trim được). Rejected: phụ
  thuộc .NET cài sẵn, vì SPEC nói không đòi cài .NET.
- **Mục "khởi động cùng Windows" do ứng dụng ghi, không phải trình cài**: trình cài chỉ gỡ mục đó khi gỡ (SPEC, "Cài đè và
  gỡ"). Rejected: cho trình cài ghi mục đó lúc cài, vì làm hai chỗ cùng giữ một cài đặt.
- **Không xoá `%AppData%\Paper\ScreenWizzard` khi gỡ** — cài đặt và ảnh là của người dùng (SPEC).
- **Công cụ WiX ghim ở `dotnet-tools.json` của dự án** (bản 6.0.x), nên máy nào cũng `dotnet tool restore` là dựng được và
  CI dùng đúng bản đó.

## UI wireframe

Không có cửa sổ của ứng dụng mới. Cửa sổ cài là bộ giao diện có sẵn của WiX (lời cấp phép, chọn thư mục, tiến trình,
xong); ô "Chạy ngay" ở cửa sổ cuối.

## Tasks

Nhóm duy nhất nối tiếp: mỗi bước dùng kết quả của bước trước, và bước chạy thật cần cả desktop lẫn `msiexec`.

### 1. Dựng và kiểm

- [x] T1 [red] Script kiểm `installer/verify-installer.ps1` viết **trước**: cho một file `.msi`, cài im lặng vào thư mục thử rồi kiểm từng dòng SPEC (`Cho … →`) và F1 tới F4; chạy khi chưa có file cài thì đỏ — xong khi chạy trên đường dẫn `.msi` không tồn tại thì thất bại ở kiểm tra đầu tiên với lời nêu rõ file nào thiếu {files: installer/verify-installer.ps1}
- [x] T2 Biểu tượng của ứng dụng: file `.ico` nhiều cỡ vẽ đúng hình khay (khung xanh, bốn góc, chấm giữa), gắn vào exe — xong khi `dotnet build` 0 cảnh báo và file exe có biểu tượng đọc lại được {files: src/Paper.ScreenWizzard.App/Paper.ScreenWizzard.App.csproj, src/Paper.ScreenWizzard.App/app.ico, installer/make-icon.ps1}
- [x] T3 Dựng gói: `installer/build-package.ps1` (publish một file, zip di động, `Package.wxs` dựng `.msi`, `SHA256SUMS.txt`) và `dotnet-tools.json` ghim WiX 6.0.x — xong khi chạy ra ba file đúng tên trong `artifacts/` và số phiên bản trong file cài khớp số truyền vào {files: installer/**, dotnet-tools.json, .gitignore}
- [x] T4 Chạy thật: `verify-installer.ps1` trên file cài vừa dựng, xanh cả các dòng SPEC lẫn F1 tới F4; mỗi kiểm tra ghi giá trị đọc lại — xong khi script exit 0 và số kiểm tra đã chạy > 0, rồi máy sạch lại (không còn bản thử, không còn mục Start / Run) {files: installer/verify-installer.ps1}
- [x] T5 Phát hành: `.github/workflows/release.yml` (thẻ `v*` → dựng, kiểm unit, dựng gói, chạy `verify-installer.ps1`, tạo **bản nháp** release kèm ba file), verb `package` trong profile, và cập nhật `CLAUDE.md`, `README.md`, `CODEMAP.md`, ADR 0002 — xong khi workflow đọc lại đúng cú pháp, `check-code-map` và `check-spec` sạch, và không có thẻ nào được đẩy {files: .github/workflows/release.yml, .claude/paper.profile.json, CLAUDE.md, README.md, CODEMAP.md, docs/decisions/0002-file-cai-msi-wix.md, docs/features/release/**, docs/roadmap.md}

## API đã tra

Công cụ ngoài, không phải API host: WiX 6 (`wix build`, phần mở rộng `WixToolset.UI.wixext` và `WixToolset.Util.wixext`), `msiexec`,
`dotnet publish`. Ghi ở T3 sau khi đọc trang tài liệu WiX và chạy thử; `docsSource` của profile là WPF nên không có trang Learn nào
cho phần này.

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
| T1 | `verify-installer.ps1 -Msi artifacts/nothing.msi` viết trước khi có file cài: `[FAIL] F0 the package file exists -> not found: artifacts/nothing.msi`, "the package to verify is missing", exit **1** — đỏ ở kiểm tra đầu tiên, nêu đúng file thiếu | pass |
| T2 | `make-icon.ps1` ra `app.ico` 9528 byte, 7 cỡ (16 tới 256); mở khung 256 xem: khung vuông bo góc xanh, bốn góc trắng, chấm giữa, đúng hình khay. `dotnet build` **0 Warning(s), 0 Error(s)**; `ExtractAssociatedIcon` trên exe đọc lại **32×32**. Ghi chú: `System.Drawing.Icon` không giải mã được khung PNG từ 48 trở lên (`ToBitmap` báo lỗi) nhưng trình biên dịch nhúng được và Windows đọc khung PNG | pass |
| T3 | `build-package.ps1 -Version 1.0.0` ra `…-1.0.0-win-x64.msi` **71,9 MB**, `.zip` **72,4 MB**, `SHA256SUMS.txt`; `ProductVersion` đọc lại từ bảng Property của file cài = **1.0.0**, `ProductVersion` của exe = **1.0.0**. Bốn chỗ máy trả lời khác dự đoán, đều sửa: (1) `WIX0091` khai trùng `WIXUI_INSTALLDIR`; (2) cài lần đầu ra **1603** vì điều kiện `WindowsBuild >= 18362` — Windows Installer trả **9600** và `VersionNT` 603 trên Windows 11 bản dựng 26200 — nên đọc `CurrentBuildNumber` từ registry; (3) mục Ứng dụng ghi ở **HKLM** dù cài theo người dùng, nên script kiểm đọc cả HKCU và HKLM; (4) khoá `Icon` phải kết thúc bằng `.ico` thì mới có biểu tượng sản phẩm. Công cụ: WiX **6.0.2** (7.0 đòi chấp nhận điều khoản phí, xem ADR 0002) | pass |
| T4 | mình chạy trên gói 0.1.0 (lệnh `installer/verify-installer.ps1 -Msi artifacts/Paper.ScreenWizzard-0.1.0-win-x64.msi`): **33 kiểm tra, 33 đạt, 0 hỏng**, exit 0. Đọc lại: cài exit 0 và không đòi khởi động lại; exe 0.1.0 ở thư mục chọn; lối tắt Start; Ứng dụng có **một** mục 0.1.0 và biểu tượng sản phẩm; chạy từ cửa sổ thường (không nâng quyền); cài 0.1.1 khi app đang chạy → app tắt, exit 0, **một** mục 0.1.1, exe 0.1.1; cài 0.0.99 → **1603**, vẫn 0.1.1; cài lại đúng file → exit 0, một mục; gỡ khi app chạy → exit 0; sau gỡ: thư mục cài, lối tắt, mục Ứng dụng, mục khởi động cùng Windows đều **không còn**, file của người dùng ở `%AppData%\Paper\ScreenWizzard` **còn**; thư mục không tạo được → **1603**, không để lại gì; zip giải nén chạy được, chỉ có exe và giấy phép; `SHA256SUMS.txt` khớp cả hai file. Các lần chạy đỏ trước đó: 1603 vì WindowsBuild, hàng xóm phiên bản sai vì script tính lại `$Version`, mục Ứng dụng không thấy vì chỉ đọc HKCU và vì một mục đơn bị `.Count` trả rỗng, F3 bằng ACL không đỏ vì dịch vụ cài ghi bằng quyền hệ thống (đổi sang "cha là một file"). **Máy sạch lại** sau chạy: 0 lối tắt, 0 mục Ứng dụng, 0 giá trị Run, 0 tiến trình, 0 thư mục thử, 0 file đánh dấu, 0 sản phẩm đăng ký. **Không chứng minh được bằng chạy thật:** F1 (Windows cũ hơn 1903, không có máy): kiểm bằng đọc điều kiện khởi chạy và câu báo trong file cài | pass |
| T5 | `release.yml` chạy tay hai lần trên GitHub (`workflow_dispatch`, phiên bản 0.0.1): lần một **đỏ đúng chỗ** ở bước kiểm file cài (`Cannot find path 'HKCU:\…\Run' because it does not exist` — runner không có khoá đó), sửa script, lần hai **xanh cả 8 bước**: build Release, unit, dựng gói, `verify-installer.ps1` **32/32** trên runner (chạy nâng quyền nên bớt kiểm tra "không cần quyền quản trị"), tải lên artifact, bước "Draft release" **bị bỏ qua** như thiết kế (chỉ chạy khi đẩy thẻ). `gh release list` rỗng và `git tag` rỗng: **không có thẻ nào được đẩy, không release nào được tạo**. Verb `package` chạy qua `paperflow.ps1 package` exit 0 (ra `0.1.0`); `verify-package` không phải tên verb của kit (kit chỉ nhận build, test, ui, e2e, live, publish, package, …) nên bỏ khỏi profile. `check-spec` **4 SPEC.md, 0 problems**; `check-code-map` **6 CODEMAP.md, 0 stale**. Đã cập nhật `CLAUDE.md` (mục Deploy), `README.md`, `CODEMAP.md`, `docs/roadmap.md`, ADR 0002 `Proposed` | pass |
