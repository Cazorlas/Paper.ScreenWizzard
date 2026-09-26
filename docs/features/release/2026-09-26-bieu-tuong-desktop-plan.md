# Biểu tượng trên màn hình nền — plan — 2026-09-26

**Trạng thái:** đã duyệt 2026-09-26 ("tự tạo á", trả lời câu hỏi có tạo biểu tượng không; trước đó "cho toàn quyền với bạn, bạn cứ quyết")
**Loại việc:** code

Thêm vào `installer/Setup.iss`:
- một task Inno `desktopicon`, chọn sẵn, dùng câu chữ `CreateDesktopIcon` / `AdditionalIcons` có sẵn trong `Default.isl` và `Vietnamese.isl`;
- một mục `[Icons]` trên `{autodesktop}` gắn task đó.

`installer/verify-installer.ps1` đo cả ba trường hợp: có biểu tượng khi để ô, không có khi bỏ ô, và biểu tượng mất khi gỡ.
Brief: [2026-09-26-bieu-tuong-desktop.md](2026-09-26-bieu-tuong-desktop.md) · Luật: [SPEC.md](SPEC.md)

## Context

- Chỉ chạm `installer/Setup.iss` và `installer/verify-installer.ps1`. Không đổi mã ứng dụng.
- Có sẵn:
  - mục `[Icons]` cho menu Start (`{autoprograms}`);
  - `PrivilegesRequired=lowest` nên `{autodesktop}` là màn hình nền của người dùng;
  - script kiểm cài thật, có sẵn hàm `Start-Menu-Shortcut`.
- **Dịch vụ dùng chung:** không cần.
- **Bài học đã nhận áp dụng:** không có. **Bug liên quan:** không có.
- **Môi trường:** phiên cloud không chạy được Inno Setup hay PowerShell. Bằng chứng là lượt dựng tay `release.yml` trên runner
  Windows: dựng gói rồi chạy `verify-installer.ps1`.

## Rules that apply

- `CLAUDE.md` mục Deploy:
  - chạy `verify-installer.ps1` sau mỗi lần đổi `installer/`;
  - exit 0 chỉ khi mọi kiểm tra đạt và số kiểm tra lớn hơn 0.
- `Setup.iss` có dấu tiếng Việt thì phải giữ UTF-8 có BOM.
- Trong `Setup.iss`, đường dẫn dùng `/` (bài học ISPP ở handover 2026-09-22).
- Agent không đẩy thẻ và không tạo release; lượt dựng tay chỉ dựng và kiểm.

## Decisions

- **Task chọn sẵn, không bắt buộc.** Hùng muốn "tự tạo", nhưng một ô để bỏ chọn là thông lệ của trình cài Windows và không tốn gì.
  Rejected: luôn tạo, không có ô. Người không muốn biểu tượng sẽ phải xoá tay sau mỗi lần cài bản mới.
- **Câu chữ lấy từ `Default.isl` / `Vietnamese.isl`** (`CreateDesktopIcon`, `AdditionalIcons`), không tự viết thêm câu chữ riêng.
- **Cài đè nhớ lựa chọn cũ** (mặc định `UsePreviousTasks=yes` của Inno):
  - người đã bỏ chọn thì bản sau không tạo lại;
  - người nâng từ 0.1.0 (chưa có task) thì ô chọn sẵn, nên có biểu tượng.

## Tasks

Không có lane `unit` hay `ui`: không có mã ứng dụng. Bằng chứng là `verify-installer.ps1` chạy trên runner.

### 1. File cài

- [ ] T1 `verify-installer.ps1`: hàm `Desktop-Shortcut`; kiểm "What the user does 3 / Cài đè và gỡ: có một biểu tượng trên màn hình nền" sau lần cài tiếng Việt; kiểm "không có biểu tượng khi bỏ chọn" bằng một lần cài `/MERGETASKS="!desktopicon"` vào thư mục thử rồi gỡ; kiểm "gỡ thì biểu tượng mất"; dọn biểu tượng trong khối `finally` — xong khi trên bản cài chưa có task, các kiểm tra mới **đỏ** (lượt dựng tay chạy trước khi đổi `Setup.iss`) {files: installer/verify-installer.ps1}
- [ ] T2 `Setup.iss`: `[Tasks]` `desktopicon` và `[Icons]` `{autodesktop}` — xong khi lượt dựng tay `release.yml` báo mọi kiểm tra đạt, số kiểm tra lớn hơn 35 {files: installer/Setup.iss}

### Last. Close

- [ ] T3 Đóng SPEC.md (gỡ dấu "chờ kiểm"), `check-spec` sạch; `CLAUDE.md` mục Deploy nhắc biểu tượng màn hình nền nếu cần — xong khi `check-spec` exit 0 {files: docs/features/release/SPEC.md}

## API đã tra

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|
| Inno Setup `[Tasks]` (không cờ `unchecked` là chọn sẵn), `[Icons]` `Tasks:`, hằng `{autodesktop}`, `/MERGETASKS` | 6.7.3 | **chưa đọc**: `jrsoftware.org` chưa thử ở phiên cloud; theo tài liệu Inno quen dùng. Đo bằng T1 trên runner | `{autodesktop}` là màn hình nền của người dùng khi cài không quyền quản trị; cài im lặng vẫn chạy task chọn sẵn; `/MERGETASKS="!x"` bỏ chọn task x; biểu tượng do trình cài tạo bị gỡ theo khi gỡ |

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
