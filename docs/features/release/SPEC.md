> Đã làm xong 2026-09-21 theo lời Hùng ("setup đầy đủ file cài lun nha"); đổi sang Setup.exe hai ngôn ngữ cùng ngày theo lời Hùng ("giao diện trình cài làm 2 thứ tiếng dc ko"). Việc làm và bằng chứng: [2026-09-21-file-cai-plan.md](2026-09-21-file-cai-plan.md)

# File cài đặt và phát hành — SPEC

Người dùng tải một file cài, bấm đúp là dùng được, gỡ được sạch bằng Cài đặt của Windows. Phần phát hành là chỗ
duy nhất tạo ra file cài; chủ dự án là người đẩy thẻ phát hành.

## User story

Là người dùng máy Windows, tôi muốn một file cài đặt bấm là chạy, bằng tiếng Việt nếu máy tôi dùng tiếng Việt, không đòi
quyền quản trị và không đòi cài .NET, để dùng được ngay mà không phải chép file bằng tay; khi không cần nữa thì gỡ được sạch,
còn ảnh và cài đặt của tôi vẫn còn nếu tôi cài lại.

## What the user does

1. Tải `Paper.ScreenWizzard-<số phiên bản>-win-x64-Setup.exe`, bấm đúp. Cửa sổ cài hiện bằng **tiếng Việt hay tiếng
   Anh theo ngôn ngữ hiển thị của Windows** (Windows dùng ngôn ngữ khác thì hiện một hộp chọn giữa hai thứ tiếng). Có lời
   cấp phép, chỗ chọn thư mục (mặc định nằm trong thư mục riêng của người dùng), rồi nút Cài đặt.
2. Cài xong, có mục **Paper.ScreenWizzard** trong menu Start, và ô "Chạy Paper.ScreenWizzard ngay" đã được chọn sẵn ở
   cửa sổ cuối; chạy ngay thì biểu tượng hiện ở khay như lúc mở tay.
3. Muốn dùng bản mới: bấm đúp file cài của bản mới. Nó thay bản cũ tại chỗ; cài đặt, ảnh đã lưu và việc "khởi động
   cùng Windows" vẫn như cũ.
4. Muốn gỡ: Cài đặt của Windows, Ứng dụng, Paper.ScreenWizzard, Gỡ cài đặt.
5. Ai không muốn cài thì tải bản **di động** `Paper.ScreenWizzard-<số phiên bản>-win-x64.zip`, giải nén ở đâu cũng
   chạy được, không ghi gì ngoài thư mục cài đặt của ứng dụng.
6. Cạnh hai file có một file `SHA256SUMS.txt` để so mã kiểm tra.

## Inputs

| Thứ | Giá trị |
| --- | --- |
| Số phiên bản | ba số `x.y.z`, lấy từ tên thẻ phát hành `v<x.y.z>`; dựng thử ở máy thì tự đặt |
| Thư mục cài | mặc định `%LocalAppData%\Programs\Paper.ScreenWizzard`; người dùng đổi được ở cửa sổ cài |
| Ngôn ngữ cài | theo Windows; ai muốn chọn khác thì bấm chọn ở hộp ngôn ngữ (chỉ hiện khi Windows dùng ngôn ngữ khác hai thứ tiếng trên) |

## Outputs

- Một file cài (`Setup.exe`) cho Windows 10 và 11 bản 64 bit, tự chứa .NET, không cần cài gì thêm.
- Một file zip di động cùng nội dung.
- Một file mã kiểm tra SHA-256 cho cả hai.
- Sau khi cài: thư mục cài có file chạy; mục menu Start; mục trong danh sách Ứng dụng của Windows với tên, số phiên
  bản và biểu tượng của ứng dụng.
- Biểu tượng của ứng dụng (file chạy, menu Start, khay, danh sách Ứng dụng, cửa sổ cài) là **một hình**: tờ giấy gập góc có
  đường kẻ và ê-ke của người làm kỹ thuật, nằm trong bốn góc khung ngắm chụp màn hình, trên nền xanh.

## Key entities

- **Bản cài:** một lần cài của một số phiên bản cho một người dùng Windows.
- **Bản di động:** cùng file chạy, không có bản cài.

## Cài đè và gỡ

**Cài bản mới lên bản cũ thay bản cũ, không tạo hai bản.**

- Cho bản 1.0.0 đang cài, cài file 1.1.0 → chỉ còn **một** mục trong danh sách Ứng dụng, số phiên bản là **1.1.0**,
  file chạy là của 1.1.0.
- Cho bản 1.1.0 đang cài, cài file 1.0.0 → **bị từ chối** và nói rõ đã có bản mới hơn; không có gì bị đổi.
- Cho bản 1.0.0 đang cài, cài lại đúng file 1.0.0 → cài xong, vẫn một mục.

**Ứng dụng đang chạy không làm cài hay gỡ thất bại.**

- Cho ứng dụng đang chạy ở khay, cài bản mới hay gỡ → ứng dụng **tự được tắt**, việc cài hay gỡ xong, và **không đòi
  khởi động lại Windows**.

**Gỡ là gỡ sạch phần của ứng dụng, giữ phần của người dùng.**

- Sau khi gỡ: thư mục cài **không còn**, mục menu Start **không còn**, mục "khởi động cùng Windows" (nếu người dùng
  đã bật) **không còn**.
- Sau khi gỡ, thư mục cài đặt của ứng dụng (`%AppData%\Paper\ScreenWizzard`) và ảnh người dùng đã lưu **vẫn còn**, để
  cài lại là dùng tiếp.

## Edge cases

- Cài trên Windows cũ hơn Windows 10 bản 1903 → không cài, nói rõ cần bản nào (bằng ngôn ngữ của cửa sổ cài).
- Thư mục cài đã chọn không ghi được → trình cài báo lỗi và không cài dở.
- Hai người dùng trên cùng máy: mỗi người cài riêng cho mình, không ảnh hưởng nhau.

## When it does not do the job

| Mã | Khi nào | Người dùng thấy |
| --- | --- | --- |
| F1 | Windows cũ hơn Windows 10 bản 1903 | trình cài dừng ở đầu, nói cần Windows 10 bản 1903 trở lên |
| F2 | đã có bản mới hơn | trình cài dừng, nói đã có bản mới hơn, không đổi gì |
| F3 | thư mục cài không ghi được | trình cài báo lỗi, không để lại gì đã cài dở |
| F4 | ứng dụng đang chạy lúc cài hay gỡ | ứng dụng tự tắt, không hỏi khởi động lại Windows |

## Assumptions

- Cài theo người dùng, không theo máy: không đòi quyền quản trị, không ghi vào Program Files. Bù lại người quản trị
  máy không cài một lần cho mọi người dùng được.
- **Chưa ký số:** Windows SmartScreen có thể hiện hộp xanh "Windows đã bảo vệ PC của bạn" lần đầu tải và chạy file cài; bấm
  Thông tin thêm, rồi Vẫn chạy. Ký số cần một chứng chỉ (xem ADR về file cài).
- Việc phát hành do chủ dự án bắt đầu: đẩy thẻ `v<x.y.z>` thì bản dựng tự chạy và tạo bản nháp trên GitHub. Agent chỉ đẩy thẻ
  và bấm Publish khi chủ dự án nói trong phiên đó.
- Bản dịch tiếng Việt của cửa sổ cài là bản cộng đồng đi kèm công cụ Inno Setup; câu chữ riêng của dự án (báo bản mới hơn, ô
  "Chạy ngay") do dự án viết.

## What it does not do yet

- Ký số file chạy và file cài.
- Tự cập nhật trong ứng dụng.
- Cài cho mọi người dùng của máy (cần quyền quản trị).
- Bản cho Windows trên ARM.
- Điều kiện Windows 10 bản 1903 (F1) được kiểm bằng cách đọc thiết lập trong kịch bản cài, không chạy thật trên Windows cũ
  (không có máy).
