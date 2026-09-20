> Bản nháp chờ duyệt 2026-09-20. Đây là yêu cầu, viết trước khi có code. Việc làm và bằng chứng: [2026-09-20-m1-chup-va-sua-anh-plan.md](2026-09-20-m1-chup-va-sua-anh-plan.md)

# Khung ứng dụng — SPEC

Ứng dụng chạy ẩn ở khay hệ thống, luôn sẵn sàng bằng phím tắt, có một thanh chụp gọn để bấm chuột, và một cửa sổ cài
đặt. Đây là chỗ mọi tính năng khác (chụp, sửa ảnh, sau này quay và ghi chú) đứng lên.

## User story

Là người dùng máy cả ngày, tôi muốn công cụ chụp màn hình luôn chờ sẵn mà không chiếm chỗ trên thanh tác vụ, bật lên
bằng một phím, và tôi tự đặt được phím, thư mục lưu, và việc gì xảy ra sau mỗi lần chụp.

## What the user does

1. Mở ứng dụng. Biểu tượng hiện ở khay hệ thống, thanh chụp hiện lên (lần đầu). Không có cửa sổ chính chiếm màn hình.
2. Bấm chuột phải biểu tượng khay: menu có Chụp vùng chữ nhật, Chụp vùng tự do, Chụp cửa sổ, Chụp toàn màn hình,
   Mở ảnh…, Thanh chụp (hiện hoặc ẩn), Cài đặt, Thoát. Bấm đúp biểu tượng thì hiện thanh chụp.
3. Thanh chụp là một cửa sổ nhỏ luôn nằm trên cùng, có một nút cho mỗi kiểu chụp và nút Cài đặt. Kéo nó đi đâu cũng
   được; lần sau mở lại nó ở đúng chỗ đó.
4. Trong Cài đặt, người dùng đổi: phím tắt của từng kiểu chụp, việc làm sau khi chụp, thư mục lưu, định dạng file, độ
   trễ, kèm con trỏ, khởi động cùng Windows, ngôn ngữ (tự theo Windows, tiếng Việt, tiếng Anh), giao diện (tự theo
   Windows, sáng, tối).
5. Đóng thanh chụp hay cửa sổ sửa bằng nút X chỉ đóng cửa sổ đó; ứng dụng vẫn chạy ở khay. Chỉ "Thoát" trong menu khay
   mới tắt ứng dụng.

## Inputs

| Người dùng chọn | Mặc định | Quyết định điều gì |
| --- | --- | --- |
| Phím tắt vùng chữ nhật | PrintScreen | bấm phím này thì bắt đầu chụp vùng chữ nhật |
| Phím tắt vùng tự do | Shift+PrintScreen | chụp vùng tự do |
| Phím tắt cửa sổ | Alt+PrintScreen | chụp cửa sổ |
| Phím tắt toàn màn hình | Ctrl+PrintScreen | chụp toàn màn hình |
| Việc làm sau khi chụp | hiện hộp thoại "Đã chụp" | hộp thoại có nút chọn, hay tự đi thẳng tới một nơi; xem SPEC chụp màn hình |
| Thư mục lưu | thư mục Ảnh, mục Paper.ScreenWizzard | xem SPEC chụp màn hình |
| Định dạng file, chất lượng JPG | PNG, 90 | xem SPEC chụp màn hình |
| Độ trễ, kèm con trỏ, toàn màn hình lấy | 0 giây, tắt, màn hình có con trỏ | xem SPEC chụp màn hình |
| Khởi động cùng Windows | tắt | ứng dụng tự chạy (ẩn ở khay) khi đăng nhập |
| Ngôn ngữ | theo Windows | chữ trên mọi cửa sổ |
| Giao diện | theo Windows | sáng hay tối |

## Outputs

- Biểu tượng khay, menu khay, thanh chụp, cửa sổ Cài đặt.
- Tập tin cài đặt của người dùng: mọi thay đổi ở Cài đặt được giữ lại cho lần mở sau.
- Khi bật "Khởi động cùng Windows", ứng dụng có mặt trong danh sách khởi động của người dùng; tắt thì gỡ khỏi đó.

## Key entities

Biểu tượng khay, thanh chụp, cài đặt, phím tắt, phiên bản đang chạy của ứng dụng.

## Chỉ một bản chạy

- Cho ứng dụng đã chạy, mở ứng dụng lần nữa → **không có bản thứ hai**; bản đầu hiện thanh chụp lên trước
- Cho bấm X trên thanh chụp → ứng dụng **vẫn có mặt ở khay** và phím tắt vẫn chụp được

## Phím tắt

**Phím tắt phải đủ an toàn để không cướp việc gõ chữ.**

- Cho đặt một chữ cái đơn, ví dụ A, không kèm phím nào → **bị từ chối**, phím tắt cũ giữ nguyên
- Cho đặt Ctrl+Shift+A → **nhận**
- Cho đặt PrintScreen một mình, hoặc F9 một mình → **nhận** (phím chức năng và PrintScreen được phép đứng riêng)
- Cho đặt phím trùng với phím của kiểu chụp khác trong ứng dụng → **bị từ chối**, nêu tên kiểu đang giữ phím đó
- Cho đặt phím mà Windows hay ứng dụng khác đã giữ → **bị từ chối**, nêu rõ không đăng ký được, phím cũ giữ nguyên

**Đổi phím có hiệu lực ngay, không cần khởi động lại.**

- Cho đổi phím vùng chữ nhật từ PrintScreen sang Ctrl+Shift+1 → bấm PrintScreen **không còn** chụp, bấm Ctrl+Shift+1
  **chụp**

## Cài đặt được giữ lại

- Cho đổi thư mục lưu rồi đóng ứng dụng, mở lại → thư mục lưu **vẫn là thư mục mới**
- Cho kéo thanh chụp tới một chỗ rồi đóng, mở lại → thanh chụp **ở đúng chỗ đó**; nếu chỗ đó nay nằm ngoài mọi màn
  hình (đã rút màn hình) thì thanh chụp hiện ở **góc trên phải màn hình chính**
- Cho tập tin cài đặt bị hỏng (không đọc được) → ứng dụng **vẫn chạy với cài đặt mặc định**, giữ lại bản hỏng với đuôi
  .bak, và hiện thông báo nói rõ điều đó

## Khởi động cùng Windows

- Cho bật rồi đăng nhập lại Windows → ứng dụng **tự chạy ẩn ở khay**, không mở cửa sổ nào
- Cho tắt → ứng dụng **không** chạy khi đăng nhập

## Ngôn ngữ và giao diện

- Cho ngôn ngữ "theo Windows" trên máy Windows tiếng Việt → mọi chữ trên cửa sổ **bằng tiếng Việt**; trên máy tiếng
  khác → **tiếng Anh**
- Cho đổi ngôn ngữ trong Cài đặt → chữ đổi **ngay**, không khởi động lại
- Cho giao diện tối → chữ và biểu tượng vẫn đọc được: tỉ lệ tương phản chữ **từ 4.5:1**, biểu tượng và viền **từ 3:1**,
  ở cả giao diện sáng lẫn tối

## Dùng được bằng bàn phím

- Cho Tab qua các nút của thanh chụp và Cài đặt → thứ tự **theo thứ tự nhìn thấy**, nút đang chọn có viền rõ
- Cho Enter trong Cài đặt → **lưu và đóng**; Esc → **đóng, bỏ thay đổi chưa lưu**
- Mọi nút chỉ có biểu tượng đều có **tên** để trình đọc màn hình đọc được

## Edge cases

- Ứng dụng luôn ở khay khi chạy; nó không hiện trên thanh tác vụ (trừ khi cửa sổ sửa hay Cài đặt đang mở).
- Đổi ngôn ngữ, giao diện, phím tắt, thư mục có hiệu lực **ngay** khi bấm Lưu ở Cài đặt.

## When it does not do the job

| # | Case | What the user sees |
| --- | --- | --- |
| F1 | tập tin cài đặt hỏng | ứng dụng chạy với mặc định; thông báo nói cài đặt cũ bị hỏng và đã giữ bản .bak |
| F2 | không ghi được tập tin cài đặt (chỉ đọc, hết chỗ) | thông báo nêu đường dẫn và lý do; ứng dụng vẫn chạy và dùng cài đặt trong phiên này; thay đổi mất khi thoát và **thông báo nói vậy** |
| F3 | không ghi được mục khởi động cùng Windows | công tắc **trở về trạng thái cũ**, thông báo nêu lý do |
| F4 | phím tắt đặt mới bị từ chối | phím cũ **vẫn hoạt động**; thông báo nêu lý do (chữ đơn, trùng trong ứng dụng, hoặc ứng dụng khác đang giữ) |
| F5 | thư mục lưu nhập vào không tồn tại | Cài đặt hỏi có tạo thư mục không; không đồng ý thì **không lưu** giá trị đó và thư mục cũ giữ nguyên |
| F6 | mở ứng dụng lần hai | không có bản mới; bản đầu hiện thanh chụp; không có thông báo lỗi |
| F7 | không đăng ký được một phím tắt lúc khởi động | thông báo nêu phím nào; ứng dụng và các phím khác vẫn chạy |

## Assumptions

- Chưa có nút hay menu cho quay màn hình và ghi chú: chỉ hiện thứ đã chạy được, không để nút giả.
- Cài đặt lưu ở thư mục ứng dụng của người dùng, không cần quyền quản trị; ứng dụng không chạy với quyền quản trị.
- Chỉ Windows 10 bản 1903 trở lên (Windows 11 đã thử) vì cần cho phần quay ở đợt sau.
- Ngôn ngữ chỉ có tiếng Việt và tiếng Anh.

## Clarifications

### Session 2026-09-20

- Q: ứng dụng có cửa sổ chính không? -> A: không; khay hệ thống cộng thanh chụp nổi (mặc định của mình, chờ người dùng duyệt).
- Q: ngôn ngữ giao diện? -> A: tiếng Việt và tiếng Anh, mặc định theo Windows (chờ người dùng duyệt).

## What it does not do yet

- Cập nhật tự động, bộ cài, và ký số file chạy.
- Đồng bộ cài đặt giữa các máy.
- Chạy với quyền quản trị để chụp cửa sổ của ứng dụng chạy quyền cao.
- Nút quay màn hình và ghi chú lên màn hình: đợt sau, xem lộ trình.
