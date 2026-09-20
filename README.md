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

## Dựng và thử

Cần .NET 10 SDK trên Windows 10 1903 trở lên.

```
dotnet build Paper.ScreenWizzard.slnx
dotnet test tests/Paper.ScreenWizzard.UnitTests     # luật, không cần Windows
dotnet test tests/Paper.ScreenWizzard.UiTests       # cửa sổ thật trên dữ liệu giả, lái bằng FlaUI
dotnet test tests/Paper.ScreenWizzard.E2eTests      # adapter thật và chính file exe; dùng chuột và bàn phím thật
```

Hai bộ cuối điều khiển màn hình thật: đóng bản Paper.ScreenWizzard đang chạy trước khi chạy chúng. CI (GitHub Actions) dựng
bản Release và chạy bộ unit. Chưa có bản phát hành: xem mục Deploy của CLAUDE.md.
