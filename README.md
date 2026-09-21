# Paper.ScreenWizzard

Ứng dụng Windows để chụp màn hình và sửa ảnh; quay màn hình và ghi chú lên màn hình là các đợt sau.
Nằm ở khay hệ thống, gọi bằng phím tắt hoặc thanh chụp nổi.

## Đã có (đợt 1, xong 2026-09-21)

- **Chụp:** vùng chữ nhật, vùng tự do, cửa sổ, toàn màn hình; độ trễ, con trỏ, nhiều màn hình và DPI khác nhau.
  Chụp xong hiện hộp "Đã chụp" (Lưu, Lưu thành…, Sao chép, Sửa, Bỏ) hoặc đi thẳng tới nơi đã chọn.
- **Sửa ảnh:** bút, dạ quang, đường, mũi tên, khung, elip, chữ, số bước, làm mờ, cắt; hoàn tác, chọn và kéo hình,
  lưu PNG hoặc JPG, sao chép.
- **Khung:** khay, phím tắt đổi được (mặc định PrintScreen và các phím kèm Shift, Alt, Ctrl), cài đặt, khởi động cùng
  Windows, tiếng Việt và tiếng Anh, giao diện sáng và tối, dùng được bằng bàn phím.

## Đọc tiếp

- Yêu cầu: [khung](docs/features/shell/SPEC.md), [chụp](docs/features/capture/SPEC.md), [sửa ảnh](docs/features/editor/SPEC.md)
- Việc đã làm và bằng chứng: [plan đợt 1](docs/features/shell/2026-09-20-m1-chup-va-sua-anh-plan.md)
- Kiến trúc: [ADR 0001](docs/decisions/0001-clean-architecture.md) (còn `Proposed`, chờ chủ dự án), [CLAUDE.md](CLAUDE.md), [CODEMAP.md](CODEMAP.md)
- Lộ trình: [docs/roadmap.md](docs/roadmap.md)

## Cài đặt

Tải bản mới nhất ở mục Releases của repo: `Paper.ScreenWizzard-<số phiên bản>-win-x64-Setup.exe` (bấm đúp, tiếng Việt hoặc tiếng Anh theo Windows,
không cần quyền quản trị, không cần cài .NET) hoặc `….zip` (bản di động, giải nén ở đâu cũng chạy). Gỡ bằng Cài đặt của Windows, Ứng dụng; cài đặt và ảnh
của bạn ở `%AppData%\Paper\ScreenWizzard` được giữ lại. Chưa ký số nên Windows SmartScreen có thể hiện hộp xanh lần đầu: bấm Thông tin thêm, rồi Vẫn chạy.
Luật đầy đủ: [SPEC file cài](docs/features/release/SPEC.md).

## Dựng và thử

Cần .NET 10 SDK trên Windows 10 1903 trở lên.

```
dotnet build Paper.ScreenWizzard.slnx
dotnet test tests/Paper.ScreenWizzard.UnitTests     # luật, không cần Windows
dotnet test tests/Paper.ScreenWizzard.UiTests       # cửa sổ thật trên dữ liệu giả, lái bằng FlaUI
dotnet test tests/Paper.ScreenWizzard.E2eTests      # adapter thật và chính file exe; dùng chuột và bàn phím thật
```

Dựng file cài, ba cách (đều cần .NET 10 SDK):

- **Bấm đúp** `installer/Build-Installer.cmd` (thêm số phiên bản nếu muốn: `Build-Installer.cmd 1.2.3`): ra `Setup.exe`, bản zip và
  `SHA256SUMS.txt` ở `artifacts/`. Bộ biên dịch Inno Setup tự được tải vào `.tools/`, không cần cài Inno Setup.
- **Mở `installer/Setup.iss` bằng Inno Setup** (đã thử với 6.7.3 và 7.1.0) rồi bấm Compile (Ctrl+F9): đủ để ra `Setup.exe` ở `artifacts/`; nếu app chưa được
  publish thì chính file này publish trước. Đổi `Version` ở đầu file khi ra bản khác.
- **Dòng lệnh:**

```
powershell -File installer/build-package.ps1 -Version 1.0.0          # ra artifacts/: Setup.exe, .zip, SHA256SUMS.txt (cùng việc với Build-Installer.cmd)
powershell -File installer/verify-installer.ps1 -Setup artifacts/Paper.ScreenWizzard-1.0.0-win-x64-Setup.exe   # cài, cài đè, gỡ thật
```

Hai bộ test cuối điều khiển màn hình thật: đóng bản Paper.ScreenWizzard đang chạy trước khi chạy chúng. CI (GitHub Actions) dựng
bản Release và chạy bộ unit; `release.yml` dựng và kiểm file cài khi đẩy thẻ `vX.Y.Z` (chi tiết ở mục Deploy của CLAUDE.md).
