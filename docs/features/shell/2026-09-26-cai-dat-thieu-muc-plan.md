# Tập tin cài đặt thiếu mục — plan — 2026-09-26

**Trạng thái:** đã duyệt 2026-09-26 ("duyệt nha" cho đề xuất sửa trước đợt 2; "còn việc gì thì làm đi"; trước đó "cho toàn quyền với bạn, bạn cứ quyết")
**Loại việc:** code

Adapter đọc `settings.json` thành một bản ghi thô (mục thiếu là `null`, không kiểm miền). Luật "thiếu thì mặc định, sai miền
thì hỏng" chuyển về Domain, và `ShellInteractor` gọi nó; khi luật báo hỏng, interactor bảo adapter giữ `.bak`.
Brief: [2026-09-26-cai-dat-thieu-muc.md](2026-09-26-cai-dat-thieu-muc.md) · Luật: [SPEC.md](SPEC.md)

## Context

- **Chạm:**
  - `Domain/Shell`: thêm `StoredSettings` (bản ghi thô) và `SettingsRules` (giới hạn và luật hoàn thiện).
  - `UseCases/Shell`:
    - `SettingsLoadResult` mang `StoredSettings?` thay cho `AppSettings?`;
    - `ISettingsStore` thêm `KeepAsBackup()`;
    - `ShellInteractor.LoadOrDefault` và `Persist` gọi luật.
  - `Infrastructure/Shell/SettingsStore`: tài liệu JSON có mục null được, không kiểm miền.
  - `Presentation/Shell/ViewModels/SettingsViewModel`: kẹp chất lượng JPG theo hằng của Domain.
- **Có sẵn:**
  - `SettingsDefaults.Create`;
  - các test `ShellStartRepairTests` (tập tin không đọc được, phím trơn) và `ShellSpecTests` (F1, F2);
  - test adapter `E2eTests/Adapters/StoresTests.cs`.
- **Dịch vụ dùng chung:**
  - lưu cài đặt: có (`ISettingsStore`), thêm `KeepAsBackup`;
  - thông báo: có (`Shell.SettingsCorrupt`, không chữ mới);
  - log: có (`ILog.Info`);
  - transaction, progress: không cần.
- **Bài học đã nhận áp dụng:** không có. **Bug liên quan:** không có.
- **Môi trường:** phiên cloud không có .NET SDK. Unit chạy trên CI (`windows-latest`). Test adapter (`E2eTests`) chỉ được
  **dựng** trên CI, còn chạy thì trên máy Hùng (T10). Cùng giới hạn như plan thư mục theo domain.

## Rules that apply

- ADR 0001/0003: quyết định ở Domain/UseCases; adapter chỉ dịch JSON ↔ bản ghi và làm IO (`.bak`); kiểu mới ở domain `Shell`
  (Shell là domain điều phối, được nhìn `Domain.Capture` và `Domain.Shared`).
- `CLAUDE.md`: file hỏng thì giữ `.bak`, dùng mặc định, có báo — không đổi; chỉ đổi cái gì là "hỏng".
- SPEC shell: các dòng mới của "Cài đặt được giữ lại", F1, F8.

## Decisions

- **Luật ở Domain, interactor gọi, adapter chỉ đọc thô.**
  - `SettingsRules.Complete(StoredSettings, AppSettings defaults)` trả về một trong hai:
    - cài đặt đầy đủ, kèm danh sách mục đã lấy mặc định;
    - lý do hỏng.
  - Rejected: đưa mặc định vào hàm dựng của adapter rồi để nó gọi luật. Cách đó ít đụng test hơn, nhưng adapter thành chỗ
    áp luật, và `architecture-reviewer` đã ghi "giới hạn cài đặt ở `SettingsStore`" là nợ.
- **Sai kiểu vẫn do adapter bắt.** Lỗi JSON (chữ ở chỗ số, tên enum lạ, tài liệu rỗng hay không phải object) → `Corrupt` và
  `.bak` như cũ. **Ngoài miền do luật bắt:**
  - interactor gọi `KeepAsBackup()` rồi báo `Shell.SettingsCorrupt`;
  - tập tin không bị ghi đè lúc khởi động, như nhánh `Corrupt` hiện nay.
- **Miền:**
  - chất lượng JPG 1–100;
  - độ trễ 0–10 giây (SPEC capture: chọn 0, 3, 5, 10);
  - thư mục lưu không rỗng;
  - phím tắt có tên kiểu chụp đúng và phím không rỗng. `Modifiers` thiếu là `None`, vì PrintScreen một mình là phím hợp lệ.
- **Thiếu một kiểu chụp trong bảng phím, hay thiếu cả bảng → phím mặc định của kiểu đó.**
  - Nếu phím mặc định trùng phím người dùng đã gán cho kiểu khác thì lượt đăng ký thứ hai hỏng. Người dùng thấy thông báo
    F7 như mọi phím không đăng ký được. Không thêm luật mới.
- **Mục thiếu không làm ứng dụng ghi lại tập tin lúc khởi động.** Lần Lưu kế tiếp ở Cài đặt ghi đủ mục.
- **Sau một lần khởi động gặp tập tin bị khoá (`Persist`):**
  - tập tin nay đọc được và luật chấp nhận → vẫn từ chối lưu, như cũ;
  - luật báo hỏng → coi như hỏng, cho lưu.

## Tasks

Nhóm 1 là unit (chạy trên CI). Nhóm 2 là lane `ui`, gồm test adapter trong `E2eTests`: dựng trên CI, chạy trên máy Hùng.

### 1. Luật và interactor

- [ ] T1 [red][unit] `Shell/SettingsRulesTests.cs` (Domain) và ca mới trong `Shell/ShellStartRepairTests.cs`, mỗi test mang tên mã: `F8_AMissingSettingTakesItsDefault_TheOthersAreKept`, `F8_AMissingHotkeyTableGivesTheDefaultKeys`, `F8_AMissingKindTakesItsDefaultKey`, `F8_AMissingSettingStartsWithoutNoticeAndWithoutBackup` (interactor, có một dòng log), `F1_JpgQualityOutsideOneToHundredIsBroken`, `F1_DelayBelowZeroOrAboveTenIsBroken`, `F1_AnEmptySaveFolderIsBroken`, `F1_AHotkeyForAnUnknownKindOrWithoutAKeyIsBroken`, `F1_AnOutOfRangeFileIsKeptAsBakAndStartsOnDefaultsWithTheCorruptNotice` (interactor); `Persist` sau lần khởi động bị khoá với tập tin nay ngoài miền thì cho lưu — xong khi đỏ ở assertion (không có SDK ở đây: CI đỏ, hoặc lý do "không biên dịch vì kiểu chưa có" ghi rõ trong bằng chứng) {files: tests/Paper.ScreenWizzard.UnitTests/Shell/**}
- [ ] T2 [unit] `Domain/Shell/StoredSettings.cs`, `Domain/Shell/SettingsRules.cs`; `SettingsLoadResult.Stored`; `ISettingsStore.KeepAsBackup`; `ShellInteractor`; fake — xong khi CI xanh, số test đã chạy > 345 {files: src/Paper.ScreenWizzard.Domain/Shell/**, src/Paper.ScreenWizzard.UseCases/Shell/**, tests/Paper.ScreenWizzard.UnitTests/Shell/**}

### 2. Adapter và màn hình

- [ ] T3 [red][ui] `E2eTests/Adapters/StoresTests.cs`: `F8_AFileWithoutAJpgQualityLoadsWithThatSettingEmpty`, `F8_AFileWithAnUnknownSettingLoads`, `F1_KeepAsBackupCopiesTheFileOverBak`; ca "bảng phím null" nay là `Loaded` (luật quyết, không phải adapter); các ca đọc lại đi qua `SettingsRules.Complete` — xong khi dựng được trên CI; đỏ và xanh trên máy Hùng ở T10 {files: tests/Paper.ScreenWizzard.E2eTests/Adapters/StoresTests.cs}
- [ ] T4 [ui] `SettingsStore`: tài liệu với mục null được, không kiểm miền, `KeepAsBackup` công khai; `SettingsViewModel.JpgQuality` kẹp theo `SettingsRules` (sau T3) — xong khi CI dựng 0 cảnh báo {files: src/Paper.ScreenWizzard.Infrastructure/Shell/**, src/Paper.ScreenWizzard.Presentation/Shell/**, tests/Paper.ScreenWizzard.E2eTests/Adapters/StoresTests.cs}

### 3. Chạy trên Windows

- [ ] T9 CI: dựng Release 0 cảnh báo, unit xanh với số test > 345 — xong khi đọc được số test trong log
- [ ] T10 Trên máy Hùng: `dotnet test tests/Paper.ScreenWizzard.E2eTests -c Debug` (các ca `Settings_*`, `F1_*`, `F8_*` của `StoresTests`) và `paperflow ui` — xong khi số test đạt bằng lần trước cộng ca mới, không ca nào bị bỏ qua

### Last. Close

- [ ] T11 `find-bug` trên các dòng SPEC đổi — xong khi mọi phát hiện có input đã thành test hoặc vào báo cáo
- [ ] T12 Agent `architecture-reviewer` trên các file đổi — xong khi 0 vi phạm
- [ ] T13 Đóng SPEC.md (gỡ dấu "chờ kiểm"), `CODEMAP.md` của Domain, UseCases, Infrastructure; `check-spec`, `check-code-map` sạch — xong khi hai lệnh exit 0 {files: docs/features/shell/SPEC.md, src/**/CODEMAP.md}

## API đã tra

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|
| `JsonSerializer.Deserialize` với property `int?`, enum `Nullable<T>` và `JsonStringEnumConverter` | .NET 10 | **chưa đọc**: `learn.microsoft.com` bị chặn ở phiên cloud (403) | điều đang giả định: mục thiếu thành `null`; mục có mà sai kiểu (chữ ở chỗ số, tên enum lạ) vẫn ném `JsonException`; mục lạ bị bỏ qua (mặc định `JsonUnmappedMemberHandling.Skip`). Test T3 đo cả ba trên máy Hùng |

## Bằng chứng

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
