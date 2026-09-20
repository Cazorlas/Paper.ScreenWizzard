# Paper.ScreenWizzard đợt 1: khung, chụp, sửa ảnh — plan — 2026-09-20

**Trạng thái:** đã duyệt 2026-09-20 ("mình duyệt nha")
**Loại việc:** code

Dựng solution .NET 10 bốn tầng (ADR 0001, phương án A) và làm ba tính năng đầu: khung ứng dụng (khay, phím tắt, cài đặt),
chụp màn hình bốn kiểu, trình sửa ảnh. Không host nào: thứ chạy thật là chính file exe trên desktop tương tác.
Brief: [2026-09-20-m1-chup-va-sua-anh.md](2026-09-20-m1-chup-va-sua-anh.md) · Luật: [SPEC.md](SPEC.md) (shell),
[../capture/SPEC.md](../capture/SPEC.md), [../editor/SPEC.md](../editor/SPEC.md)

## Context

- Kho trống. Mọi thứ dưới đây là mới. Tầng và target: xem `CLAUDE.md` và ADR 0001.
- Kho tham khảo: PaperTodo (WPF, một app nhỏ giữ cửa sổ nổi và lưu vị trí) chỉ để học cách bố cục; Fluent-Screen-Recorder
  dành cho đợt 2. Không chép mã của kho nào.
- **`PaperLibrary` (RevitAPI-C) không dùng được:** nó là project trong solution Revit, không phải gói NuGet, và ràng buộc
  "không tham chiếu mã nguồn dự án khác" của kho này cấm liên kết file. Vì vậy `.App/Mvvm/` tự giữ `BindableBase`,
  `CommandBase`, `AsyncCommandBase` nhỏ, cùng tên và hình dạng như trong `paper-wpf-style`, để sau này đổi sang
  thư viện dùng chung nếu nó được tách thành gói.
- **Dịch vụ dùng chung:**
  - transaction/undo: **cần**, là lịch sử của trình sửa ảnh (UseCases/Editor), tự làm trong T11-T12, không phải dịch vụ chung;
  - progress có huỷ: **không cần** ở đợt này (ảnh chụp tức thì);
  - thông báo/báo cáo: **thiếu** → T2 khai cổng thông báo (`INotificationPort`: dòng báo nhỏ và hộp lỗi), T6 cài;
  - lưu cài đặt: **thiếu** → T2 khai `ISettingsStorePort`, T17 cài `%AppData%\Paper\ScreenWizzard\configs\settings.json`;
  - xử lý lỗi/cảnh báo: mỗi tính năng trả kết quả có tên lỗi (F-code), ViewModel hiện; không ném ngoại lệ ra UI;
  - logging: **thiếu** → T2 khai `ILogPort`, T17 cài ghi file `%AppData%\Paper\ScreenWizzard\logs\`.
- **Bài học đã nhận áp dụng:** không có (kho mới, sổ `retro` chưa có).
- **Bug liên quan:** không có.

## Rules that apply

- ADR 0001: quyết định ở UseCases/Domain, host chỉ qua port, dữ liệu qua port là dữ liệu thuần (mảng byte pixel BGRA,
  toạ độ nguyên pixel vật lý, không có `System.Windows`/`System.Drawing` trong hai tầng đó).
- `CLAUDE.md`: property/field private `_camelCase`; lệnh là class có tên; chữ hiển thị qua `DynamicResource`.
- Mỗi test bắt đầu bằng mã `F<n>` của dòng nó phủ; lớp test đặt theo tính năng nên `F2` của chụp và `F2` của khung không
  lẫn nhau (task ghi rõ `capture/F2`).
- `paper-wpf-style`: tương phản 4.5:1 chữ và 3:1 biểu tượng ở cả hai giao diện, viền focus rõ, biểu tượng có tên, dùng
  được bằng bàn phím, tôn trọng cài đặt tắt hiệu ứng.
- Gói NuGet: bản mới nhất mà `net10.0` cho phép, kiểm bằng `dotnet list package --outdated` ở T1.

## Decisions

- **Solution `.slnx`, bốn project src và ba project test** — `Domain`, `UseCases` (`net10.0`), `Infrastructure`, `App`
  (`net10.0-windows10.0.19041.0`, WinExe); `UnitTests` (`net10.0`), `UiTests`, `E2eTests` (`-windows`). Rejected:
  Presentation thành project thứ năm, vì compiler đã giữ ranh giới còn lại (ADR 0001).
- **Test bằng NUnit 4** — theo Paper.AutoCad, để một cách viết test cho mọi dự án Paper. Rejected: xUnit, không có lý do
  khác biệt đủ để tách.
- **Toạ độ luôn là pixel vật lý của desktop ảo, process per-monitor DPI v2 (khai trong manifest)** — chụp và chọn cùng một
  hệ toạ độ, nên không có bước đổi đơn vị nào để sai. WPF làm việc bằng đơn vị hiển thị, nên lớp phủ chọn vùng đổi ở đúng
  một chỗ (một hàm trong App) và có test.
- **Chụp bằng GDI `BitBlt` từ desktop ảo, một lần lúc bấm (hay hết đếm ngược); lớp phủ hiện ảnh đó** — ảnh đứng yên trong
  lúc chọn, và ảnh ra không bao giờ chứa lớp phủ (SPEC chụp: F-rows và dòng "Ảnh không bao giờ chứa…"). Rejected:
  Windows.Graphics.Capture cho ảnh tĩnh, vì nó dành cho quay, cần thêm WinRT và chậm khởi động; sẽ dùng ở đợt 2.
- **Khung cửa sổ = `DWMWA_EXTENDED_FRAME_BOUNDS`, không phải `GetWindowRect`** — để ảnh không dính viền bóng vô hình
  (SPEC chụp, Cửa sổ). Danh sách cửa sổ chụp cùng lúc với ảnh đóng băng.
- **Trình sửa ảnh giữ hình vẽ là danh sách vật thể, ghép vào pixel chỉ lúc lưu / chép** — chọn, kéo, đổi màu, xoá được
  cho tới lúc lưu (SPEC sửa). Vẽ ra pixel bằng WPF `RenderTargetBitmap` ở App (cần font và bút), còn **mọi luật**
  (lịch sử, đánh số, ghép ô vuông làm mờ, phép cắt, ràng buộc Shift) ở UseCases/Domain trên số nguyên và mảng byte.
  Rejected: vẽ thẳng lên bitmap ngay từ đầu, vì không sửa lại được và Undo phải lưu cả ảnh.
- **Làm mờ = ghép ô vuông 12 px** — không đảo ngược được (SPEC sửa, Làm mờ).
- **Sau khi chụp là một hộp thoại chọn nơi, không mở thẳng trình sửa** — Hùng nói 2026-09-20 (theo FastStone). Quyết định
  "ảnh đi đâu" thuộc UseCases: interactor chụp trả về một **plan** (mở hộp thoại, hay đi thẳng tới trình sửa/clipboard/file
  theo cài đặt), rồi mỗi nút của hộp thoại gọi một lệnh của interactor và nhận kết quả có mã lỗi (capture/F4, F5).
  Hộp thoại giữ ảnh của riêng nó, nên nhiều hộp cùng mở được. Rejected: hộp thoại tự gọi cổng file và clipboard, vì lúc đó
  F4 và F5 chỉ kiểm được khi có Windows.
- **Mỗi phím tắt toàn cục qua `RegisterHotKey`** — không hook bàn phím toàn cục, không cần quyền cao, và phím đã bị ứng
  dụng khác giữ báo lỗi ngay (khung F4, chụp F1).
- **Tray bằng `System.Windows.Forms.NotifyIcon`** trong `App` — WPF không có khay sẵn; chỉ dùng lớp này, không dùng
  WinForms cho việc khác. Rejected: gói bên thứ ba cho một biểu tượng khay.
- **Ngôn ngữ: hai `ResourceDictionary` vi/en đổi được lúc chạy**, khoá dùng chung cho cả ba tính năng.
- **Đợt 2 (quay) và 3 (ghi chú) chưa có SPEC**; xem `docs/roadmap.md`.

## UI wireframe

```text
Thanh chụp (luôn trên cùng, kéo được)         Menu khay
┌──────────────────────────────────────┐      ┌───────────────────────┐
│ [▭ Vùng] [✎ Tự do] [▢ Cửa sổ] [⛶ Cả màn] │ [⚙] │ Chụp vùng chữ nhật  PrtScn │
└──────────────────────────────────────┘      │ Chụp vùng tự do   Shift+ … │
                                              │ Chụp cửa sổ       Alt+ …   │
Lớp phủ chọn vùng (mọi màn hình, ảnh đóng băng) │ Chụp toàn màn hình Ctrl+ … │
┌────────────────────────────────────────┐    │ Mở ảnh…                    │
│  (ảnh đóng băng, mờ 40%)                  │    │ Thanh chụp   ✓             │
│      ┌─────────────┐                    │    │ Cài đặt                    │
│      │ vùng sáng    │ 412 × 268         │    │ Thoát                      │
│      └─────────────┘                    │    └───────────────────────┘
│  Esc / chuột phải: huỷ                   │
└────────────────────────────────────────┘

Hộp thoại "Đã chụp" (luôn trên cùng, giữa màn hình vừa chụp; mỗi lần chụp một hộp riêng)
┌────────────────────────────────────────────┐
│ Đã chụp                                  ✕ │
├────────────────────────────────────────────┤
│  ┌──────────────────────┐   1280 × 720     │
│  │  ảnh thu nhỏ          │   PNG             │
│  └──────────────────────┘                  │
│  [Lưu] [Lưu thành…] [Sao chép] [Sửa] [Bỏ]  │
└────────────────────────────────────────────┘

Trình sửa ảnh
┌──────────────────────────────────────────────────────────────────────┐
│ [Chọn][Bút][Dạ quang][Đường][Mũi tên][Khung][Elip][Chữ][1][Mờ][Cắt] │
│ Màu ●●●●●●●● [+]  Dày ▬▬▬ 4   Chữ 18       [↶][↷]  [Vừa khung] 100%  │
├──────────────────────────────────────────────────────────────────────┤
│                    (ảnh + các hình vẽ, cuộn / phóng to)               │
├──────────────────────────────────────────────────────────────────────┤
│ 1280 × 720   Chưa lưu                              [Sao chép] [Lưu…] │
└──────────────────────────────────────────────────────────────────────┘

Cài đặt: các nhóm Phím tắt · Sau khi chụp · Lưu file · Khởi động cùng Windows · Ngôn ngữ · Giao diện; Enter lưu, Esc đóng.
```

## Tasks

Thứ tự: test đỏ trước code trong mỗi lane; mock UI trước UI thật và trước chạy thật; lane `e2e` chạy sau các nhóm `ui`
và không cùng nhóm với chúng (cùng cần màn hình); `find-bug`, review kiến trúc và đóng SPEC cuối. Lane trong một nhóm chạy
song song, mỗi lane một lane agent, chỉ sửa file trong `{files:}` của task mình. Mã `shell/F<n>`, `capture/F<n>`,
`editor/F<n>` là dòng của bảng "When it does not do the job" ở SPEC tương ứng; mỗi mã một test bắt đầu bằng `F<n>_`.

### 1. Khung solution và hợp đồng — một luồng, chưa có lane song song

- [ ] T1 Dựng solution `Paper.ScreenWizzard.slnx`, bốn project src và ba project test (không có logic), `Directory.Build.props` (`net10.0`, nullable, warning là lỗi), `.gitignore`, `.editorconfig`, thư mục `Mvvm` của App; kiểm bản mới nhất của gói NuGet; khai verbs `build`, `test`, `ui`, `e2e` vào `.claude/paper.profile.json` (kèm `verify.watch`, `featureDocs`, `docsSource`, `architecture.platformFree` cho Domain và UseCases, `api.namespaces` cho `System.Windows`, `Windows.` và `Microsoft.Win32`) — xong khi verb `build` exit 0 và `git status` không có file sinh ra ngoài `.gitignore` {files: Paper.ScreenWizzard.slnx, Directory.Build.props, Directory.Build.targets, .gitignore, .editorconfig, src/**/*.csproj, tests/**/*.csproj, src/Paper.ScreenWizzard.App/Mvvm/**, src/Paper.ScreenWizzard.App/app.manifest, .claude/paper.profile.json}
- [ ] T2 Hợp đồng của ba tính năng, chỉ interface và record thuần (không logic): cổng `IScreenSourcePort`, `IWindowCatalogPort`, `IMonitorCatalogPort`, `IClipboardPort`, `IFileStorePort`, `IHotkeyPort`, `ISettingsStorePort`, `IAutostartPort`, `INotificationPort`, `ILogPort`, `IClockPort`; interactor `ICaptureInteractor`, `IEditorInteractor`, `IShellInteractor`; record vào/ra (vùng, đường bao, cửa sổ, kết quả có mã lỗi F); kiểu giá trị Domain (điểm, hình chữ nhật, màu) — xong khi verb `build` exit 0 và không interface nào nhắc kiểu của `System.Windows` hay `System.Drawing` (sau T1) {files: src/Paper.ScreenWizzard.Domain/**, src/Paper.ScreenWizzard.UseCases/**/Ports/**, src/Paper.ScreenWizzard.UseCases/**/Models/**}

### 2. Khung ứng dụng (shell) — logic và giao diện song song

- [ ] T3 [red][unit] Test cho SPEC shell: "Phím tắt" (chữ đơn A bị từ chối, Ctrl+Shift+A nhận, PrintScreen và F9 đứng riêng nhận, phím trùng kiểu chụp khác bị từ chối kèm tên, phím ứng dụng khác giữ bị từ chối và phím cũ giữ nguyên, đổi phím có hiệu lực ngay), "Cài đặt được giữ lại" (thư mục, vị trí thanh chụp ngoài màn hình → góc trên phải màn hình chính, file hỏng → mặc định cộng .bak), "Khởi động cùng Windows", "Ngôn ngữ và giao diện" (chọn vi/en theo culture), quyết định "Chỉ một bản chạy"; F1 F2 F3 F4 F5 F6 F7 của shell — xong khi đỏ ở **assertion**, không đỏ vì build {files: tests/Paper.ScreenWizzard.UnitTests/Shell/**}
- [ ] T4 [unit] Code Domain/UseCases của shell tới khi T3 xanh; verb `test` — xong khi exit 0 và số test đã chạy > 0 {files: src/Paper.ScreenWizzard.UseCases/Shell/Implements/**, src/Paper.ScreenWizzard.Domain/Shell/**, tests/Paper.ScreenWizzard.UnitTests/Shell/**}
- [ ] T5 [red][ui] Mock UI trên dữ liệu giả theo wireframe: thanh chụp, cửa sổ Cài đặt, menu khay (nội dung), thông báo; SPEC shell "Dùng được bằng bàn phím" (thứ tự Tab, Enter lưu, Esc bỏ, tên cho nút biểu tượng), "Ngôn ngữ và giao diện" (đổi chữ ngay, tương phản 4.5:1 và 3:1 ở cả hai giao diện), shell F4 F5 hiện thông báo — xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UiTests/Shell/**}
- [ ] T6 [ui] View và ViewModel của thanh chụp, Cài đặt, menu khay, cài đặt của cổng thông báo; chạy verb `ui`, **mở ảnh sáng và tối ra xem** — xong khi T5 xanh và ảnh khớp wireframe {files: src/Paper.ScreenWizzard.App/Views/Shell/**, src/Paper.ScreenWizzard.App/ViewModels/Shell/**, src/Paper.ScreenWizzard.App/Resources/**, tests/Paper.ScreenWizzard.UiTests/Shell/**}

### 3. Chụp màn hình — logic và giao diện song song

- [ ] T7 [red][unit] Test cho SPEC capture: "Vùng chữ nhật" (kéo ngược hướng, cắt theo mép, toạ độ âm hợp lệ, hai màn hình liền, pixel thật ở 150%), "Vùng tự do" (hình bao, trong/ngoài đường bao, đường chưa khép), "Cửa sổ" (trên cùng, bỏ ẩn/thu nhỏ/lớp phủ, nền desktop → cả màn hình, khung thấy được không có viền bóng, cửa sổ phóng to), "Toàn màn hình" (màn hình có con trỏ, tất cả), "Độ trễ và con trỏ", "Sau khi chụp" (hộp thoại là mặc định và chưa ghi gì; Sao chép, Lưu, Lưu thành… huỷ thì quay về hộp thoại, Sửa, Bỏ; nhiều hộp thoại độc lập; cài đặt tự đi thẳng tới clipboard hay trình sửa thì không có hộp thoại; đặt tên, (2) (3) không ghi đè, JPG nền trắng, PNG giữ trong suốt), và capture/F2 F3 F4 F5 F6 F8 F9 F10 (F1 và F7 ở nhóm 5) — xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UnitTests/Capture/**}
- [ ] T8 [unit] Code UseCases/Domain của capture tới khi T7 xanh; verb `test` — xong khi exit 0 và số test đã chạy > 0 {files: src/Paper.ScreenWizzard.UseCases/Capture/Implements/**, src/Paper.ScreenWizzard.Domain/Capture/**, tests/Paper.ScreenWizzard.UnitTests/Capture/**}
- [ ] T9 [red][ui] Mock lớp phủ chọn vùng trên **ảnh đóng băng giả**: kéo chữ nhật hiện "rộng × cao", vẽ tay vùng tự do, cửa sổ sáng viền và tên, Esc và chuột phải huỷ, đếm ngược 5 tới 1, capture/F2 và F3 hiện "vùng quá nhỏ" và ở lại màn chọn, đổi đơn vị hiển thị → pixel ở đúng một hàm, **hộp thoại "Đã chụp"** (ảnh thu nhỏ, kích thước, năm nút có tên, Esc = Bỏ, capture/F4 và F5 hiện lỗi mà hộp vẫn mở, Tab và viền focus, hai giao diện) — xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UiTests/Capture/**}
- [ ] T10 [ui] View và ViewModel của lớp phủ chọn vùng, đếm ngược và hộp thoại "Đã chụp"; verb `ui`, **mở ảnh ra xem** — xong khi T9 xanh và ảnh khớp wireframe {files: src/Paper.ScreenWizzard.App/Views/Capture/**, src/Paper.ScreenWizzard.App/ViewModels/Capture/**, tests/Paper.ScreenWizzard.UiTests/Capture/**}

### 4. Sửa ảnh — logic và giao diện song song

- [ ] T11 [red][unit] Test cho SPEC editor: "Hình vẽ và lịch sử" (Undo/Redo theo bước, nhánh Redo bị bỏ, đổi màu, kéo đi), "Đường thẳng, mũi tên, khung, elip" (ràng buộc Shift), "Số bước" (1,2,3; xoá 3 → 3; xoá 2 → 4), "Làm mờ" (ô 12 × 12 một màu, không khôi phục được), "Cắt" (400 × 300, dịch hình, bỏ hình ngoài, kẹp mép), "Lưu và chép" (quyết định hỏi ghi đè, JPG nền trắng), editor/F1 F2 F3 F4 F5 F6 F7 F8 F9 — xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UnitTests/Editor/**}
- [ ] T12 [unit] Code UseCases/Domain của editor tới khi T11 xanh; verb `test` — xong khi exit 0 và số test đã chạy > 0 {files: src/Paper.ScreenWizzard.UseCases/Editor/Implements/**, src/Paper.ScreenWizzard.Domain/Editor/**, tests/Paper.ScreenWizzard.UnitTests/Editor/**}
- [ ] T13 [red][ui] Mock trình sửa trên ảnh giả: dạ quang giữ chữ đen, mũi tên nhọn ở (300, 200), chữ "Đường ống 45° — thử nghiệm" đủ dấu, khung/đường/elip kéo ngược hướng, vị trí hình ở (150, 100) không lệch, lưu ở phóng 400% vẫn đúng cỡ, Ctrl+C không phụ thuộc mức phóng, editor/F1 F7 hiện hộp thoại và không đóng cửa sổ, editor/F6 nút mờ — xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.UiTests/Editor/**}
- [ ] T14 [ui] View, ViewModel, bộ vẽ ra pixel (`RenderTargetBitmap`) và các công cụ của trình sửa; verb `ui`, **mở ảnh ra xem** — xong khi T13 xanh và ảnh khớp wireframe {files: src/Paper.ScreenWizzard.App/Views/Editor/**, src/Paper.ScreenWizzard.App/ViewModels/Editor/**, src/Paper.ScreenWizzard.App/Rendering/**, tests/Paper.ScreenWizzard.UiTests/Editor/**}

### 5. Chạy thật trên Windows — sau các nhóm ui, không cùng lúc với lane ui

- [ ] T15 Tra API Win32 và WPF sẽ gọi ở `docsSource` (bảng "API đã tra" bên dưới) trước khi viết adapter — xong khi mỗi member có một dòng với trang đã đọc
- [ ] T16 [red][e2e] Test adapter thật trên desktop tương tác: dựng một cửa sổ thử có các khối màu ở toạ độ biết trước rồi chụp vùng / cửa sổ và so từng pixel (không dính viền bóng); danh sách cửa sổ và màn hình; đăng ký phím tắt và phím đã bị giữ (capture/F1, shell/F7); clipboard vòng đi vòng lại; file store vào thư mục tạm chỉ đọc (capture/F4); settings file hỏng; khởi động cùng Windows ghi rồi gỡ mục của người dùng hiện tại; chỉ một bản chạy — xong khi đỏ ở assertion {files: tests/Paper.ScreenWizzard.E2eTests/Adapters/**}
- [ ] T17 [e2e] Adapter Infrastructure và gốc ghép (`App.xaml.cs`, khay, manifest DPI); chạy verb `e2e` — xong khi T16 xanh và số test đã chạy > 0 {files: src/Paper.ScreenWizzard.Infrastructure/**, src/Paper.ScreenWizzard.App/App.xaml, src/Paper.ScreenWizzard.App/App.xaml.cs, src/Paper.ScreenWizzard.App/Startup/**, tests/Paper.ScreenWizzard.E2eTests/Adapters/**}
- [ ] T18 [e2e] Điều khiển **chính file exe** như người dùng: bấm PrintScreen bằng `SendInput`, kéo chuột trên lớp phủ, xác nhận **hộp thoại "Đã chụp" hiện** đúng cỡ ảnh rồi bấm Sửa để ảnh mở trong trình sửa; bấm Lưu, Sao chép, Bỏ và đọc lại file, clipboard; vẽ mũi tên, số bước và làm mờ rồi lưu, đọc file lại so pixel; chụp cửa sổ và toàn màn hình; mở bản thứ hai (shell/F6); mở ảnh; **mở ảnh chụp màn hình lúc chạy ra xem** — xong khi mỗi dòng "Cho … →" của ba SPEC có một giá trị đọc lại, và capture/F7 ghi `not verifiable` kèm lý do {files: tests/Paper.ScreenWizzard.E2eTests/Drive/**}

### Last. Close

- [ ] T19 Test kiến trúc canh ADR 0001: hướng tham chiếu, Domain và UseCases nhắm `net10.0` không `-windows`, ViewModel không gọi Infrastructure, lớp trong hai tầng không nhắc `System.Windows`/`System.Drawing` — xong khi verb `test` exit 0 và số test đã chạy > 0 {files: tests/Paper.ScreenWizzard.UnitTests/Architecture/**}
- [ ] T20 `find-bug` trên ba SPEC.md, mỗi phát hiện có input được người đọc độc lập xác nhận — xong khi mỗi phát hiện đã thành test hoặc vào báo cáo
- [ ] T21 Agent `architecture-reviewer` trên các file plan này đổi (`review-files`) — xong khi đọc N/N và 0 vi phạm
- [ ] T22 Đóng ba SPEC.md (gỡ banner "Bản nháp chờ duyệt", xoá dòng bị thay), viết `CODEMAP.md` cho từng project, `check-spec` và `check-code-map` sạch, cập nhật `docs/roadmap.md` và `README.md` — xong khi hai lệnh kiểm exit 0 {files: src/**/CODEMAP.md, tests/**/CODEMAP.md, CODEMAP.md, docs/features/**/SPEC.md, docs/roadmap.md, README.md}

## API đã tra

Nguồn tra khai ở `docsSource` trong `.claude/paper.profile.json` (learn.microsoft.com, WPF và Win32). **Chưa tra:** T15 làm
việc này trước khi có dòng code nào gọi chúng; bảng dưới là danh sách sẽ tra, không phải đã tra.

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|
| `RegisterHotKey` / `UnregisterHotKey` | Win32 | chưa tra (T15) | phím bị giữ trả lỗi; cần cửa sổ nhận `WM_HOTKEY` |
| `BitBlt`, `GetDC`, `CreateDIBSection` | Win32 GDI | chưa tra (T15) | pixel vật lý; `CAPTUREBLT` để lấy cửa sổ layered |
| `DwmGetWindowAttribute` với `DWMWA_EXTENDED_FRAME_BOUNDS` | Win32 DWM | chưa tra (T15) | khung thấy được, không có bóng |
| `EnumWindows`, `IsWindowVisible`, `IsIconic`, `DwmGetWindowAttribute` với `DWMWA_CLOAKED` | Win32 | chưa tra (T15) | bỏ cửa sổ ẩn, thu nhỏ, bị "cloaked" |
| `EnumDisplayMonitors`, `GetMonitorInfo`, `MonitorFromPoint` | Win32 | chưa tra (T15) | toạ độ desktop ảo có thể âm |
| `SetProcessDpiAwarenessContext`, manifest `PerMonitorV2` | Win32 | chưa tra (T15) | phải có trước khi tạo cửa sổ đầu tiên |
| `Clipboard.SetImage` / `SetDataObject` | WPF | chưa tra (T15) | có thể ném `COMException` khi bị khoá |
| `RenderTargetBitmap`, `DrawingVisual`, `FormattedText` | WPF | chưa tra (T15) | DPI của bitmap ra; font tiếng Việt |
| `NotifyIcon` | Windows Forms | chưa tra (T15) | dùng từ WPF, dọn `Dispose` khi thoát |
| `Registry` `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | .NET | chưa tra (T15) | khoá của người dùng hiện tại, không cần quyền cao |
| `Mutex` đặt tên | .NET | chưa tra (T15) | `Local\` cho từng phiên đăng nhập |

## Bằng chứng

Mỗi task đã tick có đúng một dòng ở đây; task phủ `F<n>` ghi tên test `F<n>_…` của từng mã. Verdict là một trong ba:
`pass`, `fail`, `not verifiable`.

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
