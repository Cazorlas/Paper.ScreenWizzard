# ADR-0002: File cài là Setup.exe theo người dùng, dựng bằng Inno Setup

## Status

Proposed, chờ chủ dự án duyệt. Hùng yêu cầu làm file cài ("setup đầy đủ file cài lun nha", 2026-09-21) và giao diện hai thứ
tiếng; công nghệ cụ thể là lựa chọn của agent, nên chưa phải quyết định của chủ dự án cho tới khi Hùng duyệt. Agent không tự ghi
`Accepted`.

Lịch sử: bản đầu của ADR này chọn MSI dựng bằng WiX 6 (đã dựng, cài thử thật, chạy được trên GitHub). Khi Hùng hỏi có làm được
giao diện hai thứ tiếng không, WiX cho thấy giới hạn: bộ giao diện của nó không có tiếng Việt (phải tự dịch khoảng hai trăm
chuỗi) và một file MSI không tự chọn ngôn ngữ theo máy nếu không thêm một chương trình khởi động. Nên đổi sang Inno Setup **trước
khi có bản phát hành nào**, nên không ai đã cài bản MSI.

## Date

Dự thảo 2026-09-21.

## Context

Ứng dụng là một exe WPF `win-x64`, tự chứa .NET, không quyền quản trị (`CLAUDE.md`, mục Deploy). Người dùng cần một file cài bấm là
chạy, hiện bằng tiếng Việt hay tiếng Anh theo máy, cài đè để nâng cấp, gỡ sạch bằng Cài đặt của Windows, và một bản zip di động
cho ai không muốn cài. Phần dựng phải chạy được bằng lệnh ở máy người làm và trên runner GitHub, và phải kiểm được bằng cách
cài thật.

## Decision

1. **Định dạng: một `Setup.exe` theo người dùng** (`PrivilegesRequired=lowest`, thư mục mặc định
   `%LocalAppData%\Programs\Paper.ScreenWizzard`, lối tắt trong menu Start của người dùng, mục Ứng dụng ở nhánh registry của người
   dùng). Không quyền quản trị.
2. **Công cụ: Inno Setup 6.7.3.** Kịch bản là `installer/Setup.iss`. Bộ biên dịch được `installer/get-inno.ps1` tải về (ghim
   theo số phiên bản và SHA-256, chữ ký phải hợp lệ) và cài vào `.tools/inno` trong dự án, rồi gỡ khỏi máy các mục nó tự thêm
   (mục Ứng dụng, liên kết file `.iss`); nên ở máy nào cũng dựng được mà không cài gì lên máy.
3. **Hai ngôn ngữ, tiếng Anh và tiếng Việt,** chọn theo ngôn ngữ hiển thị của Windows. Bản dịch tiếng Việt là
   `installer/Languages/Vietnamese.isl` của cộng đồng (lấy từ kho của chính Inno Setup); câu chữ riêng của dự án nằm trong
   `[CustomMessages]`.
4. **Số phiên bản là tên thẻ phát hành** `vX.Y.Z`; file cài, exe và mục trong Ứng dụng cùng mang số đó.
5. **Gỡ giữ phần của người dùng:** cài đặt và ảnh ở `%AppData%\Paper\ScreenWizzard` không bị xoá; mục "khởi động cùng Windows"
   do ứng dụng ghi thì trình gỡ xoá.
6. **Ứng dụng đang chạy bị tắt** (`taskkill` trong kịch bản cài và gỡ), vì nó chỉ có biểu tượng khay, không có cửa sổ để đóng
   nhẹ nhàng; không đòi khởi động lại Windows.
7. **Windows cũ hơn 10 bản 1903 bị từ chối** bằng `MinVersion`, với câu báo có sẵn của Inno trong ngôn ngữ của cửa sổ cài.
8. **Một hình biểu tượng, một nguồn:** `src/Paper.ScreenWizzard.App/app.ico`, vẽ bằng `installer/make-icon.ps1`. Exe, menu Start,
   danh sách Ứng dụng, cửa sổ cài và biểu tượng khay đều dùng nó (khay đọc biểu tượng của chính file exe).
9. **Phát hành do chủ dự án bắt đầu:** đẩy thẻ thì `release.yml` dựng, kiểm bằng `installer/verify-installer.ps1` (cài, chạy, cài đè,
   hạ bản, cài lại, gỡ, trên runner) và tạo **bản nháp**.

## Alternatives considered

- **MSI bằng WiX 6** (bản đầu của ADR này): chuẩn MSI mà người quản trị máy hay cần, kiểm được kỹ; loại vì giao diện không có
  tiếng Việt, không tự chọn ngôn ngữ, và ghi mục Ứng dụng ở nhánh máy dù cài theo người dùng. WiX 7 đòi chấp nhận điều khoản
  phí bảo trì nên không dùng.
- **MSIX:** cài sạch nhất, nhưng đòi ký số bằng chứng chỉ tin cậy mới cài được ngoài Store; chưa có chứng chỉ.
- **NUKE** (nuke.build): là bộ điều phối dựng bằng C#, không tự làm ra file cài; nó chỉ gọi lại đúng những bước này. Chưa cần
  cho một script dựng ngắn; đáng cân nhắc khi dự án có nhiều dự án dựng và nhiều bước hơn.
- **Chỉ zip di động:** không cài đè, không mục trong Ứng dụng, không gỡ. Vẫn giữ làm bản phụ.

## Consequences

- Inno Setup miễn phí kể cả thương mại; bản dịch tiếng Việt là của cộng đồng nên câu chữ có thể cần chỉnh.
- Chưa ký số: SmartScreen có thể hiện cảnh báo ở lần chạy đầu (xem SPEC file cài). Ký số sẽ là một quyết định riêng khi có
  chứng chỉ hay chương trình ký miễn phí cho mã nguồn mở.
- Không có MSI: người quản trị máy muốn triển khai hàng loạt dùng được `Setup.exe /VERYSILENT` nhưng không có bản MSI chuẩn.
- Kịch bản kiểm chạy được ở cửa sổ thường, nên chứng minh được "không cần quyền quản trị"; trên runner GitHub (chạy nâng quyền)
  phần đó chỉ được ghi chú, không tính là đạt.
