# Quay màn hình ra MP4 (đợt 2a) — plan — 2026-09-27

**Trạng thái:** đã duyệt 2026-09-27 ("1 --> duyte65, 2 --> ok, 3 --> Ok"; kèm "với cái giao diện khi chụp hay tắt thì ẩn cái giao diện ban đầu đi nha, dống fastron hoạt động ý")
**Loại việc:** code

Domain mới `Recorder` trong cả năm project:
- Domain: luật vùng quay (chẵn, cắt theo màn hình, tối thiểu 16 × 16), dòng thời gian khi tạm dừng, tên file.
- UseCases: `RecorderInteractor` với các port khung hình, tiếng, bộ mã hoá.
- Infrastructure: Desktop Duplication, WASAPI, Media Foundation.
- Presentation: thanh quay, viền, cửa sổ "Đã quay".
- App: khay, phím tắt, thoát.

Brief: [2026-09-27-quay-man-hinh.md](2026-09-27-quay-man-hinh.md) · Luật: [SPEC.md](SPEC.md)

## Context

- **Chạm:**
  - `src/*/Recorder/**` (mới) và `tests/*/Recorder/**` (mới);
  - `Shell`: hotkey, cài đặt, khay, thanh chụp; `Shared` nếu hai domain cần;
  - `App`: `AppShell`, `CompositionRoot`, `TrayIcon`;
  - `Directory.Packages` / `csproj` của Infrastructure: gói mới, theo ADR 0004.
- **Có sẵn để dựa vào:**
  - `IMonitorCatalog` (pixel vật lý, DPI) và `IWindowCatalog` (khung cửa sổ không bóng, SPEC capture "Cửa sổ");
  - `SelectionOverlayWindow` và `PhysicalWindowPlacer` để kéo vùng; `CountdownWindow`;
  - `IHotkeys`, hiện chỉ nhận `CaptureKind`, nên phải mở rộng;
  - `ISettingsStore`, `SettingsRules.Complete` (mục thiếu thì lấy mặc định, F8 của shell);
  - `INotifications`; `IFileStore` và luật đặt tên file có hậu tố (2) của capture;
  - `TrayIcon`.
- **Dịch vụ dùng chung:**
  - lưu settings: có (`ISettingsStore`), thêm các mục của quay;
  - thông báo: có (`INotifications`);
  - log: có (`ILog`);
  - huỷ: có (`CancellationToken`, như `CaptureInteractor`);
  - progress: không cần, thanh quay hiện thời gian;
  - transaction/undo: không cần;
  - **thiếu:** hotkey cho việc không phải một kiểu chụp. T4 thêm nó, trước khi có task nào dùng.
- **Bài học đã nhận áp dụng:** không có (chưa có `docs/retro`). **Bug liên quan:** không có (chưa có `docs/bugs`).
- **Môi trường:**
  - phiên cloud không có .NET SDK và không mở được learn.microsoft.com (proxy chặn);
  - CI (`windows-latest`) dựng và chạy unit; runner không có desktop để quay;
  - adapter thật chỉ kiểm được bằng E2E trên máy Hùng; `ui` cũng chạy trên máy Hùng.

## Rules that apply

- ADR 0001, 0003: domain `Recorder` là thư mục cấp một ở mọi project.
  - Port ở `UseCases/Recorder/Ports`, mỗi file một interface, tên theo thứ nó cung cấp.
  - Recorder chỉ nhìn chính nó và `Shared`; `Shell` điều phối.
  - `FolderShapeTests` và `LayerTests` canh hình này; số interactor đếm cứng sẽ lên 5.
- Mọi quyết định (vùng, cỡ chẵn, lịch tạm dừng, trộn tiếng hay không, dừng vì lỗi gì, tên file) nằm ở Domain và UseCases, chạy
  bằng unit test. Adapter chỉ chép khung hình, mẫu tiếng và ghi file.
- Qua port chỉ đi số, mảng byte, record. Không một kiểu D3D, DXGI, MF hay NAudio nào vượt qua port.
- Toạ độ là pixel vật lý của desktop ảo (CLAUDE.md).
- Chữ qua `{DynamicResource}`, đủ vi và en; nút chỉ có icon thì có `AutomationProperties.Name`.
- Không gửi phím PrintScreen thật trong test; test quay không ghi vào thư mục Video thật.
- Gói mới chỉ vào Infrastructure; Domain và UseCases không có gói nào (`layer-guard`).

## Decisions

- **Mọi lựa chọn thành một hình chữ nhật của desktop, cố định lúc bắt đầu.**
  - Màn hình: khung của nó. Cả desktop: hình bao mọi màn hình. Vùng: vùng kéo. Cửa sổ: khung không bóng lúc bấm Quay.
  - Vì vậy chỉ cần một nguồn khung hình và một luật cắt.
  - Rejected: bám theo cửa sổ (cỡ video đổi giữa chừng, và phải dùng Windows.Graphics.Capture).
- **Khung hình lấy bằng DXGI Desktop Duplication, mỗi màn hình một bản, cắt và ghép thành vùng quay.**
  - Không có viền vàng mà Windows.Graphics.Capture vẽ trên Windows 10.
  - Cho con trỏ tách riêng, nên luật "kèm con trỏ" vẽ con trỏ theo hình và vị trí nó đưa.
  - Rejected:
    - Windows.Graphics.Capture: viền vàng không tắt được trên Windows 10;
    - GDI BitBlt mỗi khung: chậm ở 4K và 60 fps.
- **Mã hoá bằng Media Foundation Sink Writer (H.264 + AAC vào MP4).** Có sẵn trong Windows, không kèm ffmpeg (roadmap: "Không
  dùng ffmpeg"). Khung hình vào là BGRA; Sink Writer tự chuyển sang NV12.
- **Tiếng bằng WASAPI:** loopback cho tiếng hệ thống, capture cho micro. Hai luồng đổi về cùng 48 kHz stereo float rồi trộn ở
  UseCases, bằng một luật cộng có kẹp để test được.
- **Gói ngoài: `Vortice.Direct3D11`, `Vortice.DXGI`, `Vortice.MediaFoundation` và `NAudio.Wasapi` (MIT).**
  - Gọi các COM này bằng tay là hàng nghìn dòng interop.
  - Ghi thành **ADR 0004** (Proposed; chỉ Hùng duyệt), gồm cả ba lựa chọn trên.
  - Rejected: interop tự viết (dài, dễ sai), ffmpeg (nặng, giấy phép).
- **Dòng thời gian:**
  - Mỗi mẫu hình và mẫu tiếng mang giờ đo bằng một đồng hồ chung (QPC).
  - Tạm dừng trừ khoảng dừng khỏi mọi giờ sau đó, nên không có đoạn đứng hình.
  - Khung hình trễ thì lặp khung trước, khung dư thì bỏ, để giữ đúng fps.
  - Luật này nằm ở Domain (`RecordingTimeline`).
- **Ứng dụng không quay chính nó:**
  - thanh quay, viền, đếm ngược và toast đặt `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` (Windows 10 2004);
  - việc này thuộc ngoại lệ đặt cửa sổ ở Presentation, như `PhysicalWindowPlacer`.
- **Hotkey:**
  - `IHotkeys` đổi từ `CaptureKind` sang một `HotkeyAction`, gồm bốn kiểu chụp cộng `RecordStartStop` và `RecordPause`;
  - **Lệch khi làm (2026-09-27, chờ Hùng đồng ý):** code giữ `CaptureKind` cho bốn phím chụp và thêm kiểu `RecordHotkey` (StartStop,
    Pause) với `IHotkeys.Register/Unregister(RecordHotkey)` và sự kiện `RecordPressed`. Lý do: mọi chỗ gọi, test và tập tin cài đặt của
    phím chụp giữ nguyên; phím quay được lưu ở mục riêng `recordHotkeys`. Hệ quả giống nhau: tập tin 0.1.3 đọc được, hai phím quay có
    mặc định, kiểm trùng dùng chung một luật. Nếu Hùng muốn đúng `HotkeyAction` thì là một lượt đổi tên riêng;
  - `AppSettings.Hotkeys` đọc tập tin cũ không có hai mục mới thì lấy mặc định (F8 của shell);
  - mã hoá JSON giữ nguyên tên của bốn kiểu chụp, nên tập tin cũ vẫn đọc được.
- **File ghi dở:**
  - ghi vào `<tên>.mp4.part`, đổi tên khi Sink Writer kết thúc sạch;
  - lỗi thì xoá `.part` nếu không cứu được (F3);
  - dừng vì F1 hay F2 thì kết thúc sạch với phần đã có.
- **Cài đặt mới:** thư mục video, fps, đếm ngược, lựa chọn cuối của thanh quay (cái để quay, màn hình nào, ba công tắc), hai phím
  tắt. Nhóm "Quay màn hình" trong Cài đặt.

- **Spec bổ sung 2026-09-27 (cùng lượt duyệt):** thanh quay và thanh chụp ẩn khi bấm Quay, hiện lại sau khi dừng; trong lúc quay
  điều khiển ở biểu tượng khay (đỏ, tooltip thời gian, menu Tạm dừng/Tiếp tục, Dừng) và phím tắt; khi chụp ảnh thì thanh quay ẩn như
  thanh chụp. Thanh quay không còn dạng "đang quay"; T6, T7, T10 phủ các dòng này.

## UI wireframe

```
Thanh quay (luôn trên cùng, kéo được, không có trong video):
┌──────────────────────────────────────────────────────────────────────────┐
│ [▭ Màn hình ▾] [⬚ Vùng] [🗔 Cửa sổ] [▦ Cả desktop] │ 🔊 ☑ 🎤 ☐ 🖱 ☑ │ [● Quay] ✕ │
└──────────────────────────────────────────────────────────────────────────┘
   Màn hình ▾ → 1 (1920×1080, chính) · 2 (2560×1440)

Đang quay: thanh quay ẩn. Biểu tượng khay đỏ, tooltip "Đang quay 00:01:23", menu khay:
  ❚❚ Tạm dừng   Ctrl+Alt+P
  ■ Dừng        Ctrl+Alt+R
  ──────────
  (các dòng chụp, Cài đặt, Thoát như cũ)
Vùng quay: viền đỏ 2 px nét đứt ngay ngoài vùng (không có trong video).

Đã quay:
┌ Đã quay ─────────────────────────────── ✕ ┐
│ Recording 2026-09-27 14.03.05.mp4          │
│ 1920 × 1080 · 00:01:23 · 42 MB             │
│ [Mở video] [Mở thư mục] [Đóng]             │
└────────────────────────────────────────────┘
```

- **Bốn nút chọn cái để quay nằm thành một hàng, không để trong menu** (Hick: bốn lựa chọn, một cái nhìn; nhận ra hơn nhớ). Chỉ
  "màn hình nào" nằm trong menu thả, vì nó chỉ có khi có hơn một màn hình.
- **Công tắc tiếng và con trỏ nằm cạnh nút Quay** (Fitts: quyết định cuối trước khi bấm ở gần nút bấm; Gestalt: tách khỏi nhóm
  "cái để quay" bằng vạch). Chúng kéo ngược với sự gọn của thanh; giữ vì Hùng muốn bật tắt từng cái.
- **Khi đang quay, thanh ẩn; trạng thái và nút nằm ở khay** (Hùng muốn như FastStone; hiện trạng thái hệ thống qua biểu tượng đỏ
  và tooltip; không che nội dung đang quay). Kéo ngược: nút ở khay xa hơn (Fitts) → bù bằng phím tắt.
- **Cửa sổ "Đã quay" dùng cùng khuôn với "Đã chụp"** (nhất quán): file đã lưu, nên không có nút Bỏ.

## Tasks

Lane `e2e` không có verb (profile chỉ cấp `unit` và `ui`). Các task E2E không gắn lane và chạy tay bằng
`dotnet test tests/Paper.ScreenWizzard.E2eTests -c Debug` trên máy Hùng, như đợt 1.

### 1. Quyết định kỹ thuật và luật lõi

- [x] T1 ADR 0004 "Quay: Desktop Duplication, Media Foundation Sink Writer, WASAPI, qua Vortice và NAudio", Proposed; mục lục ADR; CLAUDE.md mục Tầng nhắc gói của Infrastructure — xong khi ADR có đủ lựa chọn, phương án bị bỏ, hệ quả {files: docs/decisions/**, CLAUDE.md}
- [x] T2 [red][unit] Test cho SPEC recorder: "Quay cái gì": vùng của màn hình, cả desktop, vùng kéo, cửa sổ, cỡ lẻ thành chẵn, cắt theo màn hình, 150%; "Tạm dừng và dừng": 60 giây, dừng khi tạm dừng, phím trong đếm ngược, thoát khi đang quay; "File": tên, (2), tạo thư mục; "Tiếng": không rãnh, một rãnh, trộn hai rãnh, lệch dưới 0,1 giây theo dòng thời gian; các mã F1, F2, F3, F4, F5, F6, F7, F8, mỗi mã một test `F<n>_…`, với port giả; xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UnitTests/Recorder/**}
- [x] T3 [unit] Code tới khi T2 xanh: Domain/Recorder: `RecordArea`, `RecordingTimeline`, `RecordingFileName`, `AudioMix`; UseCases/Recorder: ports `IScreenFrames`, `ISoundSources`, `IVideoWriter`, `IRecorderInteractor`; `RecorderInteractor`; `FolderShapeTests` và `LayerTests` đếm 5 interactor; verb `test` — xong khi exit 0, số test > 420 {files: src/Paper.ScreenWizzard.Domain/Recorder/**, src/Paper.ScreenWizzard.UseCases/Recorder/**, tests/Paper.ScreenWizzard.UnitTests/Recorder/**, tests/Paper.ScreenWizzard.UnitTests/Architecture/**}

### 2. Hotkey và cài đặt cho quay — sau nhóm 1

- [x] T4 [red][unit] Test: `HotkeyAction` và hai phím quay mặc định Ctrl+Alt+R, Ctrl+Alt+P; tập tin cài đặt 0.1.3 (chỉ bốn phím chụp, không mục quay) đọc được và lấy mặc định; F8 recorder: phím quay bị giữ thì báo và các phím khác vẫn chạy; các mục quay trong `SettingsRules` (fps 15/30/60, đếm ngược 0/3/5, ngoài miền là hỏng); xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UnitTests/Shell/**}
- [x] T5 [unit] Code (làm khác: `RecordHotkey` bên cạnh `CaptureKind`, xem Decisions, "Lệch khi làm"): `HotkeyAction` thay `CaptureKind` ở `IHotkeys`, `AppSettings.Hotkeys`, `StoredSettings`; `RecorderSettings` trong `AppSettings`; `SettingsRules.Complete`; `SettingsStore` document; `HotkeyService`; verb `test` — xong khi exit 0 và mọi test cũ vẫn xanh {files: src/Paper.ScreenWizzard.Domain/Shell/**, src/Paper.ScreenWizzard.UseCases/Shell/**, src/Paper.ScreenWizzard.Infrastructure/Shell/**, tests/Paper.ScreenWizzard.UnitTests/Shell/**}

### 3. Giao diện trên dữ liệu giả — sau nhóm 2

- [ ] T6 [red][ui] Test FlaUI: thanh quay theo wireframe: bốn nút cái để quay, menu màn hình khi có hai màn hình giả, ba công tắc, nút Quay; F7: vùng nhỏ thì Quay bị tắt, có thông báo; đang quay: thời gian chạy, Tạm dừng thành Tiếp tục; cửa sổ "Đã quay" với ba nút; Tab theo thứ tự nhìn thấy; khoá chuỗi vi/en; menu khay có "Quay màn hình…"; thanh chụp có nút Quay; nhóm "Quay màn hình" trong Cài đặt; xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UiTests/Recorder/**, tests/Paper.ScreenWizzard.UiTests/Shell/**}
- [ ] T7 [ui] Code tới khi T6 xanh, mở ảnh chụp ra so với wireframe: `Presentation/Recorder`: thanh quay, viền, "Đã quay", lệnh; ViewModel khay, thanh chụp, Cài đặt; chuỗi; verb `ui` — xong khi ảnh khớp wireframe {files: src/Paper.ScreenWizzard.Presentation/**, tests/Paper.ScreenWizzard.UiTests/**}

### 4. Adapter thật — sau nhóm 3 (E2E trên máy Hùng; CI chỉ dựng)

- [ ] T8 E2E (viết trước, đỏ) `tests/.../E2eTests/Recorder`: quay 3 giây một màn hình vào thư mục tạm, đọc lại file bằng Media Foundation; kiểm: cỡ đúng vùng và chẵn; thời lượng 3 ± 0,2 giây; rãnh tiếng có hoặc không theo công tắc; tạm dừng 2 giây thì không làm dài video; thanh quay không có trong khung đầu (so pixel dưới thanh); F3: thư mục chỉ đọc thì không còn `.part`; xong khi đỏ vì adapter chưa có {files: tests/Paper.ScreenWizzard.E2eTests/Recorder/**}
- [ ] T9 Adapter: `DesktopDuplicationFrames` (từng màn hình, cắt, ghép, con trỏ); `WasapiSoundSources` (loopback, micro, đổi 48 kHz); `MediaFoundationVideoWriter` (`.part`, đổi tên); gói theo ADR 0004; xong khi T8 xanh trên máy Hùng và CI dựng 0 cảnh báo {files: src/Paper.ScreenWizzard.Infrastructure/Recorder/**, src/Paper.ScreenWizzard.Infrastructure/*.csproj, tests/Paper.ScreenWizzard.E2eTests/Recorder/**}

### 5. Ghép vào ứng dụng — sau nhóm 4

- [x] T10 `AppShell`, `CompositionRoot`, `TrayIcon`: mở thanh quay; phím quay; thoát khi đang quay thì dừng và lưu trước; `WDA_EXCLUDEFROMCAPTURE` cho thanh, viền, đếm ngược, toast; xong khi CI dựng 0 cảnh báo {files: src/Paper.ScreenWizzard.App/**, src/Paper.ScreenWizzard.Presentation/Shared/Views/**}
- [ ] T11 E2E chạy exe: phím Ctrl+Alt+R bắt đầu, lại lần nữa thì dừng, file ra ở thư mục video của bản chạy thử (biến `PAPER_SCREENWIZZARD_DATA`), thoát khi đang quay vẫn có file — xong khi xanh trên máy Hùng {files: tests/Paper.ScreenWizzard.E2eTests/Drive/**}

### Last. Close

- [x] T12 SPEC shell (hai ngôn ngữ) ghi dòng khay "Quay màn hình…", nút Quay của thanh chụp, nhóm quay trong Cài đặt; roadmap: đợt 2a xong, 2b webcam, 2c icon lên mặt — xong khi `check-spec` sạch {files: docs/**}
- [x] T13 `find-bug` trên SPEC recorder — xong khi mọi phát hiện có input đã thành test hoặc vào báo cáo
- [x] T14 Agent `architecture-reviewer` trên mọi file plan này đổi — xong khi 0 vi phạm
- [ ] T15 Đóng SPEC.md (gỡ banner); CODEMAP cho `Recorder` ở năm project; `check-spec`, `check-code-map` sạch — xong khi hai lệnh exit 0 {files: docs/features/recorder/SPEC.md, src/**/CODEMAP.md}

## API đã tra

learn.microsoft.com bị proxy của phiên cloud chặn (2026-09-27), nên chưa đọc được trang nào. Bảng dưới là danh sách phải tra, theo
tài liệu quen dùng. T8 và T9 tra lại và đo trên máy Hùng trước khi viết dòng code gọi.

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|
| `IDXGIOutput1.DuplicateOutput`, `IDXGIOutputDuplication.AcquireNextFrame` / `ReleaseFrame`, `GetFramePointerShape` | DXGI 1.2, Windows 8+ | **chưa đọc** | mỗi output một bản; `DXGI_ERROR_ACCESS_LOST` khi đổi chế độ màn hình (F2); timeout thì không có khung mới, lặp khung trước; con trỏ tách riêng |
| `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` | Windows 10 2004 (19041) | **chưa đọc** | cửa sổ không có trong Desktop Duplication, BitBlt và Graphics Capture |
| `MFCreateSinkWriterFromURL`, `IMFSinkWriter.AddStream` / `SetInputMediaType` / `WriteSample` / `Finalize` | Media Foundation, Windows 7+ | **chưa đọc** | H.264 cần cỡ chẵn; input RGB32 được chuyển sang NV12 khi bật `MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS` hay bộ xử lý video; timestamp đơn vị 100 ns |
| NAudio `WasapiLoopbackCapture`, `WasapiCapture`, `MMDeviceEnumerator` | NAudio 2.2 | **chưa đọc** | loopback không cho mẫu khi máy im lặng, nên phải chèn im lặng theo đồng hồ; mất thiết bị thì `RecordingStopped` có exception (F4, F5) |
| Vortice.Windows (Direct3D11, DXGI, MediaFoundation) | 3.x | **chưa đọc** | bọc COM kiểu .NET, MIT; phải kiểm có chạy trên `net10.0-windows10.0.19041.0` và publish single-file không trim |

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
| T1 | `docs/decisions/0004-quay-bang-desktop-duplication-va-media-foundation.md` (Proposed), mục lục ADR, CLAUDE.md bảng tầng nhắc 0004 và `Recorder` | pass |
| T2 | `tests/.../Recorder/RecordAreaTests.cs`, `RecordingClockTests.cs`, `RecorderSpecTests.cs` (F1_…, F2_…, F3_… ×2, F4_… ×2, F5_…, F6_… ×2, F7_… ×2). Đỏ trước **không quan sát được**: phiên cloud không có .NET SDK, test và code lên CI cùng một lần đẩy | pass, đỏ-trước not verifiable |
| T3 | CI `ci` run 36329004516 trên `5482f5f`: build Release 0 cảnh báo; `Passed! - Failed: 0, Passed: 464, Total: 464` (420 trước + 44 mới); `FolderShapeTests`, `LayerTests` đếm 5 interactor; `IDelay`/`TaskDelay` chuyển sang `Shared` | pass |
| T4 | `tests/.../UnitTests/Shell/RecordingSettingsTests.cs` (11 test: hai phím mặc định, tập tin 0.1.3 không mục quay thì lấy mặc định, F8 phím quay bị giữ, fps/đếm ngược ngoài miền là hỏng); `SettingsRulesTests.Chosen` có các mục quay. Đỏ trước **không quan sát được** (lý do như T2). Plan viết `HotkeyAction`; code giữ `CaptureKind` cho phím chụp và thêm `RecordHotkey` riêng (`IHotkeys.Register(RecordHotkey)`), để tập tin cũ và mọi chỗ gọi cũ không đổi | pass, đỏ-trước not verifiable |
| T5 | CI `ci` run 36330862665 trên `da629f2`: `Build succeeded. 0 Warning(s) 0 Error(s)`; `Passed! - Failed: 0, Passed: 484, Skipped: 0, Total: 484` | pass |
| T6 | `tests/.../UiTests/Recorder/RecorderWindowsTests.cs` (5 test), `TrayMenuTests` (10 dòng, dòng quay khi đang quay), `CaptureBarTests` (`RecordButton`), `SettingsWindowTests` (thứ tự Tab có nhóm quay), `LanguageAndThemeTests` (khoá chuỗi). CI dựng được; **chưa chạy**: lane `ui` cần màn hình rảnh, chạy trên máy Hùng | chờ máy Hùng |
| T7 | Code ở `Presentation/Recorder/**`, khay, thanh chụp, Cài đặt; CI dựng 0 cảnh báo (run 36330862665). Ảnh chụp so với wireframe **chưa có**: cần chạy `ui` trên máy Hùng | chờ máy Hùng |
| T8 | `tests/.../E2eTests/Recorder/RecorderAdapterTests.cs` (4 test), `Mp4Probe.cs`; CI dựng được; chưa chạy (cần desktop) | chờ máy Hùng |
| T9 | `Infrastructure/Recorder/**` (DesktopDuplicationFrames, PointerPainter, WasapiSoundSources, SoundConverter, MediaFoundationVideoWriter), gói Vortice 3.8.3 và NAudio.Wasapi 2.2.1; CI dựng 0 cảnh báo | chờ máy Hùng (T8 xanh) |
| T10 | `AppShell` (mở thanh quay, phím quay, ẩn thanh khi quay và khi chụp, khay đỏ có thời gian, thoát thì dừng và lưu trước), `CompositionRoot`, `TrayIcon.ShowRecording`, `CaptureExclusion`; CI run 36330862665: 0 cảnh báo | pass |
| T11 | `tests/.../E2eTests/Drive/RecordingDriveTests.cs` (3 test: phím bắt đầu/dừng ra file và thanh ẩn rồi hiện lại, tooltip khay; tạm dừng không làm dài video; thoát khi đang quay vẫn có file, không còn `.part`); phím thử là Ctrl+Alt+Shift+F17/F18, thư mục video là thư mục tạm. `ShellDriveTests` cập nhật tên dòng menu khay (Quay màn hình…, Kiểm bản mới) | chờ máy Hùng |
| T12 | SPEC shell hai ngôn ngữ: dòng khay, nút Quay, nhóm "Quay màn hình" trong Cài đặt, hàng Inputs; roadmap: 2a đang làm, 2b webcam, 2c icon lên mặt; `check_spec.py`: `5 SPEC.md, 0 problems` | pass |
| T13 | find-bug trên SPEC recorder, hai phát hiện, cả hai đã sửa ở `AppShell`: (1) chọn Thoát đúng lúc bản quay tự dừng vì F1/F2 (trạng thái `Finishing`) thì ứng dụng không chờ, file có thể còn `.part` → giờ chờ mọi trạng thái khác `Idle`; (2) đang quay mà mở Cài đặt thì phím Dừng/Tạm dừng bị bỏ qua → giờ chỉ chặn việc bắt đầu bản quay mới. Cả hai chỉ kiểm được bằng E2E (T11 phủ đường thoát). Nghi ngờ, chưa thành phát hiện: bấm phím bắt đầu/dừng lúc đang kéo vùng thì không huỷ việc kéo | pass |
| T14 | Agent `architecture-reviewer`, lượt 1: 9 vi phạm (thông báo F7, bắt đầu/dừng, tạm dừng và thứ tự màn hình quyết ở Presentation; bitrate ở adapter; giữ lựa chọn, guard phím và tooltip ở `AppShell`; `IRecorderViews` không có fake; lệch `HotkeyAction`). Sửa ở `22ab9c6`: `RecordAreaAnswer`, `TogglePause`, `StopIfActiveAsync`, `RecordArea.InBarOrder`/`KnownMonitor`, `RecorderRules.VideoBitsPerSecond` (tách ra thì lộ lỗi: 0,1 bit/pixel cho 10 phút 1080p30 là ~470 MB, SPEC nói ~300 MB; nay 1/16 bit, test canh), `IShellInteractor.KeepRecorderChoices`, `RecordingTimeText`, `RecordingFlowTests` (8 test, fake views); lệch hotkey ghi ở Decisions. Lượt 2 (77/77 file): 1 vi phạm, guard `State != Idle` lúc thoát ở `AppShell`; đã bỏ, use case tự quyết. CI run 36331686612 trên `22ab9c6`: `Passed: 497, Total: 497`, 0 cảnh báo | pass |
| T15 | CODEMAP năm project có `Recorder`; `check_code_map.py`: `6 CODEMAP.md, 0 stale`; `check_spec.py`: `5 SPEC.md, 0 problems`. **Banner SPEC chưa gỡ:** gỡ khi `ui` (T6, T7) và E2E (T8, T9, T11) xanh trên máy Hùng | chờ máy Hùng |
