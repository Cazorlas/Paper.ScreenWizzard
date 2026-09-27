# Kiểm bản mới ngay từ khay — plan — 2026-09-27

**Trạng thái:** đã duyệt 2026-09-27 (Hùng: "Khi chuột phải này ko có option check updates (là check liền lun) nhỉ", rồi "làm đi" với đề xuất)
**Loại việc:** code

Hùng dùng 0.1.3 và thấy menu khay không có chỗ kiểm bản mới ngay. Plan này thêm dòng "Kiểm bản mới" vào menu khay, nằm luôn ở đó,
ngay trên Thoát. Bấm vào thì ứng dụng hỏi GitHub liền và lần nào cũng trả lời bằng một thông báo của Windows, dù có bản mới hay không.
Đợt này nhỏ nên không có brief riêng: yêu cầu của Hùng ghi ở trên và ở Clarifications của SPEC.
Luật: [SPEC.md](SPEC.md), nhóm "Báo bản mới" và F9 · Đợt trước: [2026-09-26-bao-ban-moi-plan.md](2026-09-26-bao-ban-moi-plan.md)

## Context

- **Chạm:**
  - `IUpdateInteractor` thêm `CheckNowAsync`; `UpdateInteractor` dùng chung phần tìm bản (`FindAsync`) với lần kiểm hằng ngày.
  - Menu khay có thêm dòng `Tray.CheckForUpdates` và lệnh `TrayCheckForUpdatesCommand`.
  - `AppShell.CheckForUpdateNowAsync`; `TrayIcon.ShowNotice` nhận một hành động bấm có thể không có.
  - Bốn chuỗi vi/en mới.
- **Có sẵn:**
  - `IReleaseFeed`, `IBrowser`, `UpdateRules`, `AppVersion`;
  - thông báo của Windows qua `NotifyIcon`.
- **Môi trường:** biên dịch và unit chạy trên CI. `ui` cần máy của Hùng.

## Decisions

- **Kiểm ngay bỏ qua ô "Bản mới".** Chính cú bấm của người dùng là sự cho phép. Ô trong Cài đặt chỉ tắt lần kiểm tự động.
- **Lần nào cũng trả lời.** Người dùng đã hỏi thì phải có câu trả lời, kể cả khi không có mạng (F9 ở nhánh "người dùng bấm").
  Lần kiểm tự động thì vẫn im lặng như cũ.
- **Bấm kiểm mà thấy bản mới thì tính là đã báo bản đó.** Lần kiểm hằng ngày sau đó không báo lại.
- **Câu trả lời là thông báo của Windows, không phải hộp thoại.** Người dùng không phải bấm đóng.

## Tasks

- [x] T1 `CheckNowAsync` và các test trong `UpdateSpecTests`: bản mới, đã là bản mới nhất, không có trả lời, tag hay bản dựng không có số bản, vẫn hỏi khi đã tắt kiểm hằng ngày, không báo trùng với lần kiểm hằng ngày {files: src/Paper.ScreenWizzard.UseCases/Shell/**, tests/Paper.ScreenWizzard.UnitTests/Shell/UpdateSpecTests.cs}
- [x] T2 Dòng khay và chuỗi; test `TrayMenuTests` (chín dòng, dòng mới gọi lệnh) và `LanguageAndThemeTests` {files: src/Paper.ScreenWizzard.Presentation/**, tests/Paper.ScreenWizzard.UiTests/Shell/**}
- [x] T3 `AppShell` và `TrayIcon` {files: src/Paper.ScreenWizzard.App/**}
- [ ] T4 CI xanh; `ui` và thử tay trên máy Hùng (bấm "Kiểm bản mới" trên 0.1.4 thì thấy "Bạn đang dùng bản mới nhất (0.1.4)")
- [x] T5 SPEC hai ngôn ngữ, CODEMAP; `check-spec` và `check-code-map` sạch {files: docs/**, src/**/CODEMAP.md}

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
