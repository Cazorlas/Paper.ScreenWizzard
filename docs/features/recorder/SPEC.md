> Bản nháp, duyệt 2026-09-27, đang làm. Brief: [2026-09-27-quay-man-hinh.md](2026-09-27-quay-man-hinh.md) · Plan: [2026-09-27-quay-man-hinh-plan.md](2026-09-27-quay-man-hinh-plan.md)

# Quay màn hình — SPEC

Quay một màn hình, một vùng tự kéo, một cửa sổ hay cả desktop ra một file MP4, kèm tiếng hệ thống và tiếng micro bật tắt riêng,
có đếm ngược, tạm dừng và phím tắt. Webcam chồng lên video (đợt 2b) và icon dán lên mặt (đợt 2c) là các đợt sau.

English · Tiếng Việt — cùng một yêu cầu viết hai lần; đổi thì đổi cả hai.

# English

## User story

As someone who shows colleagues how to do things on the computer, I want to record exactly the part of the screen that matters,
with my voice and the computer's sound, into an MP4 I can send at once, without installing anything else.

## What the user does

1. Chooses Record screen… in the tray menu, the Record button on the capture bar, or presses the start/stop hotkey. A small
   recording bar opens.
2. On the recording bar picks what to record: **one monitor** (which one, when there are several), **a region** dragged on the
   live screen, **a window** (click it), or **the whole desktop** (every monitor). Switches system sound, microphone and pointer
   on or off, each on its own.
3. Presses Record. The recording bar **hides**, as FastStone's does; the chosen area is outlined, a countdown shows 3, 2, 1, and
   recording starts.
4. While recording, nothing of the app stays in the way: the tray icon turns red and shows the elapsed time when the pointer rests
   on it, and its menu has Pause/Resume and Stop. The hotkeys do the same.
5. Presses Stop. The MP4 is saved in the video folder, a "Recorded" window offers Open video, Show in folder and Close, and the
   recording bar comes back.

## Inputs

| The user chooses | Default | Decides |
| --- | --- | --- |
| What to record | the monitor under the pointer | one monitor, a region, a window, or the whole desktop |
| System sound | on | the sound the computer plays is in the video |
| Microphone | off | the microphone's sound is in the video |
| Pointer | on | the mouse pointer is drawn in the video |
| Countdown | 3 seconds (0, 3, 5) | how long before recording starts |
| Frames per second | 30 (15, 30, 60) | how smooth the video is, and how large the file |
| Video folder | the Videos folder, Paper.ScreenWizzard | where MP4 files are saved |
| Start/stop hotkey | Ctrl+Alt+R | starts recording with the last choices, or stops it |
| Pause hotkey | Ctrl+Alt+P | pauses or resumes |

The choices of the recording bar are kept for the next recording and the next start of the app.

## Outputs

- An MP4 file (H.264 video, AAC sound) in the video folder, named from the date and time the recording started.
- The "Recorded" window; a notice when something did not work (see "When it does not do the job").

## Key entities

Recording bar, area to record, monitor, region, window, system sound, microphone, countdown, recording, pause, MP4 file.

## What is recorded

**The area is fixed when recording starts.** Every choice becomes one rectangle of the desktop.

- Given two 1920 × 1080 monitors side by side and "monitor 2" chosen → the video is **1920 × 1080** of the second monitor only
- Given the same monitors and "whole desktop" → the video is **3840 × 1080**, both monitors side by side, no gap
- Given a region dragged from (300, 200) to (1100, 650) → the video is **800 × 450** of that part of the screen
- Given a window whose visible frame is 1000 × 700 → the video is **1000 × 700**, without the invisible shadow Windows adds
- Given the recorded window moved while recording → the video **keeps the area where the window was** at the start
- Given a region or a window of odd size, 801 × 451 → the video is **800 × 450** (a video needs even sizes); one pixel is dropped
  at the right and the bottom
- Given a region dragged partly outside every monitor → only the part on a monitor is recorded
- Given a monitor set to 150% → the video has the monitor's **real pixels** (a region of 300 × 200 as seen is 450 × 300)

**The app never records itself, and gets out of the way.**

- Given Record pressed → the recording bar and the capture bar **are hidden** until the recording stops; the bars that were open
  come back after it
- Given a screenshot taken (any kind) while the recording bar is open → the recording bar **is hidden** during the capture and
  comes back after, as the capture bar does
- Given the recording bar, the outline of the area and the countdown on screen → **none of them** is in the video
- Given a notice or the "Recorded" window of the app shown while recording → it is **not** in the video

## Sound

- Given system sound on and microphone off → the video has **the computer's sound only**
- Given both on → the video has **both, mixed**, at the same time as the picture
- Given both off → the video has **no sound track**
- Given the microphone switched on and no microphone plugged in → recording starts **without** microphone sound, and a notice says
  no microphone was found (F4)
- The sound stays in time with the picture: after a 10-minute recording, sound and picture differ by **less than 0.1 second**

## Pause and stop

- Given recording paused for 20 seconds and resumed → the video **has no gap and no frozen 20 seconds**: it goes on from the
  moment of pausing
- Given 30 seconds recorded, paused 20 seconds, then 30 more seconds → the video is **60 seconds** long
- Given Stop while paused → the video ends **at the pause**
- Given the start/stop hotkey during the countdown → **nothing is recorded** and no file is made
- Given the app exited from the tray while recording → the recording is **stopped and saved first**, then the app exits

## The file

- Given a recording started on 2026-09-27 at 14:03:05 → the file is **Recording 2026-09-27 14.03.05.mp4**
- Given a file of that name already there → the new file is **Recording 2026-09-27 14.03.05 (2).mp4**; the old one is **not
  overwritten**
- Given the video folder missing → it is **created**
- The file plays in the Windows Media Player and the Films & TV app, and in a browser

## Edge cases

- One recording at a time. While recording, the capture hotkeys still take screenshots; the recording bar and outline are not in
  them.
- Recording goes on when the app's other windows open or close.

## When it does not do the job

| # | Case | What the user sees |
| --- | --- | --- |
| F1 | the recorded window is closed while recording | the recording stops by itself and what was recorded is saved; the "Recorded" window says the window was closed |
| F2 | the recorded monitor is unplugged or the display changes (resolution, scale) while recording | the recording stops by itself and what was recorded is saved; a notice says why |
| F3 | the disk fills up or the video folder cannot be written | the recording stops; the notice names the folder and the reason; nothing half-written is left as if it were a video |
| F4 | the microphone is on but missing, or taken away while recording | recording goes on without it; a notice says the microphone was lost |
| F5 | no sound can be recorded from the computer (no output device) | recording goes on without system sound; a notice says so |
| F6 | recording cannot start (the graphics driver refuses screen capture, no video encoder) | nothing is recorded; a box names the reason; the app keeps running |
| F7 | the chosen region is smaller than 16 × 16 | Record is refused and the notice asks for a larger region |
| F8 | the start/stop or pause hotkey is taken by another program | the other hotkeys and the recording bar still work; a notice names the key (as for the capture hotkeys) |

## Assumptions

- Windows 10 version 2004 or later (the app's windows are kept out of the video by a Windows feature that version brings).
- A recording of 10 minutes at 1920 × 1080 and 30 frames per second takes at most about 300 MB.
- Protected content (some video players, DRM) may record as black; that is Windows' choice and not a failure.
- The recorded area is fixed at the start; a window that moves is not followed.
- The default hotkeys are a first choice; the user can change them in Settings like the capture hotkeys.

## Clarifications

### Session 2026-09-27

- Q: what does round 2 record? (Hùng 2026-09-20, from FastStone Screen Recorder) -> A: one monitor, a dragged region, a window or
  the whole desktop; system sound and microphone on and off independently; countdown; hotkeys; MP4.
- Q: webcam and face stickers? (Hùng 2026-09-27: "Chưa có thể hiện webcame nhỉ, rùi làm thêm mí filter icon chèn mặt dc ko") ->
  A: yes, as rounds 2b (webcam on the video) and 2c (icons that follow the face), after this one ships.
- Q: should a recorded window be followed when it moves? -> A: no, the area stays where the window was (Hùng: "ok").
- Q: the default hotkeys Ctrl+Alt+R and Ctrl+Alt+P? -> A: yes (Hùng: "Ok").
- Q: the app's own windows while capturing or recording? (Hùng: "với cái giao diện khi chụp hay tắt thì ẩn cái giao diện ban đầu đi
  nha, dống fastron hoạt động ý") -> A: they hide, and the tray icon carries the controls while recording.

## What it does not do yet

- Webcam over the video (round 2b) and icons on the face (round 2c).
- Highlighting clicks or the pointer, zooming on the pointer, a fixed-size region, repeating the last region.
- Editing or trimming a video, GIF output.
- Recording one window while it is covered by others (only what is on screen is recorded).

# Tiếng Việt

## User story

Là người hay chỉ đồng nghiệp cách làm trên máy, tôi muốn quay đúng phần màn hình cần thiết, kèm giọng mình và tiếng của máy, ra
một file MP4 gửi được ngay, không phải cài thêm gì.

## What the user does

1. Chọn Quay màn hình… trong menu khay, bấm nút Quay trên thanh chụp, hoặc bấm phím tắt bắt đầu/dừng. Một thanh quay nhỏ mở ra.
2. Trên thanh quay chọn cái để quay: **một màn hình** (màn hình nào, khi có nhiều cái), **một vùng** kéo trên màn hình đang chạy,
   **một cửa sổ** (bấm vào nó), hoặc **cả desktop** (mọi màn hình). Bật tắt tiếng hệ thống, micro và con trỏ, mỗi cái riêng.
3. Bấm Quay. Thanh quay **ẩn đi**, như FastStone; vùng đã chọn có viền bao, đếm ngược 3, 2, 1, rồi bắt đầu quay.
4. Trong lúc quay, không gì của ứng dụng chắn trên màn hình: biểu tượng khay chuyển đỏ và hiện thời gian đã quay khi rê chuột lên,
   menu của nó có Tạm dừng/Tiếp tục và Dừng. Phím tắt làm được y vậy.
5. Bấm Dừng. File MP4 được lưu vào thư mục video, cửa sổ "Đã quay" cho chọn Mở video, Mở thư mục, Đóng, và thanh quay hiện lại.

## Inputs

| Người dùng chọn | Mặc định | Quyết định điều gì |
| --- | --- | --- |
| Cái để quay | màn hình có con trỏ chuột | một màn hình, một vùng, một cửa sổ, hay cả desktop |
| Tiếng hệ thống | bật | tiếng máy tính phát ra có trong video |
| Micro | tắt | tiếng micro có trong video |
| Con trỏ | bật | con trỏ chuột có vẽ trong video |
| Đếm ngược | 3 giây (0, 3, 5) | bao lâu thì bắt đầu quay |
| Số khung hình mỗi giây | 30 (15, 30, 60) | video mượt tới đâu, và file lớn tới đâu |
| Thư mục video | thư mục Video, mục Paper.ScreenWizzard | file MP4 lưu ở đâu |
| Phím tắt bắt đầu/dừng | Ctrl+Alt+R | bắt đầu quay với lựa chọn lần trước, hoặc dừng |
| Phím tắt tạm dừng | Ctrl+Alt+P | tạm dừng hoặc tiếp tục |

Các lựa chọn trên thanh quay được giữ cho lần quay sau và lần mở ứng dụng sau.

## Outputs

- Một file MP4 (hình H.264, tiếng AAC) trong thư mục video, tên theo ngày giờ bắt đầu quay.
- Cửa sổ "Đã quay"; thông báo khi có gì không làm được (xem "When it does not do the job").

## Key entities

Thanh quay, vùng quay, màn hình, vùng, cửa sổ, tiếng hệ thống, micro, đếm ngược, lượt quay, tạm dừng, file MP4.

## Quay cái gì

**Vùng quay cố định từ lúc bắt đầu.** Lựa chọn nào cũng thành một hình chữ nhật trên desktop.

- Cho hai màn hình 1920 × 1080 đặt cạnh nhau và chọn "màn hình 2" → video **1920 × 1080**, chỉ màn hình thứ hai
- Cho cùng hai màn hình và chọn "cả desktop" → video **3840 × 1080**, hai màn hình cạnh nhau, không khe
- Cho kéo vùng từ (300, 200) tới (1100, 650) → video **800 × 450** của phần màn hình đó
- Cho cửa sổ có khung nhìn thấy 1000 × 700 → video **1000 × 700**, không dính viền bóng vô hình Windows thêm vào
- Cho cửa sổ đang quay bị kéo đi trong lúc quay → video **giữ nguyên vùng cửa sổ nằm** lúc bắt đầu
- Cho vùng hay cửa sổ có cỡ lẻ, 801 × 451 → video **800 × 450** (video cần cỡ chẵn); bỏ một pixel ở mép phải và mép dưới
- Cho vùng kéo lấn ra ngoài mọi màn hình → chỉ phần nằm trên màn hình được quay
- Cho màn hình đặt 150% → video có **pixel thật** của màn hình (vùng nhìn thấy 300 × 200 thành 450 × 300)

**Ứng dụng không bao giờ quay chính nó, và tránh sang một bên.**

- Cho bấm Quay → thanh quay và thanh chụp **ẩn đi** tới khi dừng quay; thanh nào đang mở thì hiện lại sau đó
- Cho chụp ảnh (kiểu nào cũng vậy) khi thanh quay đang mở → thanh quay **ẩn đi** trong lúc chụp rồi hiện lại, như thanh chụp
- Cho thanh quay, viền vùng quay và số đếm ngược đang hiện → **không cái nào** có trong video
- Cho một thông báo hay cửa sổ "Đã quay" của ứng dụng hiện ra trong lúc quay → nó **không** có trong video

## Tiếng

- Cho bật tiếng hệ thống, tắt micro → video chỉ có **tiếng của máy**
- Cho bật cả hai → video có **cả hai, trộn lại**, cùng lúc với hình
- Cho tắt cả hai → video **không có rãnh tiếng**
- Cho bật micro mà không cắm micro nào → vẫn bắt đầu quay **không có** tiếng micro, và thông báo nói không tìm thấy micro (F4)
- Tiếng khớp với hình: sau 10 phút quay, tiếng và hình lệch nhau **dưới 0,1 giây**

## Tạm dừng và dừng

- Cho tạm dừng 20 giây rồi tiếp tục → video **không có khoảng trống, không có 20 giây đứng hình**: nó đi tiếp từ lúc tạm dừng
- Cho quay 30 giây, tạm dừng 20 giây, rồi quay thêm 30 giây → video dài **60 giây**
- Cho bấm Dừng khi đang tạm dừng → video kết thúc **ở chỗ tạm dừng**
- Cho bấm phím tắt bắt đầu/dừng trong lúc đếm ngược → **không quay gì** và không có file nào
- Cho thoát ứng dụng từ khay khi đang quay → lượt quay **được dừng và lưu trước**, rồi ứng dụng mới thoát

## File

- Cho bắt đầu quay ngày 2026-09-27 lúc 14:03:05 → file tên **Recording 2026-09-27 14.03.05.mp4**
- Cho đã có file cùng tên → file mới tên **Recording 2026-09-27 14.03.05 (2).mp4**; file cũ **không bị ghi đè**
- Cho thư mục video chưa có → nó **được tạo**
- File mở được bằng Windows Media Player, ứng dụng Phim & TV, và trình duyệt

## Edge cases

- Một lúc chỉ một lượt quay. Trong lúc quay, phím tắt chụp vẫn chụp ảnh; thanh quay và viền không có trong ảnh.
- Lượt quay vẫn chạy khi các cửa sổ khác của ứng dụng mở hay đóng.

## When it does not do the job

| # | Case | What the user sees |
| --- | --- | --- |
| F1 | cửa sổ đang quay bị đóng | lượt quay tự dừng và phần đã quay được lưu; cửa sổ "Đã quay" nói cửa sổ đã bị đóng |
| F2 | màn hình đang quay bị rút ra, hay màn hình đổi (độ phân giải, tỉ lệ) trong lúc quay | lượt quay tự dừng và phần đã quay được lưu; thông báo nói vì sao |
| F3 | ổ đĩa đầy hay không ghi được vào thư mục video | lượt quay dừng; thông báo nêu thư mục và lý do; không để lại file ghi dở trông như một video |
| F4 | bật micro mà không có micro, hay micro bị rút trong lúc quay | vẫn quay tiếp, không có micro; thông báo nói mất micro |
| F5 | không thu được tiếng của máy (không có thiết bị phát) | vẫn quay tiếp, không có tiếng hệ thống; thông báo nói vậy |
| F6 | không bắt đầu quay được (trình điều khiển đồ hoạ từ chối chụp màn hình, không có bộ mã hoá video) | không quay gì; một hộp thoại nêu lý do; ứng dụng vẫn chạy |
| F7 | vùng chọn nhỏ hơn 16 × 16 | không cho bấm Quay, thông báo xin một vùng lớn hơn |
| F8 | phím tắt bắt đầu/dừng hay tạm dừng bị chương trình khác giữ | các phím khác và thanh quay vẫn chạy; thông báo nêu phím nào (như với phím tắt chụp) |

## Assumptions

- Windows 10 bản 2004 trở lên (cửa sổ của ứng dụng được loại khỏi video nhờ một tính năng Windows có từ bản đó).
- 10 phút quay ở 1920 × 1080, 30 khung hình mỗi giây, tốn nhiều nhất khoảng 300 MB.
- Nội dung có bảo vệ (một số trình phát video, DRM) có thể ra màu đen; đó là Windows chặn, không phải lỗi.
- Vùng quay cố định từ lúc bắt đầu; cửa sổ di chuyển thì không bám theo.
- Phím tắt mặc định là lựa chọn đầu; người dùng đổi được trong Cài đặt như phím tắt chụp.

## Clarifications

### Session 2026-09-27

- Q: đợt 2 quay những gì? (Hùng 2026-09-20, theo FastStone Screen Recorder) -> A: một màn hình, một vùng tự kéo, một cửa sổ hay cả
  desktop; tiếng hệ thống và micro bật tắt riêng; đếm ngược; phím tắt; MP4.
- Q: webcam và icon dán lên mặt? (Hùng 2026-09-27: "Chưa có thể hiện webcame nhỉ, rùi làm thêm mí filter icon chèn mặt dc ko") ->
  A: có, thành đợt 2b (webcam chồng lên video) và 2c (icon bám theo khuôn mặt), sau khi đợt này phát hành.
- Q: cửa sổ đang quay mà di chuyển thì có bám theo không? -> A: không, vùng giữ ở chỗ cửa sổ nằm lúc bắt đầu (Hùng: "ok").
- Q: phím mặc định Ctrl+Alt+R và Ctrl+Alt+P? -> A: được (Hùng: "Ok").
- Q: cửa sổ của ứng dụng khi chụp hay quay? (Hùng: "với cái giao diện khi chụp hay tắt thì ẩn cái giao diện ban đầu đi nha, dống
  fastron hoạt động ý") -> A: chúng ẩn đi, và biểu tượng khay giữ các nút điều khiển trong lúc quay.

## What it does not do yet

- Webcam chồng lên video (đợt 2b) và icon lên mặt (đợt 2c).
- Làm nổi cú bấm chuột hay con trỏ, phóng theo con trỏ, vùng cỡ cố định, lặp lại vùng lần trước.
- Sửa hay cắt video, xuất GIF.
- Quay một cửa sổ đang bị cửa sổ khác che (chỉ quay cái đang thấy trên màn hình).
