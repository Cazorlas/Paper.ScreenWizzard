---
name: sync-docs
description: End-of-session documentation sync - make each changed feature's SPEC.md and every changed folder's CODEMAP.md match the code changed this session, check open plans with the gate, flag SPEC.md drafts or change markers left behind by finished plans, check the bug ledger, present pending lessons (chờ xét) to the project owner and write only the accepted ones into their home, prune the red baseline, and write a handover when work remains. Use when the user types /sync-docs or before closing a session.
---

# /sync-docs

1. **Cái gì đã đổi.** `git status` và `git diff --stat` trong kho; nhóm file theo project và theo
   feature.
2. **Code map.** Với mỗi project đã đổi, cập nhật `CODEMAP.md` của nó (skill `code-map`) khi có file
   thêm, xoá, đổi tên, hay khi luồng đi qua chúng thay đổi. Rồi chạy skill `check-code-map` tới khi
   nó báo 0 chỗ lệch.
3. **Spec.** Với mỗi feature đã đổi, mọi hành vi đã kiểm trong phiên này phải có trong `SPEC.md` của
   nó, và không có gì ở đó trái với code (skill `spec`). Tài liệu và code lệch nhau thì nêu cả hai khả
   năng — tài liệu đã cũ, hay code sai — và để người dùng chọn; không tự sửa bên nào cho khớp bên kia.
   Chạy `check-spec`.
4. **Plan.** Chạy `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` trên mọi plan
   (`YYYY-MM-DD-<task>-plan.md`) đụng tới hôm nay. Liệt kê những plan không ở exit 0 kèm task còn mở
   của chúng.
5. **Spec bỏ quên.** Liệt kê mọi `SPEC.md` còn banner `Bản nháp chờ duyệt` hay dấu `chờ kiểm` mà plan
   trỏ tới nó đã `xong` — đó là lệch: việc đã xong mà yêu cầu chưa được đóng. Banner hay dấu của một plan
   còn `chờ duyệt`/`đã duyệt` là đúng, không liệt kê.
6. **Sổ bug.** Sổ `<docs>/bugs/README.md` khớp với các file bug: mỗi file một dòng, Status hai bên
   giống nhau. Bug `open` của một feature đã đổi hôm nay có link từ `SPEC.md` của nó (dòng `F<n>` hoặc
   "What it does not do yet"); bug `fixed` thì không còn link ở đó. Liệt kê mọi bug còn `triaging` — báo
   lỗi chưa dựng lại được, dễ bị quên nhất. Không xoá file bug nào.
7. **Hàng đợi bài học.** Trình cho người dùng mọi dòng `chờ xét` của `<docs>/retro/README.md` — ngắn: phát
   hiện, nhà đề xuất — và hỏi nhận hay không từng dòng. **Chỉ** dòng người dùng nhận mới được chép vào
   nhà của nó (`CLAUDE.md`, `SPEC.md`, comment ở code, skill — skill của kit thì sửa ở kho kit, không ở
   bản vendor), rồi đổi Status thành `đã nhận → <file>`; dòng bị gạt thành `không nhận — <lý do>`. Cả hai
   gạch ngang `~~ ~~`, **không bao giờ xoá**. Dòng người dùng chưa trả lời giữ `chờ xét` — tồn dư là bình
   thường. Không có dòng `chờ xét` nào thì bỏ qua bước này, đừng hỏi.
8. **Baseline đỏ.** Một test nằm trong `knownFailures` (khai ở `.claude/paper.profile.json`) mà nay
   xanh thì **xoá khỏi danh sách**. Danh sách phình ra mà không ai dọn là cách một suite xanh mất hết
   ý nghĩa.
9. **Handover.** Việc còn lại cho phiên sau → `docs/progress/YYYY-MM-DD-<slug>-handover.md` theo hình
   các handover đã có: trạng thái, bằng chứng, bước kế tiếp, và **cái gì còn bỏ lại trong dữ liệu
   thật** của host nào.
10. **Báo cáo** bảng: file | đã đồng bộ gì | còn mở gì — kèm số bug còn `open`, số bug còn `triaging` và số bài học còn `chờ xét`.
