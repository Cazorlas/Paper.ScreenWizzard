# Paper.ScreenWizzard file cài đặt — plan — 2026-09-21

**Trạng thái:** đã duyệt 2026-09-21 (Hùng: "setup đầy đủ file cài lun nha")
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

- [ ] T1 [red] Script kiểm `installer/verify-installer.ps1` viết **trước**: cho một file `.msi`, cài im lặng vào thư mục thử rồi kiểm từng dòng SPEC (`Cho … →`) và F1 tới F4; chạy khi chưa có file cài thì đỏ — xong khi chạy trên đường dẫn `.msi` không tồn tại thì thất bại ở kiểm tra đầu tiên với lời nêu rõ file nào thiếu {files: installer/verify-installer.ps1}
- [ ] T2 Biểu tượng của ứng dụng: file `.ico` nhiều cỡ vẽ đúng hình khay (khung xanh, bốn góc, chấm giữa), gắn vào exe — xong khi `dotnet build` 0 cảnh báo và file exe có biểu tượng đọc lại được {files: src/Paper.ScreenWizzard.App/Paper.ScreenWizzard.App.csproj, src/Paper.ScreenWizzard.App/app.ico, installer/make-icon.ps1}
- [ ] T3 Dựng gói: `installer/build-package.ps1` (publish một file, zip di động, `Package.wxs` dựng `.msi`, `SHA256SUMS.txt`) và `dotnet-tools.json` ghim WiX 6.0.x — xong khi chạy ra ba file đúng tên trong `artifacts/` và số phiên bản trong file cài khớp số truyền vào {files: installer/**, dotnet-tools.json, .gitignore}
- [ ] T4 Chạy thật: `verify-installer.ps1` trên file cài vừa dựng, xanh cả các dòng SPEC lẫn F1 tới F4; mỗi kiểm tra ghi giá trị đọc lại — xong khi script exit 0 và số kiểm tra đã chạy > 0, rồi máy sạch lại (không còn bản thử, không còn mục Start / Run) {files: installer/verify-installer.ps1}
- [ ] T5 Phát hành: `.github/workflows/release.yml` (thẻ `v*` → dựng, kiểm unit, dựng gói, chạy `verify-installer.ps1`, tạo **bản nháp** release kèm ba file), verb `package` trong profile, và cập nhật `CLAUDE.md`, `README.md`, `CODEMAP.md`, ADR 0002 — xong khi workflow đọc lại đúng cú pháp, `check-code-map` và `check-spec` sạch, và không có thẻ nào được đẩy {files: .github/workflows/release.yml, .claude/paper.profile.json, CLAUDE.md, README.md, CODEMAP.md, docs/decisions/0002-file-cai-msi-wix.md, docs/features/release/**, docs/roadmap.md}

## API đã tra

Công cụ ngoài, không phải API host: WiX 6 (`wix build`, phần mở rộng `WixToolset.UI.wixext` và `WixToolset.Util.wixext`), `msiexec`,
`dotnet publish`. Ghi ở T3 sau khi đọc trang tài liệu WiX và chạy thử; `docsSource` của profile là WPF nên không có trang Learn nào
cho phần này.

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
