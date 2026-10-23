# ADR-0004: Quay màn hình bằng Desktop Duplication, Media Foundation và WASAPI, qua Vortice và NAudio

## Status

Proposed, chờ chủ dự án duyệt. Duyệt plan quay màn hình (2026-09-27) không phải duyệt ADR này.

## Date

Dự thảo 2026-09-27.

## Context

Đợt 2a quay màn hình ra MP4 (`docs/features/recorder/SPEC.md`). Ba việc cần một thư viện hệ thống của Windows:

- lấy khung hình của màn hình;
- mã hoá hình và tiếng vào MP4;
- thu tiếng hệ thống và tiếng micro.

Roadmap đã loại ffmpeg (nặng, giấy phép) và WinUI 3. Nó cũng ghi Windows.Graphics.Capture là cách của Fluent-Screen-Recorder.

Có ba điều khó đảo ngược:
- **Nguồn khung hình quyết định cái người dùng thấy.** Windows.Graphics.Capture vẽ một viền vàng quanh vùng đang quay trên Windows 10,
  và chỉ tắt được viền này trên Windows 11.
- **Gọi DXGI, Direct3D 11, Media Foundation và WASAPI từ .NET là COM.** Tự viết interop cho cả bốn là hàng nghìn dòng.
- **Gói ngoài vào file cài.** App publish một file, self-contained, không trim, nên mỗi gói làm file cài nặng thêm.

## Decision

1. **Khung hình lấy bằng DXGI Desktop Duplication.**
   - Mỗi màn hình dính vào vùng quay có một bản sao; adapter cắt phần của vùng rồi ghép lại thành một khung BGRA.
   - Con trỏ được Desktop Duplication đưa riêng (hình và vị trí), và chỉ được vẽ vào khung khi công tắc con trỏ bật.
   - Màn hình không đổi thì không có khung mới; UseCases lặp khung trước theo đồng hồ để giữ đúng số khung mỗi giây.
2. **Mã hoá bằng Media Foundation Sink Writer:** H.264 cho hình, AAC cho tiếng, gói MP4. File được ghi dưới tên `.part` và chỉ đổi
   sang `.mp4` khi Sink Writer kết thúc sạch.
3. **Tiếng bằng WASAPI:** loopback cho tiếng hệ thống, capture cho micro. Adapter đổi cả hai về 48 kHz stereo float. Việc trộn và
   canh giờ ở Domain (`AudioMixer`, `RecordingTimeline`), chạy được bằng unit test.
4. **Gói:**
   - `Vortice.Direct3D11`, `Vortice.DXGI`, `Vortice.MediaFoundation` (MIT) cho DXGI, D3D11 và Media Foundation;
   - `NAudio.Wasapi` (MIT) cho WASAPI.
   - Chỉ project Infrastructure tham chiếu chúng. Không kiểu nào của chúng đi qua port: qua port chỉ có `PixelImage`, `float[]` và
     record.
5. **Cửa sổ của app không có trong video.** Thanh quay, viền, số đếm ngược và toast đặt
   `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`. Việc này cần Windows 10 bản 2004, trùng với target
   `net10.0-windows10.0.19041.0` của app.

## Alternatives rejected

- **Windows.Graphics.Capture:** viền vàng trên Windows 10, và mỗi màn hình hay cửa sổ là một item riêng. Giữ lại cho sau này, nếu
  cần quay một cửa sổ đang bị che (SPEC: "What it does not do yet").
- **GDI BitBlt cho mỗi khung:** đơn giản, nhưng chậm ở 4K và 60 khung mỗi giây, và không cho con trỏ riêng.
- **ffmpeg đóng gói kèm:** nặng, giấy phép LGPL/GPL, và phải điều khiển một tiến trình ngoài.
- **Tự viết interop COM:** không kéo thêm gói nào, nhưng hàng nghìn dòng khó review và khó đúng. Nếu một gói Vortice bị bỏ rơi thì
  chuyển sang cách này.
- **MediaTranscoder cùng MediaStreamSource (WinRT):** cần surface Direct3D qua WinRT và luồng kéo khung. Khó thử bằng unit test hơn
  Sink Writer đẩy khung.

## Consequences

- File cài nặng thêm khoảng vài MB (các DLL Vortice và NAudio). `verify-installer.ps1` vẫn phải đạt.
- Adapter chỉ kiểm được trên máy có màn hình thật: E2E trên máy Hùng. CI chỉ dựng.
- Tắt `DXGI_ERROR_ACCESS_LOST`, đổi độ phân giải hay mở một ứng dụng toàn màn hình độc quyền đều làm mất bản sao. UseCases coi đó là
  "màn hình đổi" (SPEC recorder F2): dừng và lưu phần đã có.
- Nội dung có bảo vệ (DRM) ra màu đen: Windows chặn, và SPEC ghi là giả định.
