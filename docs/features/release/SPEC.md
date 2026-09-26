> Đã làm xong 2026-09-21 theo lời Hùng ("setup đầy đủ file cài lun nha"); đổi sang Setup.exe hai ngôn ngữ cùng ngày theo lời Hùng ("giao diện trình cài làm 2 thứ tiếng dc ko"). Việc làm và bằng chứng: [2026-09-21-file-cai-plan.md](2026-09-21-file-cai-plan.md)

# File cài đặt và phát hành — SPEC

Người dùng tải một file cài, bấm đúp là dùng được, gỡ được sạch bằng Cài đặt của Windows. Phần phát hành là chỗ
duy nhất tạo ra file cài; chủ dự án là người đẩy thẻ phát hành.

English · Tiếng Việt — cùng một yêu cầu viết hai lần; đổi thì đổi cả hai.

# English

## User story

As someone using Windows, I want one installer that runs with a double-click, in Vietnamese if my Windows is in Vietnamese,
asking for no administrator rights and no .NET, so that I can start at once without copying files by hand; and when I no longer
need it, to remove it cleanly, while my pictures and settings are still there if I install it again.

## What the user does

> Changed by [2026-09-26-bieu-tuong-desktop.md](2026-09-26-bieu-tuong-desktop.md), chờ kiểm

1. Downloads `Paper.ScreenWizzard-<version>-win-x64-Setup.exe` and double-clicks it. The installer window is in **Vietnamese
   or English following the Windows display language** (on a Windows in another language, a box offers the two). There is
   the licence, a folder choice (by default inside the user's own folders), then the Install button.
2. Before the Install button, an "Additional tasks" page has the box **"Create a desktop shortcut"**, already ticked; whoever
   does not want it unticks it.
3. After installing there is a **Paper.ScreenWizzard** entry in the Start menu and (with the box left ticked) an icon on the
   desktop; the box "Run Paper.ScreenWizzard now" is ticked on the last page; running it shows the icon in the tray as when
   opened by hand.
4. To use a new version: double-click the new version's installer. It replaces the old one in place; settings, saved pictures
   and "start with Windows" stay as they were.
5. To remove it: Windows Settings, Apps, Paper.ScreenWizzard, Uninstall.
6. Whoever does not want to install downloads the **portable** `Paper.ScreenWizzard-<version>-win-x64.zip`, which runs
   unzipped anywhere and writes nothing outside the app's settings folder.
7. Beside the two files there is a `SHA256SUMS.txt` to compare checksums.

## Inputs

| Thing | Value |
| --- | --- |
| Version | three numbers `x.y.z`, from the release tag `v<x.y.z>`; a local trial build sets it by hand |
| Install folder | by default `%LocalAppData%\Programs\Paper.ScreenWizzard`; the user can change it in the installer |
| Installer language | follows Windows; to choose another, pick it in the language box (shown only when Windows uses neither language) |

## Outputs

> Changed by [2026-09-26-bieu-tuong-desktop.md](2026-09-26-bieu-tuong-desktop.md), chờ kiểm

- One installer (`Setup.exe`) for 64-bit Windows 10 and 11, with .NET inside, needing nothing else.
- One portable zip with the same content.
- One SHA-256 checksum file for both.
- After installing: the install folder holds the executable; a Start menu entry; a desktop icon (unless the user unticked it);
  an entry in the Windows Apps list with the app's name, version and icon.
- The app's icon (executable, Start menu, desktop, tray, Apps list, installer window) is **one picture**: a sheet of paper with a
  folded corner, ruled lines and an engineer's set square, inside the four corners of a screenshot viewfinder, on blue.

## Key entities

- **Installation:** one install of one version for one Windows user.
- **Portable copy:** the same executable, with no installation.

## Upgrade and uninstall

> Changed by [2026-09-26-bieu-tuong-desktop.md](2026-09-26-bieu-tuong-desktop.md), chờ kiểm

**Installing a new version over an old one replaces it; there are never two.**

- Given 1.0.0 installed, installing the 1.1.0 file → **one** entry in the Apps list, version **1.1.0**, the executable of 1.1.0
- Given 1.1.0 installed, installing the 1.0.0 file → **refused**, saying a newer version is installed; nothing changes
- Given 1.0.0 installed, installing the same 1.0.0 file again → it installs, still one entry

**There is a desktop shortcut, unless the user does not want one.**

- Given the box "Create a desktop shortcut" left ticked → the user's desktop has **one** Paper.ScreenWizzard icon, and a
  double-click opens the app
- Given the box unticked → **no** icon on the desktop; the Start menu entry is still there

**A running app does not make installing or uninstalling fail.**

- Given the app running in the tray, installing a new version or uninstalling → the app **is closed by itself**, the install or
  uninstall finishes, and **no Windows restart is asked for**

**Uninstalling removes all of the app and keeps what is the user's.**

- After uninstalling: the install folder is **gone**, the Start menu entry and the desktop icon are **gone**, the "start with
  Windows" entry (if the user had turned it on) is **gone**.
- After uninstalling, the app's settings folder (`%AppData%\Paper\ScreenWizzard`) and the pictures the user saved **are still
  there**, so installing again carries on.

## Edge cases

- Installing on Windows older than Windows 10 version 1903 → nothing is installed, and the installer says which version is needed
  (in the installer's language).
- An install folder that cannot be written → the installer reports the error and leaves nothing half installed.
- Two users on one machine: each installs for themselves, without affecting the other.

## When it does not do the job

| Code | When | The user sees |
| --- | --- | --- |
| F1 | Windows older than Windows 10 version 1903 | the installer stops at the start, saying Windows 10 version 1903 or later is needed |
| F2 | a newer version is installed | the installer stops, says a newer version is installed, changes nothing |
| F3 | the install folder cannot be written | the installer reports the error and leaves nothing half installed |
| F4 | the app is running during install or uninstall | the app closes by itself; no Windows restart is asked for |

## Assumptions

- Installed per user, not per machine: no administrator rights, nothing written to Program Files. In exchange an administrator
  cannot install once for every user of the machine.
- **Not signed yet:** Windows SmartScreen may show the blue box "Windows protected your PC" the first time the installer is
  downloaded and run; click More info, then Run anyway. Signing needs a certificate (see the ADR on the installer).
- A release is started by the project owner: pushing the tag `v<x.y.z>` runs the build and leaves a draft on GitHub. An agent
  pushes a tag or clicks Publish only when the owner says so in that session.
- The Vietnamese text of the installer window is the community translation shipped with Inno Setup; the project's own lines
  ("newer version installed", "Run now") are written by the project.

## What it does not do yet

- Signing the executable and the installer.
- Updating itself from inside the app.
- Installing for every user of the machine (needs administrator rights).
- A build for Windows on ARM.
- The Windows 10 version 1903 condition (F1) is checked by reading the installer script's setting, not run on an older Windows
  (no machine).

# Tiếng Việt

## User story

Là người dùng máy Windows, tôi muốn một file cài đặt bấm là chạy, bằng tiếng Việt nếu máy tôi dùng tiếng Việt, không đòi
quyền quản trị và không đòi cài .NET, để dùng được ngay mà không phải chép file bằng tay; khi không cần nữa thì gỡ được sạch,
còn ảnh và cài đặt của tôi vẫn còn nếu tôi cài lại.

## What the user does

> Đổi bởi [2026-09-26-bieu-tuong-desktop.md](2026-09-26-bieu-tuong-desktop.md), chờ kiểm

1. Tải `Paper.ScreenWizzard-<số phiên bản>-win-x64-Setup.exe`, bấm đúp. Cửa sổ cài hiện bằng **tiếng Việt hay tiếng
   Anh theo ngôn ngữ hiển thị của Windows** (Windows dùng ngôn ngữ khác thì hiện một hộp chọn giữa hai thứ tiếng). Có lời
   cấp phép, chỗ chọn thư mục (mặc định nằm trong thư mục riêng của người dùng), rồi nút Cài đặt.
2. Trước nút Cài đặt có trang "Các thiết lập bổ sung" với ô **"Tạo biểu tượng trên Màn hình chính (Desktop)"** đã được chọn
   sẵn; ai không muốn thì bỏ chọn.
3. Cài xong, có mục **Paper.ScreenWizzard** trong menu Start và (nếu để ô trên) một biểu tượng trên màn hình nền; ô "Chạy
   Paper.ScreenWizzard ngay" đã được chọn sẵn ở cửa sổ cuối; chạy ngay thì biểu tượng hiện ở khay như lúc mở tay.
4. Muốn dùng bản mới: bấm đúp file cài của bản mới. Nó thay bản cũ tại chỗ; cài đặt, ảnh đã lưu và việc "khởi động
   cùng Windows" vẫn như cũ.
5. Muốn gỡ: Cài đặt của Windows, Ứng dụng, Paper.ScreenWizzard, Gỡ cài đặt.
6. Ai không muốn cài thì tải bản **di động** `Paper.ScreenWizzard-<số phiên bản>-win-x64.zip`, giải nén ở đâu cũng
   chạy được, không ghi gì ngoài thư mục cài đặt của ứng dụng.
7. Cạnh hai file có một file `SHA256SUMS.txt` để so mã kiểm tra.

## Inputs

| Thứ | Giá trị |
| --- | --- |
| Số phiên bản | ba số `x.y.z`, lấy từ tên thẻ phát hành `v<x.y.z>`; dựng thử ở máy thì tự đặt |
| Thư mục cài | mặc định `%LocalAppData%\Programs\Paper.ScreenWizzard`; người dùng đổi được ở cửa sổ cài |
| Ngôn ngữ cài | theo Windows; ai muốn chọn khác thì bấm chọn ở hộp ngôn ngữ (chỉ hiện khi Windows dùng ngôn ngữ khác hai thứ tiếng trên) |

## Outputs

> Đổi bởi [2026-09-26-bieu-tuong-desktop.md](2026-09-26-bieu-tuong-desktop.md), chờ kiểm

- Một file cài (`Setup.exe`) cho Windows 10 và 11 bản 64 bit, tự chứa .NET, không cần cài gì thêm.
- Một file zip di động cùng nội dung.
- Một file mã kiểm tra SHA-256 cho cả hai.
- Sau khi cài: thư mục cài có file chạy; mục menu Start; biểu tượng trên màn hình nền (trừ khi người dùng bỏ chọn); mục trong danh sách Ứng dụng của Windows với tên, số phiên
  bản và biểu tượng của ứng dụng.
- Biểu tượng của ứng dụng (file chạy, menu Start, màn hình nền, khay, danh sách Ứng dụng, cửa sổ cài) là **một hình**: tờ giấy gập góc có
  đường kẻ và ê-ke của người làm kỹ thuật, nằm trong bốn góc khung ngắm chụp màn hình, trên nền xanh.

## Key entities

- **Bản cài:** một lần cài của một số phiên bản cho một người dùng Windows.
- **Bản di động:** cùng file chạy, không có bản cài.

## Cài đè và gỡ

> Đổi bởi [2026-09-26-bieu-tuong-desktop.md](2026-09-26-bieu-tuong-desktop.md), chờ kiểm

**Cài bản mới lên bản cũ thay bản cũ, không tạo hai bản.**

- Cho bản 1.0.0 đang cài, cài file 1.1.0 → chỉ còn **một** mục trong danh sách Ứng dụng, số phiên bản là **1.1.0**,
  file chạy là của 1.1.0.
- Cho bản 1.1.0 đang cài, cài file 1.0.0 → **bị từ chối** và nói rõ đã có bản mới hơn; không có gì bị đổi.
- Cho bản 1.0.0 đang cài, cài lại đúng file 1.0.0 → cài xong, vẫn một mục.

**Có lối tắt trên màn hình nền, trừ khi người dùng không muốn.**

- Cho cài mà để nguyên ô "Tạo biểu tượng trên Màn hình chính" → trên màn hình nền của người dùng có **một** biểu tượng
  Paper.ScreenWizzard, bấm đúp là mở ứng dụng
- Cho cài mà bỏ chọn ô đó → **không có** biểu tượng trên màn hình nền; mục menu Start vẫn có

**Ứng dụng đang chạy không làm cài hay gỡ thất bại.**

- Cho ứng dụng đang chạy ở khay, cài bản mới hay gỡ → ứng dụng **tự được tắt**, việc cài hay gỡ xong, và **không đòi
  khởi động lại Windows**.

**Gỡ là gỡ sạch phần của ứng dụng, giữ phần của người dùng.**

- Sau khi gỡ: thư mục cài **không còn**, mục menu Start và biểu tượng trên màn hình nền **không còn**, mục "khởi động cùng Windows" (nếu người dùng
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
