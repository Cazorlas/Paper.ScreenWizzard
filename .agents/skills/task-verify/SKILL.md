---
name: task-verify
description: Verify a plan end to end - the evidence each lane agent returned read lane by lane (a run that counted zero tests is not verifiable, never pass), the full test suite against the known-red baseline, one review-files list of the files to review (a reviewer that did not read all N of N fails), a find-bug pass against SPEC.md, the architecture-reviewer agent on that list, the rule-check table of a model plan, closing SPEC.md (draft banner and change markers out, replaced lines deleted), the bug ledger and lesson queue, the plan gate - and report every SPEC rule and failure mode (F1, F2 ...) the task touched as pass, fail or not verifiable with evidence. Use when the user types /task-verify, and at the end of /task-do.
---

# /task-verify <plan>

Xanh ở đây nghĩa là **đã đo**, không phải đã tin — kể cả điều một lane agent vừa báo.

## Harness evidence

Read the matching `.paper/harness/runs/*/summary.json` and `events.jsonl` after the lane evidence. Use the
run id and event paths to correlate session/tool/task timing and recovery, but never treat a harness summary
as proof by itself: the plan's evidence rows, independent verification, and `paperflow` gate still decide.
Use `summary.json.quality` as a compact diagnostic: compare event/tool/failure/permission counts and changed
files against the lane evidence. If the summary is stale or missing, run `harness.ps1 summarize` after
recovering the final JSONL line; keep the run incomplete until independent verification exists.
If a run has no `summary.json`, use `recover` to salvage any partial final JSONL line, then report the
run as incomplete until the plan's independent verification evidence is available.

0. **Bằng chứng theo lane.** Đọc bảng `## Bằng chứng` của plan theo từng lane (`unit`, `ui`, `e2e`,
   `live`, `model`) — mỗi lane agent trả dòng của lane nó, và từng dòng được đọc lại ở đây:
   - dòng lane `unit`/`ui`/`e2e` mà **số test đã chạy là 0**, hay không ghi số test nào → `not verifiable`,
     không phải `pass`, dù exit 0 và dù task đã tick. Chạy lại đúng lần đó một lần; vẫn 0 thì ghi
     `môi trường: <lý do>` và báo dòng luật của nó là `not verifiable`;
   - dòng lane `live` hay `model` lấy số từ giá trị trả về của chính lệnh vừa sửa, không phải từ một lần
     đọc lại riêng → `not verifiable`; task đọc lại thêm vào plan, trả về `/task-do`;
   - file một lane agent báo đã sửa nằm ngoài `{files:}` của task nó → **fail** của task đó;
   - lane mà profile không khai → `không áp dụng`, không phải lỗ hổng.

1. **Suite.**

   ```powershell
   .claude/paperflow/paperflow.ps1 test
   ```

   `/task-do` vừa chạy bản đầy đủ và **không file nào đổi từ lần đó** (so `git status --short` và
   `git diff --stat` với lúc nó chạy): dòng số test của nó là bằng chứng, không chạy lại. File đã đổi
   (một test hồi quy, một sửa sau review): chạy lại. Plan model không đổi code: `không áp dụng`.
   Pass cần exit 0 **và** số test đã chạy **> 0**. Exit 0 với 0 test là `not verifiable`, không phải
   pass. Một fail không nằm trong `knownFailures` của profile là **fail**, bất kể ai gây ra. Nếu thay
   đổi đụng tới một cấu hình build riêng, build cả cấu hình đó (verb `build`).

2. **Danh sách file phải xem**, lập **một lần** trước mọi agent của bước 2 và 3:

   ```powershell
   .claude/paperflow/paperflow.ps1 review-files
   ```

   Dòng đầu nói nguồn (`source ocr <ver>`, `git`, hay `git (ocr failed: …)` — `ocr` hỏng không bao giờ bỏ
   bước này) — chép vào báo cáo. Đầu ra nguyên văn (danh sách, khối `rules:` theo nhóm nếu có, `excluded:`
   kèm lý do, `total: N`) là phạm vi của `find-bug`, `architecture-reviewer` và từng reviewer của profile —
   không agent nào tự lập danh sách khác. Mỗi agent trả `đã xem N/N` và tên file chưa xem; thiếu dù một file
   là **fail** (F20): giao lại đúng các file thiếu cho agent đó, không đóng `SPEC.md`. Exit **2** = không kiểm
   được (không phải repo git, `-Base` sai, không tìm ra nhánh gốc, không có merge-base — bản clone nông, hay
   một lỗi bất ngờ mà lệnh in ra): verdict review
   `not verifiable` kèm dòng lý do lệnh in ra; reviewer vẫn chạy trên `git status` cộng các file trong bảng
   bằng chứng của plan, và báo cáo nói rõ danh sách đó **không do máy lập**. Exit **5** (không thay đổi nào)
   → phần "đã xem N/N" của bước 2 và 3 `không áp dụng`; `find-bug` vẫn đối chiếu `SPEC.md`.

   **Thử làm nó sai** bằng skill `find-bug` trên đúng danh sách đó, đối chiếu với `SPEC.md` của feature —
   trước hết các mục mang banner nháp hay dấu `chờ kiểm` mà việc này đặt vào: case đối xứng, chỗ gọi thứ
   hai, return sớm. Một nghi ngờ không có input thì đưa vào báo cáo, không đuổi theo.

   **Mỗi phát hiện có input cụ thể phải qua một agent đọc lại độc lập trước khi thành việc sửa** (mục
   "Verifying a finding before it becomes a task" của `find-bug/SKILL.md`) — dispatch song song, một
   agent riêng cho mỗi phát hiện, agent đó không biết ai tìm ra phát hiện và chỉ nhận dòng luật (nguyên
   văn) cùng input, không nhận đường dẫn code, lời giải thích hay kết luận của người tìm ra nó. Verdict:
   - **xác nhận** (`confirmed`) → thêm test hồi quy viết từ chính input đó, trả việc về `/task-do`;
   - **bác bỏ** (`rejected`) → vào báo cáo kèm lý do, không vào plan;
   - **chưa xác minh được** (`needs_validation`) → vào báo cáo, nêu đúng thứ còn thiếu, không chặn các
     phát hiện khác đang chờ xác minh song song, không tự coi là xong.
   Hai phát hiện cùng một chỗ hỏng thật (fingerprint agent đọc lại nêu ra) dù input khác nhau → gộp một
   task sửa. Một phát hiện có input mà chưa qua bước này khi đóng `SPEC.md` là **fail** (F21): quay lại
   bước xác minh, không đóng.

3. **Kiến trúc** (plan code; plan model chỉ đổi script harness thì `không áp dụng`). Chạy agent
   `architecture-reviewer` với **nguyên đầu ra `review-files` của bước 2** và đường dẫn plan; rồi từng
   agent mà profile khai ở `architecture.reviewers` (reviewer riêng của dự án: tên tầng, quyết định của
   nó), cùng đầu ra đó — song song được, vì cả hai chỉ đọc. Một vi phạm bất kỳ reviewer nào báo là
   **fail**: thêm task sửa vào plan và trả việc về `/task-do` — không đóng `SPEC.md`, không qua cổng.
   Sạch và mọi reviewer báo đủ N/N thì tick task review trong nhóm Close kèm dòng bằng chứng
   "0 vi phạm, đã xem N/N".

   **Tra API** (plan code), cùng lúc với các reviewer:

   ```powershell
   .claude/paperflow/paperflow.ps1 api-check -Path <plan>
   ```

   Exit **1** = một file code mà nhánh có thêm dòng (so với nhánh gốc, cả phần chưa commit) dùng host —
   qua `using` trong chính file, hay qua `global using` / `<Using>` của project chứa nó — mà bảng
   `## API đã tra` của plan trống (F17): **fail** như một vi phạm kiến trúc; thêm task tra API (skill
   `api-lookup`) vào plan, trả việc về `/task-do`. Exit **2** = không kiểm được (không tìm thấy plan,
   không phải repo git, `-Base` sai, không tìm ra nhánh gốc, không có merge-base — clone nông): ghi verdict `not verifiable` kèm lý do lệnh in ra. Exit **5** = `không áp dụng`
   (không file nào dùng host, hoặc profile không khai `api.namespaces` — lệnh nói cách khai). Danh sách
   `not in the API table` mà lệnh in ra, kể cả khi exit 0, chép vào báo cáo để người kiểm xem từng dòng —
   đó là đoán từ chữ trong code, không chặn.

   **Plan model: kiểm luật.** Mỗi mã trong `## Luật áp dụng` có đúng một dòng trong `## Kiểm tra luật`,
   số đo lấy từ script kiểm chỉ đọc chạy trên model sau lần thực hiện cuối (skill `model-task`). Một mã
   `không đạt` là fail: task sửa, về `/task-do`. Mã `không đo được` phải có lý do.

4. **Đóng `SPEC.md`** (skill `spec`), chỉ khi bước 0, 1, 2 và 3 sạch:
   - gỡ banner `Bản nháp chờ duyệt` và mọi dấu `chờ kiểm` mà việc này đặt vào;
   - xoá dòng đã bị một dòng vừa kiểm thay thế, và dòng mà việc làm chứng minh là sai;
   - cập nhật `Inputs`, `When it does not do the job` và `What it does not do yet` nếu chúng đã dịch —
     mã `F<n>` không bao giờ đánh số lại, dòng bỏ đi để lại khoảng trống;
   - skill `check-spec` sạch.

   Dòng luật chưa đo được thì **không** đóng: để nguyên dấu `chờ kiểm` và nói ra trong báo cáo.

5. **Code map.** `CODEMAP.md` của mọi thư mục đã sửa là hiện hành; `check-code-map` sạch.

6. **Sổ bug.** Plan này sửa một bug (mã trong Context của plan hay trong brief) và mọi dòng luật, mọi
   `F<n>` của nó `pass` → đổi Status trong `<docs>/bugs/<mã>-<slug>.md` thành `fixed` kèm ngày và link
   plan, cập nhật dòng của nó trong `<docs>/bugs/README.md`, gỡ link bug khỏi `SPEC.md`. **Không bao giờ
   xoá file bug.** Còn một dòng chưa `pass` thì Status giữ nguyên.

7. **Bài học.** Những gì việc này dạy mà lần sau đáng biết — một bẫy, một con số đo được, một luật bị
   phạm, một dịch vụ dùng chung thiếu API — ghi thành dòng `chờ xét` trong `<docs>/retro/README.md`
   (chưa có thì tạo từ `task-verify/retro-readme-template.md`): ngày, phát hiện, nhà đề xuất
   (`CLAUDE.md`, dòng SPEC, comment ở code, skill), status. **Không** tự ghi luật vào `CLAUDE.md`, skill
   hay `SPEC.md` — chủ dự án xét hàng đợi ở `/sync-docs`. Ngoại lệ duy nhất: người dùng đã yêu cầu
   **chính luật đó** trong phiên này. Đọc hàng đợi trước khi ghi để không thêm lại dòng đã có.

8. **Cổng.** `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` phải exit **0**; rồi đặt dòng trạng
   thái của plan thành `xong <ngày>`. Brief không bị sửa.

9. **Báo cáo** (skill `verification-before-completion`), bằng ngôn ngữ của người dùng, ngắn — một dòng
   cho mỗi dòng luật và **mỗi mã `F<n>`** của `SPEC.md` mà việc này chạm:

| Luật (SPEC.md) | Lane | Bằng chứng | Verdict |
| --- | --- | --- | --- |
| Cho ống 900 x 400 → rộng 1200 | unit | fixture, số đỏ rồi xanh | pass |
| F3 — không chọn gì ở chế độ cắt | unit | `F3_…` đỏ rồi xanh | pass |
| Hộp thoại có 3 trường | ui | ảnh đã xem, cái gì lệch | fail |
| Nối vào ống thật | live | ids, giá trị đọc lại | pass |
| C-16 — khoảng hở dưới dầm ≥ 50 mm | model | script kiểm, 55/61/58 mm | pass |
| Luồng đi hết | e2e | vì sao không đo được | not verifiable |

Rồi: cái gì đã đổi, `SPEC.md` đã đóng tới đâu, kết quả review kiến trúc, bug nào thành `fixed`, bao nhiêu
bài học vào hàng đợi `chờ xét`, tìm ra gì, và còn gì bỏ lại trong dữ liệu hay chưa làm.
Lane mà profile không khai ghi `không áp dụng` — đó là verdict hợp lệ, không phải lỗ hổng. Không commit gì.
