# Paper.ScreenWizzard — lộ trình

Mỗi đợt là một plan riêng đi hết vòng đời (SPEC -> duyệt -> làm -> kiểm) rồi mới sang đợt sau. Chỉ đợt 1
đã có SPEC; các đợt sau viết SPEC khi tới lượt, dựa trên đợt trước đã chạy thật.

| Đợt | Nội dung | Slug | Trạng thái |
| --- | --- | --- | --- |
| 1 | Khung ứng dụng, khay hệ thống, phím tắt, cài đặt; chụp 4 kiểu; trình sửa ảnh | `shell`, `capture`, `editor` | xong 2026-09-21 (chưa có bản phát hành; xem CLAUDE.md, mục Deploy) |
| 2 | Quay màn hình ra MP4 (vùng / cửa sổ / toàn màn hình) | `recorder` | chưa viết |
| 3 | Ghi chú lên màn hình kiểu EpicPen | `screen-note` | chưa viết |

## Yêu cầu đã nhận cho đợt 2 (quay màn hình, Hùng nói 2026-09-20, chưa thành SPEC)

Làm như FastStone Screen Recorder:

- **Chọn cái để quay:** một màn hình, một vùng tự kéo, một cửa sổ, hoặc toàn bộ desktop.
- **Bật camera (webcam)** chồng lên video, **chỉnh hình dạng khung camera** (tròn, vuông bo góc, chữ nhật...), kéo đi
  và đổi cỡ được, bật tắt trong lúc đang quay.
- **Âm thanh:** bật tắt tiếng hệ thống Windows và tiếng micro, độc lập nhau.
- Kèm con trỏ chuột, phím tắt bắt đầu/dừng/tạm dừng, đếm ngược trước khi quay, ra file MP4.

Từ ảnh chụp FastStone Screen Recorder Hùng gửi (2026-09-20), thứ có trong bản gốc:

- **Chọn cái quay:** cửa sổ / đối tượng, vùng chữ nhật, vùng kích thước cố định, toàn màn hình không tính thanh tác vụ,
  toàn màn hình, lặp lại vùng lần trước.
- **Hàng bật tắt:** micro, tiếng loa (hệ thống), webcam; ba nút Quay, Sửa, Tuỳ chọn.
- **Tuỳ chọn, các tab Video / Âm thanh / Thiết bị / Phím tắt / Đầu ra / Thông báo.** Video: số khung hình mỗi giây
  (mặc định 15), chất lượng, quay con trỏ, **làm nổi con trỏ**, **làm nổi cú bấm chuột** (kiểu tròn hoặc sao, cỡ),
  tỉ lệ phóng theo con trỏ (200%).

Từ ảnh trình sửa FastStone, các công cụ SPEC sửa ảnh đợt 1 **chưa có**: Caption (dòng chữ dưới ảnh), Edge (viền và bóng),
Resize (đổi cỡ), Spotlight (làm tối xung quanh vùng nhấn), Cut, Print, PDF, Email, tab nhiều ảnh. Cần cái nào thì báo,
sẽ thành một plan riêng chen trước đợt 2.

Hệ quả kỹ thuật cần chốt lúc viết SPEC đợt 2: camera qua Media Foundation (capture thiết bị), tiếng hệ thống qua WASAPI
loopback, micro qua WASAPI, trộn hai luồng tiếng và ghép với hình vào MP4; khung camera vẽ chồng lên từng khung hình
trước khi encode, nên hình dạng khung là một phần của luật ghép hình, thử được bằng unit test.

## Chọn kỹ thuật (đã đọc hai repo tham khảo, để đợt sau không phải tìm lại)

- **Chụp ảnh (đợt 1):** GDI `BitBlt` trên desktop ảo, pixel vật lý, process per-monitor DPI v2. Chụp một lần lúc
  bấm phím rồi cho người dùng chọn trên ảnh đóng băng, nên nội dung không đổi khi đang kéo. Khung cửa sổ lấy
  bằng `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` để không dính viền bóng trong suốt.
- **Quay (đợt 2):** `Windows.Graphics.Capture` (WinRT, Windows 10 1903 trở lên) lấy khung hình, encode H.264 bằng
  Media Foundation. Cách của Fluent-Screen-Recorder (`CaptureEncoder`: `GraphicsCaptureItem` + `MediaStreamSource`
  + `MediaTranscoder`) chạy được và không cần ffmpeg; nó là UWP nên phải kiểm lại cách gọi từ WPF/.NET 10.
  Âm thanh (micro, tiếng hệ thống qua WASAPI loopback) là phần nặng nhất, quyết ở đầu đợt 2.
- **Ghi chú (đợt 3):** cửa sổ toàn desktop ảo trong suốt, luôn trên cùng, có hai chế độ: vẽ (nhận chuột) và
  xuyên thấu (`WS_EX_TRANSPARENT`, chuột chạm xuống cửa sổ bên dưới). PaperTodo (WPF, ghi chú dán lên desktop)
  là tham chiếu cho cách một app WPF nhỏ giữ cửa sổ nổi và lưu vị trí.
- **Không dùng:** ffmpeg đóng gói kèm (nặng, giấy phép), CommunityToolkit.Mvvm (trái `paper-wpf-style`),
  WinUI 3 (Fluent-Screen-Recorder dùng UWP; app này chọn WPF để dùng chung kit, `drive-wpf` và hook của Paper).

## Câu hỏi chưa chốt, không chặn đợt 1

1. **Ghi chú** có hai nghĩa: (a) vẽ lên màn hình như EpicPen, đã chọn cho đợt 3; (b) ghi chú chữ dán trên
   desktop như PaperTodo. Nếu cần cả (b), thêm đợt 4.
2. ~~Quay có tiếng không~~ **Đã chốt:** có, tiếng hệ thống và micro bật tắt riêng, cộng camera; xem mục yêu cầu đợt 2.
3. **Chụp cuộn** (cuộn trang dài, FastStone có) và **xuất GIF**: hoãn, chưa có đợt.
