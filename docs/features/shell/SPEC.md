> Đợt 1 đã làm xong 2026-09-21 (SPEC được duyệt 2026-09-20). Việc làm và bằng chứng: [2026-09-20-m1-chup-va-sua-anh-plan.md](2026-09-20-m1-chup-va-sua-anh-plan.md). Tập tin cài đặt thiếu mục: [2026-09-26-cai-dat-thieu-muc-plan.md](2026-09-26-cai-dat-thieu-muc-plan.md)

# Khung ứng dụng — SPEC

Ứng dụng chạy ẩn ở khay hệ thống, luôn sẵn sàng bằng phím tắt, có một thanh chụp gọn để bấm chuột, và một cửa sổ cài
đặt. Đây là chỗ mọi tính năng khác (chụp, sửa ảnh, sau này quay và ghi chú) đứng lên.

English · Tiếng Việt — cùng một yêu cầu viết hai lần; đổi thì đổi cả hai.

# English

## User story

As someone at the computer all day, I want a screenshot tool that always waits in the background without taking room on the
taskbar, comes up with one key, and lets me set the keys, the save folder, and what happens after each capture.

## What the user does

1. Opens the app. Its icon appears in the notification area and the capture bar comes up (the first time). No main window takes the
   screen.
2. Right-clicks the tray icon: the menu has Capture rectangle, Capture freeform, Capture window, Capture full screen, Open image…,
   Capture bar (show or hide), Settings, Exit. Double-clicking the icon shows the capture bar.
3. The capture bar is a small window always on top, with one button per kind of capture and a Settings button. It can be dragged
   anywhere; next time it opens exactly there.
4. In Settings the user changes: the hotkey of each kind of capture, what happens after a capture, the save folder, the file
   format, the delay, including the pointer, starting with Windows, the language (follow Windows, Vietnamese, English), the theme
   (follow Windows, light, dark).
5. Closing the capture bar or the editor with X closes only that window; the app keeps running in the tray. Only "Exit" in the tray
   menu ends the app.

## Inputs

| The user chooses | Default | Decides |
| --- | --- | --- |
| Rectangle hotkey | PrintScreen | this key starts a rectangle capture |
| Freeform hotkey | Shift+PrintScreen | freeform capture |
| Window hotkey | Alt+PrintScreen | window capture |
| Full-screen hotkey | Ctrl+PrintScreen | full-screen capture |
| After a capture | show the "Captured" dialog | a dialog with buttons to choose, or go straight to one place; see the capture SPEC |
| Save folder | the Pictures folder, Paper.ScreenWizzard | see the capture SPEC |
| File format, JPG quality | PNG, 90 | see the capture SPEC |
| Delay, include pointer, what full screen takes | 0 seconds, off, the screen under the pointer | see the capture SPEC |
| Start with Windows | off | the app starts by itself (hidden in the tray) at sign-in |
| Language | follow Windows | the text of every window |
| Theme | follow Windows | light or dark |

## Outputs

- The tray icon, the tray menu, the capture bar, the Settings window.
- The user's settings file: every change made in Settings is kept for the next start.
- With "Start with Windows" on, the app is in the user's startup list; off, it is removed from it.

## Key entities

Tray icon, capture bar, settings, hotkey, the running copy of the app.

## Only one copy runs

- Given the app is running, opening it again → **no second copy**; the first one brings the capture bar forward
- Given X on the capture bar → the app **is still in the tray** and the hotkeys still capture

## Hotkeys

**A hotkey must be safe enough never to steal typing.**

- Given a single letter, for example A, with no other key → **refused**, the old hotkey stays
- Given Ctrl+Shift+A → **accepted**
- Given PrintScreen alone, or F9 alone → **accepted** (function keys and PrintScreen may stand alone)
- Given a key already set for another kind of capture in the app → **refused**, naming the kind that holds it
- Given a key Windows or another program already holds → **refused**, saying it could not be registered; the old key stays

**A new hotkey works at once, without a restart.**

- Given the rectangle hotkey changed from PrintScreen to Ctrl+Shift+1 → PrintScreen **no longer** captures, Ctrl+Shift+1
  **captures**

## Settings are kept

- Given the save folder changed, the app closed and opened again → the save folder **is still the new one**
- Given the capture bar dragged somewhere, closed and opened again → the bar **is exactly there**; if that place is now outside
  every screen (a monitor was unplugged) the bar shows in the **top-right corner of the primary screen**
- Given a broken settings file (it cannot be read) → the app **still runs on the default settings**, keeps the broken file with
  the .bak extension, and shows a notice saying so
- Given a settings file from an older version that lacks a setting the newer one has (for example no delay) → the app **uses
  every other setting in the file**, and the missing one takes its **default value**; no notice and no .bak
- Given a settings file with a setting of the wrong type or outside its range (JPG quality 0 or 101, a delay below 0 or above 10
  seconds, an empty save folder, a hotkey for a kind of capture that does not exist) → the file **is broken** (F1)
- Given a settings file with a setting this version does not know (written by a newer version) → that setting **is ignored** and
  the rest are used; the next save does not keep it

## Start with Windows

- Given it on, then signing in to Windows again → the app **starts hidden in the tray**, opening no window
- Given it off → the app does **not** start at sign-in

## Language and theme

- Given the language "follow Windows" on a Vietnamese Windows → every text of the windows **is in Vietnamese**; on any other
  language → **English**
- Given the language changed in Settings → the text changes **at once**, without a restart
- Given the dark theme → text and icons stay readable: text contrast **4.5:1 or more**, icons and borders **3:1 or more**, in the
  light and the dark theme alike

## Usable from the keyboard

- Given Tab through the buttons of the capture bar and of Settings → the order **follows what is seen**, and the focused button
  has a clear outline
- Given Enter in Settings → **save and close**; Esc → **close, dropping unsaved changes**
- Every button that is only an icon has a **name** a screen reader can read

## Edge cases

- The app is always in the tray while it runs; it is not on the taskbar (unless the editor or Settings is open).
- Language, theme, hotkeys and folder take effect **at once** when Save is pressed in Settings.

## When it does not do the job

| # | Case | What the user sees |
| --- | --- | --- |
| F1 | the settings file is broken (not a settings document, a setting of the wrong type, or a setting outside its range) | the app runs on the defaults; a notice says the old settings were broken and the .bak copy was kept |
| F2 | the settings file cannot be written (read-only, disk full) | a notice names the path and the reason; the app keeps running and uses the settings for this session; the changes are lost at exit and **the notice says so** |
| F3 | the start-with-Windows entry cannot be written | the switch **goes back to its old state**, and a notice gives the reason |
| F4 | a new hotkey is refused | the old key **still works**; a notice gives the reason (a single letter, taken in the app, or held by another program) |
| F5 | the typed save folder does not exist | Settings asks whether to create it; if not, that value is **not saved** and the old folder stays |
| F6 | the app is opened a second time | no new copy; the first one shows the capture bar; no error notice |
| F7 | a hotkey cannot be registered at start | a small notice that fades names the key (no box to close, since on a machine where a key is held it would come up at every start); the app and the other keys keep working |
| F8 | the settings file lacks a setting | that setting takes its default and the others are kept; no notice (a missing setting is normal after an update), only a line in the log |

## Assumptions

- No button or menu for screen recording and screen notes yet: only what works is shown, no dummy button.
- Settings are kept in the user's application folder, needing no administrator rights; the app does not run as administrator.
- Windows 10 version 1903 or later only (Windows 11 tried), because the recording of a later round needs it.
- Only Vietnamese and English.
- A hotkey made of a letter or a digit must include Ctrl, Alt or the Windows key: Shift alone is not enough, since Shift and a
  letter is just a capital letter. Function keys (F1 to F12) and PrintScreen may stand alone, except F12, which Windows keeps for
  the debugger and which is refused as a key another program holds. (The narrowest reading, chosen while building the shell;
  Hùng has not reviewed it.)
- The longest delay a settings file may hold is 10 seconds, the longest the Settings window offers.

## Clarifications

### Session 2026-09-20

- Q: does the app have a main window? -> A: no; the tray plus a floating capture bar (my default, waiting for the user's approval).
- Q: the language of the windows? -> A: Vietnamese and English, following Windows by default (waiting for the user's approval).

### Session 2026-09-26

- Q: a settings file lacking a setting? -> A: the setting takes its default and the file is not broken; only a wrong type or a
  value outside its range makes it broken (Hùng: "duyệt nha" to the proposal, then "còn việc gì thì làm đi").

## What it does not do yet

- A hotkey the app itself holds cannot be typed into the key box of Settings (Windows gives the key to the app, not to the box);
  pressing it while Settings is open does nothing. Proposal: release the keys while Settings is open. Waiting for Hùng.
- Alt+PrintScreen and Ctrl+PrintScreen, the defaults, could not be registered on the machine tried (Windows or another program holds
  them): the user changes the key in Settings.
- Automatic updates and signing of the executable.
- Syncing settings between machines.
- Running as administrator to capture windows of programs running elevated.
- Buttons for screen recording and screen notes: later rounds, see the roadmap.

# Tiếng Việt

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
- Cho tập tin cài đặt của bản cũ, thiếu một mục mà bản mới có (ví dụ thiếu mục độ trễ) → ứng dụng **vẫn dùng mọi mục còn
  lại** của tập tin, mục thiếu lấy **giá trị mặc định**; không có thông báo và không có bản .bak
- Cho tập tin cài đặt có một mục sai kiểu hay ngoài miền (chất lượng JPG 0 hoặc 101, độ trễ âm hoặc quá 10 giây, thư mục
  lưu rỗng, phím tắt cho một kiểu chụp không có) → tập tin **bị coi là hỏng** (F1)
- Cho tập tin cài đặt có mục mà bản này không biết (bản mới hơn ghi) → mục đó **bị bỏ qua**, các mục còn lại vẫn dùng; lần
  lưu sau không giữ mục đó

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
| F1 | tập tin cài đặt hỏng (không phải một tài liệu cài đặt, có mục sai kiểu, hay có mục ngoài miền) | ứng dụng chạy với mặc định; thông báo nói cài đặt cũ bị hỏng và đã giữ bản .bak |
| F2 | không ghi được tập tin cài đặt (chỉ đọc, hết chỗ) | thông báo nêu đường dẫn và lý do; ứng dụng vẫn chạy và dùng cài đặt trong phiên này; thay đổi mất khi thoát và **thông báo nói vậy** |
| F3 | không ghi được mục khởi động cùng Windows | công tắc **trở về trạng thái cũ**, thông báo nêu lý do |
| F4 | phím tắt đặt mới bị từ chối | phím cũ **vẫn hoạt động**; thông báo nêu lý do (chữ đơn, trùng trong ứng dụng, hoặc ứng dụng khác đang giữ) |
| F5 | thư mục lưu nhập vào không tồn tại | Cài đặt hỏi có tạo thư mục không; không đồng ý thì **không lưu** giá trị đó và thư mục cũ giữ nguyên |
| F6 | mở ứng dụng lần hai | không có bản mới; bản đầu hiện thanh chụp; không có thông báo lỗi |
| F7 | không đăng ký được một phím tắt lúc khởi động | một thông báo nhỏ tự tắt nêu phím nào (không có hộp phải đóng, vì trên máy có phím bị giữ thì lần nào mở cũng gặp); ứng dụng và các phím khác vẫn chạy |
| F8 | tập tin cài đặt thiếu một mục | mục đó lấy giá trị mặc định, các mục khác giữ nguyên; không thông báo (thiếu một mục là chuyện thường sau khi cập nhật), chỉ ghi một dòng vào nhật ký |

## Assumptions

- Chưa có nút hay menu cho quay màn hình và ghi chú: chỉ hiện thứ đã chạy được, không để nút giả.
- Cài đặt lưu ở thư mục ứng dụng của người dùng, không cần quyền quản trị; ứng dụng không chạy với quyền quản trị.
- Chỉ Windows 10 bản 1903 trở lên (Windows 11 đã thử) vì cần cho phần quay ở đợt sau.
- Ngôn ngữ chỉ có tiếng Việt và tiếng Anh.
- Phím tắt gồm chữ cái hay chữ số phải kèm ít nhất Ctrl, Alt hoặc phím Windows: Shift một mình không đủ, vì Shift cộng chữ chỉ là gõ chữ
  hoa. Phím chức năng (F1 tới F12) và PrintScreen được đứng riêng, riêng F12 Windows dành cho trình gỡ lỗi nên bị từ chối như phím do
  ứng dụng khác giữ. (Mình chọn cách hiểu nhỏ nhất khi làm nhóm khung ứng dụng; Hùng chưa xem lại.)
- Độ trễ dài nhất một tập tin cài đặt được ghi là 10 giây, dài nhất mà cửa sổ Cài đặt cho chọn.

## Clarifications

### Session 2026-09-20

- Q: ứng dụng có cửa sổ chính không? -> A: không; khay hệ thống cộng thanh chụp nổi (mặc định của mình, chờ người dùng duyệt).
- Q: ngôn ngữ giao diện? -> A: tiếng Việt và tiếng Anh, mặc định theo Windows (chờ người dùng duyệt).

### Session 2026-09-26

- Q: tập tin cài đặt thiếu một mục? -> A: mục đó lấy mặc định và tập tin không bị coi là hỏng; chỉ sai kiểu hay ngoài miền mới là
  hỏng (Hùng: "duyệt nha" với đề xuất, rồi "còn việc gì thì làm đi").

## What it does not do yet

- Phím tắt đang do chính ứng dụng giữ không ghi được vào ô phím của Cài đặt (Windows đưa phím cho ứng dụng, không cho ô); bấm nó khi Cài đặt mở không làm gì. Đề xuất: nhả các phím trong lúc Cài đặt mở. Chờ Hùng quyết.
- Alt+PrintScreen và Ctrl+PrintScreen mặc định không đăng ký được trên máy đã thử (Windows hay chương trình khác giữ): người dùng đổi phím trong Cài đặt.
- Cập nhật tự động và ký số file chạy.
- Đồng bộ cài đặt giữa các máy.
- Chạy với quyền quản trị để chụp cửa sổ của ứng dụng chạy quyền cao.
- Nút quay màn hình và ghi chú lên màn hình: đợt sau, xem lộ trình.
