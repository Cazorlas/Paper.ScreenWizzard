---
name: task-spec
description: Turn a request into the requirement first - the work type (code or model) decided from the profile and asked in one question when unclear, SPEC.md written or changed in the user's words with measurable acceptance, a frozen brief, and a plan with test-first tasks in lanes, each naming the files it may write, the API lookup table and a status line - then stop for the user's approval. A model plan reads the project's rules, samples and references first. Use when the user types /task-spec, or starts any code or model change that has no approved plan yet.
---

# /task-spec <yêu cầu>

Viết yêu cầu, brief và plan — theo đúng thứ tự đó — rồi kết thúc lượt. Nó không sửa một dòng code nào.

1. **Đọc trước khi viết.** `CLAUDE.md` của kho và của project con, `CODEMAP.md`, `SPEC.md` của
   feature (skill `spec-backfill` nếu feature đã có mà chưa có spec), source và test của nó. Tài liệu
   người dùng đưa (video, ảnh, file dữ liệu) đọc trước và chép nguyên vẹn vào `<featureDocs>/<slug>/input/`.
   Rồi hai sổ cạnh `<featureDocs>` (thư mục cha `<docs>`): các dòng `đã nhận → <file>` của
   `<docs>/retro/README.md` chạm tới việc này — bài học chủ dự án đã nhận là luật phải theo, dòng
   `chờ xét` thì chưa — và bug `open` của feature trong `<docs>/bugs/README.md`.
2. **Đọc `.claude/paper.profile.json`** — `lanes` của nó là danh sách lane duy nhất được dùng,
   `workTypes` là các loại việc dự án khai (mỗi loại kèm lane của nó), và `docsSource` là nơi phải tra API.
   Dự án chưa có profile thì viết nó trước; không có profile thì mọi verb trả 2 và không có gì chạy được.
3. **Chọn loại việc và ghi lại.**
   - `code` — đổi chương trình; `model` — đổi model hay bản vẽ của người dùng (dựng, sửa, học một luật).
   - Yêu cầu không rõ thuộc loại nào, hay profile khai `workTypes` mà không có loại này: hỏi **một** câu,
     lựa chọn là các khoá của `workTypes`, rồi **dừng** (F16). Không đoán. Profile không khai `workTypes`:
     việc là `code`. Yêu cầu nêu cả hai loại: hai plan, mỗi plan một loại.
   - Loại việc ghi thành dòng `**Loại việc:** code` hoặc `**Loại việc:** model` ngay dưới dòng trạng thái
     của plan.
   - **`model`**: trước mọi thứ khác đọc các file khớp glob `workTypes.model.rules`, `samples` và
     `references` chạm tới vùng việc này — luật có mã, mẫu, và cách đọc tài liệu đứng sau luật — rồi đi
     theo skill `model-task` cho SPEC.md, brief và plan. Tài liệu người dùng mới thả mà chưa có mục thì ghi
     mục trước; luật người dùng vừa nêu thì ghi vào file luật trong cùng lượt.
4. **Viết hoặc sửa `<featureDocs>/<slug>/SPEC.md` trước tiên** — nó là yêu cầu, không phải bản tóm tắt
   viết sau khi code chạy. Luật là skill `spec`: lời người dùng, dòng `Cho … → **kết quả đo được**`,
   không tên class/method/file/test, bảng "When it does not do the job" không có dòng nào là "không
   gì" và mỗi dòng mang mã `F<n>` (không bao giờ đánh số lại; một skip im lặng là một dòng `F` riêng,
   không bao giờ bị bỏ). `SPEC.md` mới mở đầu bằng banner `> Bản nháp chờ duyệt <ngày>. …`; mục bị
   đổi của một `SPEC.md` có sẵn mang `> Đổi bởi <brief>, chờ kiểm` ngay dưới heading của nó.
   **Viết cả hai ngôn ngữ trong một file**: phần `# English` và phần `# Tiếng Việt`, cùng mục, cùng dòng
   nghiệm thu, cùng mã `F`, đổi cùng lúc — skill `spec`, mục "Two languages".
5. **Viết brief `<featureDocs>/<slug>/YYYY-MM-DD-<task>.md` từ `spec/brief-template.md`**: ai hỏi gì, vì
   sao, tài liệu đi kèm, mục nào của `SPEC.md` đổi, model và id được đụng tới. Không tên code, không task.
6. **Viết plan `<featureDocs>/<slug>/YYYY-MM-DD-<task>-plan.md` từ `spec/plan-template.md`**: Context,
   Rules that apply, Decisions, wireframe khi có giao diện (plan model: khối "Plan model" của khuôn), rồi
   Tasks. Plan được phép nêu tên code.
   - **Context trả lời câu hỏi dịch vụ dùng chung**: transaction/undo, progress có cancel, thông báo/báo
     cáo, lưu settings, xử lý failure/warning, logging — cái nào việc này cần mà chưa có API thì một task
     riêng thêm nó, xếp trước.
   - **Mỗi dòng `Cho … →` và mỗi dòng `F<n>` mà việc này chạm phải có task tới được nó**, và task nêu
     mã `F<n>` nó phủ — mỗi mã một test tên bắt đầu bằng mã; một task không truy về dòng
     SPEC nào hay dòng Decisions nào là việc không ai yêu cầu.
   - **Task theo thứ tự test-first**, nhóm `### n.`: task đầu của mỗi lane `unit`/`ui`/`e2e` mang dấu
     `[red]`; code sau test; mock UI trước host thật; lane `live` sau khi unit xanh — trừ khi profile khai
     `live.loop`: host tự kiểm được, nên mỗi nhóm có code kiểm được trên host mở bằng một task `[live]` **đo
     baseline** (nhóm trước task `[red]`, xong khi dòng bằng chứng có chữ `baseline`), và `[red]` viết từ số đó
     (skill `task-do`, "Host kiểm được thì đo trước"); nhóm cuối là
     Close — `find-bug`, agent `architecture-reviewer`, rồi đóng `SPEC.md`. Mỗi task nói **xong khi** nào.
   - **Mỗi task sửa file ghi `{files: <glob>, <glob>}` ở cuối dòng** — đó là tất cả những gì lane agent
     của nó được đụng. Các lane trong một nhóm chạy song song: glob của hai lane khác nhau trong một nhóm
     không được giao nhau (F3), không thì task sau ghi `(sau T<n>)` hay tách nhóm. Lane `ui` và `live`
     cần cùng màn hình: đặt ở hai nhóm khác nhau.
   - Plan `model`: lane `model` thôi; mỗi nhóm đủ bốn bước khảo sát, chạy thử không giữ gì, thực hiện, đọc
     lại độc lập; **mỗi task nêu mã luật** nó dựng hay đo theo (F12); `## Luật áp dụng` liệt kê đủ các mã.
   - Lane của task lấy từ `lanes` và từ lane của loại việc. Việc thuộc lane dự án không chạy thì ghi
     `không áp dụng` ngay trong dòng task — im lặng bỏ nó là nợ ẩn.
7. **Tra trước những gì đã biết chắc sẽ gọi** ở `docsSource` và điền bảng **API đã tra** của plan.
   Member phát hiện sau do `/task-do` thêm.
8. **Chỉ hỏi điều làm đổi thứ sẽ được xây** — hai tới năm phương án, nhiều nhất năm câu. Câu còn mở là
   một dòng **Chưa trả lời** dưới Clarifications của `SPEC.md`.
9. Dòng trạng thái của plan để nguyên `**Trạng thái:** chờ duyệt`. Kiểm cả hai:
   - `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` phải exit **4** (2 nghĩa là sai khuôn — lane
     ngoài loại việc, `{files:}` giao nhau trong một nhóm, task model thiếu mã luật — sửa rồi kiểm lại);
   - skill `check-spec` phải sạch trên `SPEC.md` vừa viết.
10. **Kết thúc lượt.** Trình ra ba tài liệu **kèm liên kết mở được** (`SPEC.md`, brief, plan — mỗi cái một
    dòng liên kết markdown, đủ cả ở lần duyệt lại thứ hai, thứ ba; luật đầy đủ ở skill `paperflow`, mục
    "Dừng chờ duyệt"), loại việc, các dòng luật của `SPEC.md` mới hoặc đổi, và danh sách task theo nhóm
    (lane nào chạy song song), ngắn, và nói rõ cần trả lời gì. Không gì khác xảy ra tới khi người dùng duyệt.

Khi người dùng đồng ý, ghi vào dòng trạng thái **của plan**:

`**Trạng thái:** đã duyệt <ngày> ("lời họ nói")`

Rồi đi tiếp với `/task-do <plan>`. Từ đây brief đóng băng. Yêu cầu đổi sau khi duyệt thì sửa `SPEC.md`
(dấu `chờ kiểm`), thêm dòng `Spec bổ sung <ngày>` vào Decisions của plan, đặt trạng thái về `chờ duyệt`
và duyệt lại — không bao giờ sửa brief hay viết lại một task đã tick.
