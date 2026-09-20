---
name: task-bug
description: Take a bug report to an approved fix plan - log it in the bug ledger (the docs bugs folder, status triaging) at intake, reproduce it first (a red test, or a read-back from the host session already open) and stop without a plan when it cannot be reproduced, classify it as a code bug or a spec gap, change SPEC.md when it is a spec gap, set the ledger status (open, not-a-bug, duplicate - never fixed), then write the brief and a plan with its work type, lanes and file ownership, and stop for approval. Use when the user types /task-bug or reports something broken.
---

# /task-bug <cái gì đang sai>

Một bug chưa ai dựng lại được là một phỏng đoán. Lệnh này **không bao giờ** viết kế hoạch sửa cho một
phỏng đoán.

1. **Đọc** `SPEC.md` của feature, `CODEMAP.md` và code trên đường được báo; đọc video, ảnh hay dữ
   liệu người dùng đưa, và chép chúng nguyên vẹn vào `<featureDocs>/<slug>/input/`.
2. **Vào sổ ngay lúc nhận.** Tra `<docs>/bugs/README.md` (thư mục cha của `<featureDocs>`; chưa có thì
   tạo từ `task-bug/bugs-readme-template.md`) xem đã có ai báo điều này chưa. Rồi tạo
   `<docs>/bugs/<PREFIX>-NNN-<slug>.md` từ `task-bug/bug-template.md` với Status `triaging`, và thêm dòng
   của nó vào sổ. Prefix là chữ cái đầu tên feature (chưa có thì đăng ký vào bảng Prefix), `APP-` khi
   không thuộc feature nào. Ghi **Hiện tượng** bằng lời người báo.
3. **Dựng lại** bằng bằng chứng rẻ nhất cho thấy kết quả sai, theo lane của nó:
   - logic quyết định → một test **fail ở assertion**, không phải fail vì build (lane `unit`);
   - một cửa sổ → mock UI trên dữ liệu giả (lane `ui`);
   - luồng người dùng đi hết → chạy thật, dựng lại được (lane `e2e`);
   - hành vi trong host → đo trong **phiên host đang kết nối**, trên dữ liệu chính lần chạy tạo ra rồi
     dọn, đọc lại từ host (lane `live`, theo skill live của gói host). Không bao giờ lưu dữ liệu của người
     dùng. Mở, tắt hay restart một host - kể cả bản copy bỏ đi được - phải được người dùng đồng ý **trước**;
     không có host nào đang mở thì hỏi, không tự mở;
   - model hay bản vẽ sai so với luật có mã → khảo sát **chỉ đọc** vùng đó, đo đúng mã luật bị phạm (lane
     `model`, skill `model-task`). Không sửa gì khi đang dựng lại.

   Giữ lại lệnh, id và các con số — đó là dòng bằng chứng đầu tiên.
4. **Không dựng lại được → dừng.** Trước khi kết luận thế trong host, loại trừ bẫy "host đang chạy bản
   cũ": một bản build mới không vào được host (bản đã nạp sẵn thắng mà vẫn báo thành công) thì lỗi đã sửa
   hay chưa sửa trông như nhau — skill live của gói host nói cách kiểm. Ghi đã thử gì và đo được gì vào mục
   **Tái hiện** của file bug, Status giữ `triaging`, và **không** viết kế hoạch sửa. Một nghi ngờ không có input thì vào báo cáo, không vào plan.
5. **Xếp loại** (skill `spec`):
   - *code bug* — code phạm một dòng luật trong `SPEC.md`. Spec đúng, **không sửa** `SPEC.md`; test
     viết từ chính dòng đó.
   - *spec gap* — code làm đúng spec mà kết quả vẫn sai, hoặc spec không nói gì về case này. **Sửa
     `SPEC.md` trước**: luật mới đề xuất bằng số (`Cho <hình dạng> → **<kết quả đo được>**`), dấu
     `> Đổi bởi <brief>, chờ kiểm` ngay dưới heading của mục bị đổi, gạch dòng nó trái ngược.
6. **Tìm nguyên nhân** bằng skill `systematic-debugging`, đủ xa để gọi tên được thứ mà bản sửa sẽ đổi.
   Ghi **Tái hiện**, **Bản chất** (tới `file:line`) và **Hướng fix (chưa làm)** vào file bug, rồi đặt
   Status — ở file và ở sổ, cùng lúc:
   - `open` — bug thật (code bug hay spec gap). `SPEC.md` link tới nó: từ dòng `F<n>` của bảng "When it
     does not do the job" mà nó chạm (thêm dòng `F` mới nếu đó là một skip im lặng chưa có dòng), hoặc
     từ mục "What it does not do yet";
   - `not-a-bug` — hành vi của host, đúng thiết kế, hay hiểu sai công cụ: ghi lý do, báo lại, **dừng** —
     không brief, không plan;
   - `duplicate` — trỏ về mã đã có, **dừng**.

   `/task-bug` **không bao giờ** đặt `fixed` — đó là việc của `/task-verify` trên plan sửa. `open — giữ
   có chủ ý` chỉ đặt khi chủ dự án nói không sửa. File bug không bao giờ bị xoá.
7. **Viết brief và plan như `/task-spec`** (`spec/brief-template.md`, `spec/plan-template.md`) — cả dòng
   `**Loại việc:**` (sửa chương trình là `code`, sửa model hay bản vẽ là `model`), lane của loại đó và
   `{files:}` cho mỗi task sửa file: brief
   ghi mã bug, lời báo lỗi, dữ liệu và id đã lộ ra lỗi, và mục `SPEC.md` đổi (hoặc "không đổi — code
   bug"); plan ghi mã bug ở **Bug liên quan** của Context, có bước dựng lại là `T1` **đã tick kèm dòng
   bằng chứng**, và loại lỗi trong Decisions. File bug link tới plan ở mục Hướng fix.
8. Trạng thái plan `chờ duyệt`; `paperflow.ps1 tasks -Path <plan>` exit **4**. **Dừng chờ duyệt** như
   `/task-spec` vẫn làm, gồm cả việc trình ra **kèm liên kết mở được** tới file bug, `SPEC.md` (nếu đổi), brief
   và plan — lần nào cũng đủ.
