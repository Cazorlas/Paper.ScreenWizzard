# Paper.ScreenWizzard — bàn giao sang Orca — 2026-09-22

Viết cho: phiên làm việc kế tiếp (agent trong Orca, hoặc Hùng), chưa có ngữ cảnh nào của phiên này.

Repo `D:\Repository\Cazorlas\Paper.ScreenWizzard`, nhánh `main`, commit cuối `6a7ad2c`, đã đẩy hết lên
`github.com/Cazorlas/Paper.ScreenWizzard` (kho công khai). Cây làm việc sạch. CI xanh. Bộ kit paper-kit ở bản **1.24.0**.

## 1. Đọc theo thứ tự này

1. [AGENTS.md](../../AGENTS.md), rồi [CLAUDE.md](../../CLAUDE.md) (luật của dự án, bảng tầng, mục Deploy).
2. [CODEMAP.md](../../CODEMAP.md) và `CODEMAP.md` của từng project trong `src/`.
3. [docs/roadmap.md](../roadmap.md): đợt 1 xong, đợt 2 (quay màn hình) và 3 (ghi chú lên màn hình) chưa có SPEC.
4. Việc đã làm và bằng chứng: [plan đợt 1](../features/shell/2026-09-20-m1-chup-va-sua-anh-plan.md) (35 task) và
   [plan file cài](../features/release/2026-09-21-file-cai-plan.md) (11 task). Mục **Decisions** của plan đợt 1 có "Sổ T20" và
   "Nợ kiến trúc sau T21": đó là danh sách việc còn treo.
5. Luật từng tính năng: [khung](../features/shell/SPEC.md), [chụp](../features/capture/SPEC.md),
   [sửa ảnh](../features/editor/SPEC.md), [file cài](../features/release/SPEC.md).
6. Kiến trúc: [ADR 0001](../decisions/0001-clean-architecture.md), [ADR 0002](../decisions/0002-file-cai-setup-exe-inno.md).

## 2. Trạng thái

| Thứ | Tình trạng |
| --- | --- |
| Đợt 1: khung, chụp bốn kiểu, sửa ảnh | **xong**, 35 task có bằng chứng, `paperflow tasks` exit 0 |
| Đợt sửa lỗi sau `find-bug` và `architecture-reviewer` | xong (T20, T21 và các task T31 tới T35), mọi phát hiện xác nhận đều có test |
| File cài `Setup.exe` (Inno Setup, tiếng Việt và tiếng Anh), zip di động, `SHA256SUMS.txt` | **xong**, 11 task |
| Bản phát hành **`v0.1.0`** | **đã xuất bản** trên GitHub Releases (3 file) |
| Sau `v0.1.0` chưa vào bản nào | `Setup.iss` tự đủ và `Build-Installer.cmd`; sửa "xoá mục khởi động cùng Windows ở đầu lúc gỡ"; bộ kit 1.24.0 |
| Đợt 2 (quay màn hình), đợt 3 (ghi chú) | **chưa bắt đầu**, chưa có SPEC |

Kết quả test lần chạy đầy đủ cuối (Debug): `build` 0 cảnh báo 0 lỗi; unit **329/329**; ui **441/441**; e2e **112/112**.

## 3. Cách chạy

Các lệnh (từ thư mục gốc repo). PowerShell 5.1 hoặc `pwsh` đều được.

| Việc | Lệnh |
| --- | --- |
| Dựng | `.claude/paperflow/paperflow.ps1 build` (hoặc `dotnet build Paper.ScreenWizzard.slnx -c Debug`) |
| Unit | `.claude/paperflow/paperflow.ps1 test` |
| UI (cửa sổ thật, FlaUI, mất khoảng 3 phút) | `.claude/paperflow/paperflow.ps1 ui` |
| E2E (adapter thật và **chính file exe**, khoảng 3 phút) | `dotnet test tests/Paper.ScreenWizzard.E2eTests -c Debug --nologo -v minimal` (không có verb `e2e`, kit không nhận lane đó cho host desktop) |
| Cổng của plan | `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` (0 là xong, 1 còn task mở, 4 chưa duyệt) |
| Kiểm SPEC và bản đồ code | `python .claude/skills/check-spec/check_spec.py`, `python .claude/skills/check-code-map/check_code_map.py` |
| Dựng file cài | bấm đúp `installer/Build-Installer.cmd [x.y.z]`, hoặc mở `installer/Setup.iss` bằng Inno Setup 6.7 hay 7.1 rồi Compile |
| Kiểm file cài bằng cách cài thật | `powershell -File installer/verify-installer.ps1 -Setup artifacts/Paper.ScreenWizzard-<x.y.z>-win-x64-Setup.exe` (36 kiểm tra) |

Chú ý khi chạy:
- **ui và e2e điều khiển chuột, bàn phím và màn hình thật**: đừng chạm máy trong lúc chạy, và đóng bản Paper.ScreenWizzard đang
  chạy. Bộ e2e và script kiểm cài tự từ chối nếu thấy một bản của app không do chúng khởi động.
- Phím tắt thử là `Ctrl+Alt+Shift+F13` tới `F16`. **Không bao giờ gửi PrintScreen thật** trong test (mở Snipping Tool, chụp màn hình).
- Test dùng biến môi trường `PAPER_SCREENWIZZARD_DATA` và `PAPER_SCREENWIZZARD_INSTANCE` để không đụng cài đặt thật của Hùng.
- CI (`.github/workflows/ci.yml`) chỉ dựng Release và chạy unit; ui và e2e không lên CI vì cần desktop tương tác.
- Đọc **số test đã chạy**, không chỉ exit code (0 test đã chạy không phải đạt).

## 4. Luật đang có hiệu lực (Hùng đã nói, đừng đổi)

- Commit của agent ghi tác giả `Cazorla98 <331158021+Cazorla98@users.noreply.github.com>` bằng `--author`, **không** đổi `user.name`
  hay `user.email`.
- **Agent không đẩy thẻ, không tạo release, không bấm Publish** trừ khi Hùng nói trong chính phiên đó (2026-09-21 Hùng đã nói một lần
  và `v0.1.0` đã ra; bản sau cần Hùng nói lại).
- **ADR 0001 và ADR 0002 giữ `Proposed`**: chỉ Hùng được chuyển sang `Accepted`; duyệt một plan không phải duyệt ADR.
- Mỗi lần dừng chờ Hùng duyệt phải kèm **liên kết mở được** tới từng tài liệu (luật 9 của paperflow).
- Quyền đầy đủ chỉ trên các repo `D:\Repository\Cazorlas\*`; repo khác chỉ đọc.
- SPEC viết bằng lời người dùng: không tên lớp, phương thức, test trong `SPEC.md` (`check-spec` bắt lỗi này).
- Ranh giới tầng (ADR 0001, phương án A): `Domain` và `UseCases` không dùng `System.Windows`, `System.Drawing`, `Win32`;
  `Presentation` không tham chiếu `Infrastructure`. `tests/.../UnitTests/Architecture/LayerTests.cs` canh điều này.

## 5. Việc còn lại

### 5.1 Chờ Hùng quyết (agent không tự quyết)

1. **Duyệt hay sửa ADR 0001 và ADR 0002.**
2. **Chỗ hở của SPEC**, đã ghi ở "Sổ T20" trong plan đợt 1 và ở mục "What it does not do yet" của từng SPEC: Esc lúc đếm ngược (cửa sổ
   đếm không lấy bàn phím, chỉ bấm vào số mới huỷ chắc chắn); giao ảnh thất bại khi không có hộp thoại thì ảnh mất (đề xuất mở
   hộp "Đã chụp"); PNG vùng tự do giữ màu gốc dưới điểm trong suốt (đề xuất đặt RGB bằng 0); phím đang do chính app giữ không ghi được
   vào ô phím của Cài đặt (đề xuất nhả phím lúc Cài đặt mở); hộp chữ là ước lượng 0,6 × cỡ chữ.
3. **Ký số** file cài để hết cảnh báo SmartScreen (cần chứng chỉ; các đường không tốn hoặc rẻ: SignPath Foundation cho mã nguồn mở,
   Certum cho mã nguồn mở, Azure Trusted Signing; điều kiện từng nơi chưa kiểm).
4. **Có phát hành `v0.1.1`** để mang các thay đổi sau `v0.1.0` vào một bản không (xem mục 2), và tự cập nhật trong app.

### 5.2 Việc kỹ thuật agent làm được, chưa làm

- **Nợ kiến trúc (NOTE của `architecture-reviewer`)**, chi tiết ở "Nợ kiến trúc sau T21" trong plan đợt 1: `IMonitorCatalogPort` khai ở
  thư mục Shell mà Capture dùng; `IImageCodecPort.Encode` ném ngoại lệ trong khi `Decode` trả kết quả; vài câu tiếng Anh nằm trong
  UseCases; giới hạn hợp lệ của cài đặt nằm ở `SettingsStore` chứ không ở Domain; `AppShell` còn vài quyết định; `EditorViewModel` giữ
  giới hạn 1 tới 20 và 6 tới 200; `CaptureFlow` và `SelectionOverlayViewModel` giữ vài luật; `EditorWindow` tự đặt vị trí. Không cái nào làm
  sai kết quả thấy được.
- **Tra trang docs còn nợ** cho các API dùng ở đợt sửa: `SystemEvents.DisplaySettingsChanged`, `UIElement.LostMouseCapture`,
  `Keyboard.PreviewKeyUpEvent` và hàm dựng `KeyEventArgs`, `Key.Snapshot`, `GetClassNameW`, `Icon.ExtractIcon`; và Inno Setup (`SuppressibleMsgBox`,
  ISPP `Exec`). Hành vi đã có test chạy thật, thiếu ghi trang tham chiếu vào bảng API của plan.
- **Trường thiếu trong `settings.json` vẫn bị coi là file hỏng** (sao lưu `.bak`, dùng mặc định). Phải đổi thành "thiếu thì lấy giá trị
  mặc định, chỉ sai kiểu hay ngoài miền mới là hỏng" **trước khi thêm bất kỳ cài đặt nào ở đợt 2**, kẻo file của Hùng bị coi là hỏng.
- **`ClipboardService`** chặn luồng giao diện tối đa vài giây khi clipboard bị chương trình khác giữ.
- **F1 của file cài** (Windows cũ hơn 1903) chỉ được kiểm bằng cách đọc thiết lập, chưa chạy thật; chế độ tự chọn tiếng Việt cũng chưa
  thử (máy Hùng dùng `en-US`; chọn bằng tay `/LANG=vi` đã chạy).

### 5.3 Đợt 2: quay màn hình (chưa có SPEC)

Yêu cầu Hùng đã nói nằm ở [docs/roadmap.md](../roadmap.md) (làm như FastStone Screen Recorder: chọn màn hình, vùng, cửa sổ hoặc toàn bộ
desktop; bật camera với hình dạng khung chỉnh được; tiếng hệ thống và micro bật tắt độc lập; con trỏ, phím tắt, đếm ngược; ra MP4) cùng
gợi ý kỹ thuật (Media Foundation cho camera, WASAPI loopback cho tiếng hệ thống). Quy trình: `/task-spec` viết SPEC, brief, plan, **rồi
dừng chờ Hùng duyệt** trước khi viết mã.

## 6. Những thứ mình đã đo và dễ dẫm lại

- **Windows Installer (MSI) đã bị bỏ.** WiX không có giao diện tiếng Việt; MSI ghi mục Ứng dụng ở nhánh máy dù cài theo người dùng, và
  thuộc tính `WindowsBuild` của nó báo 9600 trên Windows 11. Đừng quay lại MSI nếu không có lý do mới.
- **Bộ tiền xử lý của Inno (ISPP) đọc `\a`, `\s` trong chuỗi thành ký tự thoát**: trong `Setup.iss` dùng dấu `/` cho đường dẫn.
- **Chuỗi trong `.iss` có dấu tiếng Việt cần UTF-8 có BOM** (file hiện có BOM; giữ nguyên khi sửa).
- **`MsgBox` trong Inno không bị `/SUPPRESSMSGBOXES` chặn**; dùng `SuppressibleMsgBox`, kẻo Setup im lặng chờ một cú bấm không ai bấm
  (treo trên runner GitHub).
- `Start-Process -Wait` trong `pwsh` chờ cả tiến trình con; script kiểm dùng `WaitForExit` có giới hạn thời gian.
- Trong file Python hay heredoc, `\n` `\r` `\a` trong chuỗi đường dẫn bị đọc thành ký tự thật (đã dính ba lần): viết file bằng công cụ ghi
  file, hoặc chuỗi thô.
- Hai phím **Alt+PrintScreen và Ctrl+PrintScreen đang bị chương trình khác giữ trên máy Hùng**: app báo bằng một thông báo nhỏ lúc khởi động
  (không phải hộp lỗi, xem F7 của SPEC khung). Đừng "sửa" bằng cách ép đăng ký.
- Thư mục `__pycache__` của kit từng bị chép vào dự án và làm `vendor.ps1` từ chối cập nhật: đã thêm `__pycache__/` vào `.gitignore`;
  chạy các script `check_*.py` với `PYTHONDONTWRITEBYTECODE=1` cho chắc.
- Các hook ở `.claude/hooks` và `.codex/hooks.json` thuộc kit: **không sửa tại đây**, sửa ở repo Paper-skills rồi vendor lại
  (`D:\Repository\Cazorlas\Paper-skills\scripts\sync-consumers.ps1`).

## 7. Cái còn bỏ lại trên máy

- `.tools/inno` (bộ biên dịch Inno Setup 6.7.3 của dự án, bị git bỏ qua) và `artifacts/` (file cài dựng thử, bị git bỏ qua). Xoá được, script
  tự tạo lại. Inno Setup 7.1.0 của Hùng ở `D:\Program Files\Inno Setup 7` không phải của dự án, đừng đụng.
- Không còn bản Paper.ScreenWizzard nào được cài hay đang chạy do mình, không còn mục Start, mục Ứng dụng hay giá trị Run nào do script
  kiểm để lại (script tự trả máy về như cũ; lần kiểm gần nhất sạch).
- Ngoài repo này: bộ kit đã cập nhật lên 1.24.0 ở cả RevitAPI-C, Paper.AutoCad, Paper.ScreenWizzard. Paper-skills còn một lỗi nhỏ chưa sửa:
  `paper-kit/scripts/vendor.ps1` vẫn chép thư mục `__pycache__` nếu gặp (sửa cần nâng kit lên 1.24.1).

## 8. Bước đầu tiên nên làm

1. `git pull`, đọc mục 1, chạy `paperflow build` và `paperflow test` để chắc máy mới chạy được (cần .NET 10 SDK, Windows 10 1903 trở lên).
2. Hỏi Hùng đợt kế là gì: **phát hành `v0.1.1`**, **xử lý các chỗ hở của SPEC (mục 5.1)**, hay **bắt đầu SPEC cho đợt 2**.
3. Việc nào cũng đi theo vòng đời của kit: `/task-spec`, dừng chờ duyệt kèm liên kết, `/task-do` (test đỏ trước), `/task-verify`.
