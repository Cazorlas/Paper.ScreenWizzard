# ADR-0002: File cài đặt là MSI theo người dùng, dựng bằng WiX 6

## Status

Proposed, chờ chủ dự án duyệt. Hùng yêu cầu làm file cài ("setup đầy đủ file cài lun nha", 2026-09-21); công nghệ cụ thể là
lựa chọn của agent, nên nó chưa phải quyết định của chủ dự án cho tới khi Hùng duyệt. Agent không tự ghi `Accepted`.

## Date

Dự thảo 2026-09-21.

## Context

Ứng dụng là một exe WPF `win-x64`, tự chứa .NET, không quyền quản trị (`CLAUDE.md`, mục Deploy). Người dùng cần một file cài bấm là
chạy, cài đè để nâng cấp, gỡ sạch bằng Cài đặt của Windows, và một bản zip di động cho ai không muốn cài. Phần dựng phải chạy được
bằng lệnh ở máy người làm và trên runner GitHub, và phải kiểm được bằng cách cài thật.

## Decision

1. **Định dạng: MSI theo người dùng** (`Scope="perUser"`, thư mục mặc định `%LocalAppData%\Programs\Paper.ScreenWizzard`, lối tắt
   trong menu Start của người dùng). Không quyền quản trị.
2. **Công cụ: WiX 6.0.2**, ghim trong `dotnet-tools.json` (`dotnet tool restore`) cùng hai phần mở rộng (UI, Util) cài bằng
   `dotnet wix extension add`. Nguồn cài là một file `installer/Package.wxs`.
3. **Số phiên bản là tên thẻ phát hành** `vX.Y.Z`; MSI, exe và mục trong Ứng dụng cùng mang số đó.
4. **Gỡ giữ phần của người dùng:** cài đặt và ảnh ở `%AppData%\Paper\ScreenWizzard` không bị xoá; mục "khởi động cùng Windows" do
   ứng dụng ghi thì trình gỡ xoá.
5. **Ứng dụng đang chạy bị tắt** (`CloseApplication` với `TerminateProcess`), vì nó chỉ có biểu tượng khay, không có cửa sổ để
   đóng nhẹ nhàng; không đòi khởi động lại Windows.
6. **Cửa kiểm Windows 10 bản 1903 đọc số bản dựng từ registry**, không từ thuộc tính `WindowsBuild` của Windows Installer: đo trên
   Windows 11 bản dựng 26200 thuộc tính đó trả 9600 (Windows 8.1).
7. **Phát hành do chủ dự án bấm:** đẩy thẻ thì `release.yml` dựng, kiểm bằng `installer/verify-installer.ps1` (cài, chạy, cài đè,
   hạ bản, sửa chữa, gỡ, trên runner) và tạo **bản nháp**; agent không đẩy thẻ, không bấm Publish.

## Alternatives considered

- **Inno Setup:** file `Setup.exe` gọn, cài theo người dùng dễ, nhưng đòi cài công cụ ngoài `dotnet` (trên máy người làm và runner)
  và không phải chuẩn MSI mà người quản trị máy hay cần. Loại vì thêm một công cụ phải cài, không vì kém.
- **MSIX:** cài sạch nhất, nhưng đòi ký số bằng chứng chỉ tin cậy mới cài được ngoài Store; chưa có chứng chỉ. Loại cho tới khi
  có chứng chỉ; đổi sang MSIX khi đó là một ADR thay thế.
- **WiX 7:** bản mới nhất, nhưng đòi chấp nhận điều khoản phí bảo trì khi dùng thương mại; 6.x giữ giấy phép MS-RL. Loại tới khi
  chủ dự án quyết về điều khoản đó.
- **Chỉ zip di động:** không cài đè, không mục trong Ứng dụng, không gỡ. Vẫn giữ làm bản phụ.

## Consequences

- MSI của Windows Installer ghi mục Ứng dụng ở nhánh máy (`HKLM`) dù cài theo người dùng (đo trên máy này); vì vậy script kiểm
  đọc cả hai nhánh, và biểu tượng trong Ứng dụng là biểu tượng sản phẩm đã đăng ký chứ không phải giá trị `DisplayIcon`.
- Chưa ký số: SmartScreen có thể hỏi ở lần chạy đầu (SPEC release, Assumptions).
- Giao diện cài chỉ có tiếng Anh ở đợt này.
- Người quản trị máy không cài một lần cho mọi người dùng được (theo người dùng).
- Mỗi lần đổi số phiên bản là một sản phẩm mới với mã sản phẩm mới, nhưng cùng mã nâng cấp (`UpgradeCode` cố định), nên cài đè thay
  bản cũ.
