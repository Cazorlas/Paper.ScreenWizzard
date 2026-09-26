# <Tên việc> — plan — YYYY-MM-DD

**Trạng thái:** chờ duyệt
**Loại việc:** code

<Hai dòng: việc gì, bằng lời của code.>
Brief: [YYYY-MM-DD-<task>.md](YYYY-MM-DD-<task>.md) · Luật: [SPEC.md](SPEC.md)

<!--
Khuôn này do skill `spec` sở hữu. Cổng đọc nó là verb `tasks`:
    .claude/paperflow/paperflow.ps1 tasks -Path <file plan này>

Cổng chỉ đọc markdown — nó không biết dự án này thuộc host nào — nên chạy được ở mọi kho.

  - dòng **Trạng thái:** — "chờ duyệt" (exit 4) / "đã duyệt <ngày> ("lời họ nói")" / "xong <ngày>"
  - dòng **Loại việc:** ngay dưới nó — "code" (đổi chương trình: lane unit, ui, e2e, live) hoặc "model"
    (đổi model hay bản vẽ của người dùng: lane model, xem khối "Plan model" bên dưới). Loại việc phải là
    một khoá trong `workTypes` của .claude/paper.profile.json; thiếu dòng này thì cổng coi là code. Task
    thuộc lane mà loại việc không khai → exit 2: tách thành plan riêng theo loại của nó
  - task: "- [ ] T<n> [lane] <việc> — xong khi <bằng chứng> {files: <glob>, <glob>}" dưới heading "Tasks";
    các nhóm "### n." nằm trong nó; mã T<n> không trùng; lane phải nằm trong `lanes` của profile
    (`laneAliases` đổi tag cũ sang lane mới)
  - {files: ...} ở CUỐI dòng task: những file task được tạo hay sửa, và là tất cả những gì lane agent của
    task được đụng tới. Task không sửa file nào thì bỏ {files:}
  - chạy song song: các lane trong CÙNG một nhóm "### n." chạy cùng lúc, mỗi lane một lane agent; nhóm sau
    chờ nhóm trước xong. Hai task khác lane trong một nhóm có glob giao nhau → exit 2 (F3), trừ khi task
    sau ghi "(sau T<n>)". Lane `ui` và lane `live` cần cùng một màn hình: đặt chúng ở hai nhóm khác nhau
  - task đầu của mỗi lane `unit`/`ui`/`e2e` mang thêm dấu `[red]`: test viết trước, thấy đỏ ở
    assertion. Lane `live` và `model` được miễn — không ai làm nó đỏ trước khi host chạy
  - task [model] nêu ít nhất một mã luật (C-16, R-3, hay tiền tố trong `workTypes.model.rulePrefixes`);
    thiếu → exit 2 (F12). "C-xx" chỉ là chỗ trống, không tính là mã
  - task không tag lane (find-bug, đóng SPEC.md) hợp lệ, nhưng vẫn phải tick và có bằng chứng
  - bằng chứng: dòng "| T<n> | ... |" dưới heading "Bằng chứng"/"Evidence" cho MỖI task đã tick

Exit: 0 xong · 1 còn task / tick không bằng chứng · 2 sai khuôn (cả F3, F12, lane ngoài loại việc) · 4 chưa duyệt.

Plan viết bằng lời của code. Luật KHÔNG nằm ở đây mà ở SPEC.md — đừng chép lại luật, trỏ tới mục của nó.
Chỉ tick khi bằng chứng đã chạy. Không bao giờ viết lại một task đã tick: thêm task sửa và một dòng
Decisions. SPEC.md hoá ra sai giữa chừng: sửa SPEC.md, thêm dòng "Spec bổ sung <ngày>" vào Decisions,
đặt Trạng thái về "chờ duyệt" và dừng.
-->

<!--
Plan model — khi **Loại việc:** model (skill `model-task`). Giữ Trạng thái, Context, Decisions, API đã tra,
Bằng chứng như trên; thay "UI wireframe" bằng bốn mục:

    ## Luật áp dụng        mỗi mã C-/R- việc này phải thoả, một dòng nhắc, không chép nội dung luật
    ## Mẫu dùng            mẫu (SAMPLE) được làm theo, hoặc vì sao không mẫu nào hợp
    ## Hiện trạng đo được  bảng số đo từ khảo sát chỉ đọc
    ## Phương án dựng      cách dựng, cái gì bị xoá, cái gì giữ; ví dụ nhỏ bằng số

Mỗi nhóm đi đủ bốn bước, mỗi bước một task lane model nêu mã luật nó đo hay dựng theo. Các task cùng lane
nên một nhóm chạy nối tiếp trong một lane agent; script ở lại harness/ của feature:

    ### 1. <một tuyến / một khu mẫu>
    - [ ] T1 [model] Khảo sát chỉ đọc <cái cần đo> theo C-xx — xong khi bảng Hiện trạng đủ số đo {files: <featureDocs>/<slug>/harness/**}
    - [ ] T2 [model] Chạy thử không giữ gì trên mẫu theo R-xx — xong khi log "committed: false" và kiểm trong script đạt {files: <featureDocs>/<slug>/harness/**}
    - [ ] T3 [model] Thực hiện mẫu theo R-xx, C-xx — xong khi "committed: true" và có id mới {files: <featureDocs>/<slug>/harness/**}
    - [ ] T4 [model] Đọc lại độc lập bằng script chỉ đọc khác: C-xx, R-xx — xong khi mỗi mã có giá trị đo {files: <featureDocs>/<slug>/harness/**}

    ### 2. <phần còn lại> — lại đủ bốn bước

    ### Last. Close
    - [ ] T9 Chạy script kiểm cho mọi mã trong "Luật áp dụng", điền "Kiểm tra luật"; RULE/SAMPLE nếu có luật hay cách dựng mới — xong khi mỗi mã một dòng
    - [ ] T10 Script vào harness/ (README ghi mã luật), dọn view/lớp kiểm tạm, đóng SPEC.md, báo cáo — xong khi không còn thứ tạm nào chưa nêu tên

Thêm "## Kiểm tra luật" sau "Bằng chứng": | Mã | Script / cách đo | Giá trị đo | Kết quả | — một mã một dòng.
-->

## Context

- <project/thư mục bị chạm, và tầng của nó>
- <đã có sẵn để dựa vào: file, helper, khuôn mẫu — tìm trong thư viện của kho trước>
- **Dịch vụ dùng chung:** các dịch vụ mà thay đổi này cần — transaction/undo, progress có cancel,
  thông báo/báo cáo, lưu settings, xử lý failure/warning, logging — đã có sẵn API nó cần chưa? <mỗi dịch
  vụ một dòng: có (tên API) / không cần / **thiếu** → một task riêng thêm nó, xếp **trước** task dùng nó>
- **Bài học đã nhận áp dụng:** <dòng `đã nhận → <file>` trong `<docs>/retro/README.md` chạm tới việc này,
  hoặc "không có">
- **Bug liên quan:** <mã trong `<docs>/bugs/README.md` mà plan này sửa hay chạm, hoặc "không có">

## Rules that apply

- <luật trong CLAUDE.md của kho / của project, một dòng mỗi luật, và thiết kế này tuân thủ nó thế nào>

## Decisions

- **<lựa chọn>** — vì sao. Rejected: <phương án khác>, vì sao không.

## UI wireframe

<Chỉ khi có giao diện: wireframe dạng chữ trong khối code. Wireframe sống ở đây, không bao giờ trong SPEC.md.>

<Dưới wireframe, mỗi quyết định bố cục một dòng: 2-4 nguyên lý liên quan (Fitts, Hick, Gestalt, heuristic
Nielsen…), chỗ chúng kéo ngược nhau, và lựa chọn — ví dụ "ít cột (tải nhận thức) vs thấy đủ để so (nhận ra
hơn nhớ) → 4 cột, phần còn lại ở panel chi tiết".>

## Tasks

Profile khai `live.loop` (host tự kiểm được code): nhóm có code kiểm được trên host mở bằng một task
`[live]` đo — "Đo <hành vi> trên host đang mở (`<live.loop> ensure`, rồi `measure`) — xong khi có dòng
bằng chứng chứa chữ baseline với case id và số đo" — đặt ở nhóm TRƯỚC task `[red]` của nó, và `[red]` viết
từ số đó. Không kiểm được trên host thì dòng bằng chứng ghi "baseline: not checkable - <vì sao>". Hook
`live-first-guard` nhắc khi code bị sửa trước dòng đó.
Thứ tự: đo baseline trên host trước test đỏ khi profile khai `live.loop`; test đỏ trước code trong mỗi lane;
mock UI trước host thật; lane `live` kiểm lại sau khi unit xanh;
`find-bug`, review kiến trúc và đóng SPEC.md cuối. Mỗi dòng `Cho … →` và mỗi dòng `F<n>` của bảng
"When it does not do the job" mà việc này chạm phải có task tới được nó, và task nêu mã `F<n>` nó phủ —
mỗi `F<n>` là **một** test, tên test bắt đầu bằng mã (`F3_…`).

Lane trong một nhóm chạy song song, mỗi lane một lane agent, và chỉ sửa file trong `{files:}` của task
mình; nhóm sau chờ nhóm trước. Lane `ui` và `live` không bao giờ chung một nhóm.

### 1. <một luồng hay một nhóm luật người dùng kiểm được riêng> — logic và giao diện song song

- [ ] T1 [red][unit] Test cho SPEC "<mục>": <các dòng Cho … →> và F<n>, F<m> (mỗi mã một test `F<n>_…`) — xong khi đỏ ở **assertion**, không phải đỏ vì build {files: <tests>/<Feature>/**}
- [ ] T2 [unit] Code tới khi T1 xanh; verb `test` — xong khi exit 0 và số test đã chạy > 0 {files: <src>/Domain/<Domain>/**, <tests>/<Feature>/**}
- [ ] T3 [red][ui] Mock UI trên dữ liệu giả theo wireframe — xong khi test đỏ ở assertion {files: <src>/Presentation/<Domain>/**, <tests>/<Feature>.Ui/**}
- [ ] T4 [ui] Sửa tới khi T3 xanh; verb `ui`, mở ảnh ra xem — xong khi ảnh khớp wireframe {files: <src>/Presentation/<Domain>/**, <tests>/<Feature>.Ui/**}

### 2. <chạy thật trên host đang mở> — sau nhóm 1, không cùng lúc với lane ui

- [ ] T5 [live] Tra mọi API member sẽ gọi ở `docsSource`; ghi vào bảng dưới — xong khi mỗi member có một dòng
- [ ] T6 [live] Verb `publish` → chạy trên dữ liệu thật qua host đang mở → **đọc lại** → lặp tới khi đạt; dọn thứ đã tạo — xong khi giá trị đọc lại bằng dòng SPEC {files: <src>/Infrastructure/<Domain>/**}

### Last. Close

- [ ] T7 `find-bug` trên SPEC.md — xong khi mọi phát hiện có input đã thành test hoặc vào báo cáo
- [ ] T8 Agent `architecture-reviewer` trên các file plan này đổi — xong khi 0 vi phạm (một vi phạm là fail: về `/task-do`)
- [ ] T9 Đóng SPEC.md: gỡ banner "Bản nháp chờ duyệt" / dấu "chờ kiểm", xoá dòng đã bị thay; `check-spec` sạch; `CODEMAP.md` + `check-code-map` sạch — xong khi hai lệnh exit 0

## API đã tra

Nguồn tra khai ở `docsSource` trong `.claude/paper.profile.json`. Mỗi member được gọi là một dòng,
ghi **trước** dòng code gọi nó.

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|

## Bằng chứng

Mỗi task đã tick có đúng một dòng ở đây; task phủ `F<n>` ghi tên test `F<n>_…` của từng mã. Verdict là một trong ba: `pass`, `fail`,
`not verifiable` (host tắt, runner chạy 0 test, verb trả 4 hoặc 5 — chạy lại **một** lần, lần hai
ghi `môi trường: <lý do>` và **không** sửa code vì nó).

| Task | Lệnh / id / giá trị đọc lại | Verdict |
|---|---|---|
