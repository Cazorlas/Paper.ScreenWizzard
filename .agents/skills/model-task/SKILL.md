---
name: model-task
description: Run a change to the user's model or drawing the way a code task runs - the project's rule file with codes, its samples and reference documents read first, a brief and a plan of work type model whose tasks cite the rule codes, the user's approval, then per group survey, dry run with nothing kept, commit and an independent read-back, a rule-check table at the end, every script kept as a rerunnable harness and every leftover reported by name. Also for learning a rule from something the user built, and whenever the user states a modelling or drafting rule, which is written into the rule file the same turn. Use for any request that edits model or drawing content rather than program code.
---

# model-task: luật + mẫu → SPEC, brief, plan → khảo sát / chạy thử / thực hiện / đọc lại → kiểm luật

Ở đây sản phẩm là **model hay bản vẽ của người dùng**, không phải code. Tài liệu đi theo luồng của skill
`spec` (SPEC.md, brief, plan); skill này thêm hai thứ một model cần — **luật có mã** và **mẫu** — cùng
tài liệu tham chiếu đứng sau luật, và thứ tự làm. Cách chạm tới host (chạy script trong phiên đang mở, trả
lời hộp thoại, chụp ảnh) nằm ở **skill live của gói host**; khuôn script theo ngôn ngữ của host nằm ở skill
model của gói host nếu dự án có.

## 0. Cái gì nằm ở đâu

Profile `.claude/paper.profile.json`, khoá `workTypes.model`, nói nơi đọc; không khai thì dùng quy ước dưới.

| Cái gì | Khoá profile | Quy ước | Ai viết |
|---|---|---|---|
| Luật chung dự án, mã `C-01`… | `rules` | `<featureDocs>/<dự án>-common/RULE.md` | agent, từ lời người dùng |
| Luật riêng hạng mục, mã `R-01`… | `rules` | `<featureDocs>/<feature>/RULE.md` — chỉ điều không chung; trỏ tới mã `C-` | agent |
| Mẫu | `samples` | `SAMPLE/<kiểu>.png` + `<kiểu>.md` cạnh RULE.md | agent |
| Tài liệu tham chiếu, mã `D-01`… | `references` | `REFERENCE/` (file người dùng thả) + `REFERENCE.md` | người dùng thả, agent ghi mục |
| Yêu cầu (sống) | — | `<featureDocs>/<feature>/SPEC.md` | agent, lời người dùng, không code |
| Brief, plan | — | cạnh SPEC.md, từ `spec/brief-template.md`, `spec/plan-template.md` (khối "Plan model") | agent |
| Script | — | `<featureDocs>/<feature>/harness/` + README ghi mã luật mỗi script | agent |

Mã luật đổi tiền tố được: `workTypes.model.rulePrefixes`. Khuôn: [rule-template.md](rule-template.md),
[sample-template.md](sample-template.md), [reference-template.md](reference-template.md).

## 1. Người dùng nêu một luật — ghi trong cùng lượt

Luật đến bằng lời, ảnh, một dòng bảng tính, một lời sửa lúc review, hay một thứ họ đã dựng ("học chỗ này").
**Trong cùng lượt**:

1. Chung dự án (`C-`) hay riêng hạng mục (`R-`); lấy mã trống kế tiếp. Không dùng lại mã đã xoá.
2. Ghi vào RULE.md bằng lời của agent theo bốn phần của [rule-template.md](rule-template.md): luật bằng số,
   nguồn (lời người dùng + ngày, hay file đã chép vào `input/`, hay mã `D-`), cách kiểm (script + thế nào
   là đạt, hoặc "không đo được" và vì sao), mẫu liên quan.
3. Trái một luật đã có: ghi cả hai cạnh nhau, luật mới mang dấu `chờ chọn`, hỏi. Không dựng theo luật nào
   trong hai tới khi người dùng chọn.
4. Báo mã cho người dùng. Memory chỉ giữ con trỏ tới RULE.md, không bao giờ nội dung luật.

**Học từ model** (người dùng dựng trước): khảo sát vùng đó bằng script chỉ đọc — phần tử, loại, kích thước,
cao độ hay lớp, chuỗi nối, ai dựng — chụp một ảnh, rồi viết luật (bước trên), một SAMPLE và một script
kiểm. Model không đổi gì.

**Một tài liệu đến** (thả vào `REFERENCE/`, dán, link, đường dẫn bản vẽ): cùng lượt thêm một mục `D-` vào
REFERENCE.md theo [reference-template.md](reference-template.md) — ở đâu, phiên bản, phủ gì, **cách đọc**
(trang, layout, lớp, tỉ lệ, độ lệch toạ độ, công cụ và script), luật nào dùng nó, bẫy. File vài MB trở lên
để nguyên chỗ, mục ghi đường dẫn. Đầu mỗi yêu cầu dựng hình, liệt kê `REFERENCE/` tìm file chưa có mục và
ghi mục trước.

## 2. Mẫu

Một kiểu nối, một kiểu chi tiết = `SAMPLE/<kiểu>.png` (ảnh chụp model hay bản vẽ thật, khoanh gọn quanh
nó) và `SAMPLE/<kiểu>.md` theo [sample-template.md](sample-template.md): trông thế nào, loại và kiểu dùng,
trình tự dựng, id của mẫu thật, bẫy đã gặp, mã luật nó thoả. Thêm hay sửa mỗi khi một cách dựng mới được
duyệt hay học được. Chụp trên view hay layout sẵn có, trả lại mọi thiết lập tạm sau khi chụp.

## 3. Một yêu cầu dựng hình — SPEC, brief, plan, rồi dừng

1. **Đọc trước**: mọi file `rules`, `samples`, `references` khớp với vùng việc này; SPEC.md của feature và
   README của `harness/`.
2. **SPEC.md** (skill `spec`): tạo mới với banner `Bản nháp chờ duyệt` hay sửa với dấu `Đổi bởi <brief>,
   chờ kiểm` — vùng đó phải thoả gì, lời và số của người dùng, không code.
3. **Brief** từ `spec/brief-template.md`: yêu cầu nguyên văn, ảnh hay bản vẽ đi kèm, id phần tử được đụng.
4. **Plan** từ `spec/plan-template.md`, khối "Plan model": `**Loại việc:** model` dưới dòng trạng thái;
   `## Luật áp dụng` (mỗi mã một dòng); `## Mẫu dùng`; `## Hiện trạng đo được`; `## Phương án dựng`;
   Tasks lane `model`, mỗi nhóm đủ bốn bước, **mỗi task nêu mã luật** và `{files:}` là `harness/` của
   feature; `## Kiểm tra luật` để trống, điền lúc cuối.
5. Khảo sát chỉ đọc được chạy trước khi duyệt. `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` phải
   exit **4** (2: thiếu mã luật ở một task — F12 — hay lane ngoài `model`). Trình ra luật, phương án, task
   **kèm liên kết mở được** tới `SPEC.md`, brief, plan và các RULE, SAMPLE, REFERENCE đã dùng — lần đầu hay
   lần duyệt lại nào cũng đủ; **kết thúc lượt**.

## 4. Mỗi nhóm: bốn bước, mỗi bước có bằng chứng

`/task-do` giao các task `[model]` cho agent `lane-model`; nó làm đúng bảng này.

| Bước | Làm gì | Dòng bằng chứng |
|---|---|---|
| Khảo sát | script chỉ đọc: id, kích thước, cao độ hay lớp, chủ sở hữu, chuỗi nối | số lượng và số đo, theo mã luật |
| Chạy thử | sửa trong một phạm vi hoàn tác được rồi huỷ — không giữ gì, không lưu; kiểm ngay trong script | log, `committed: false` |
| Thực hiện | cùng script, bật commit, chỉ khi chạy thử đạt và không lỗi | `committed: true`, id mới |
| Đọc lại | một script **khác**, chỉ đọc — không bao giờ lấy giá trị trả về của script sửa | số đo theo từng mã luật |

- Chạy thử hỏng → quay về script (`systematic-debugging`), không bao giờ sang thực hiện.
- Một phần tử từ chối (người khác giữ, hình không vừa) thì bỏ qua và nêu tên; phần còn lại làm tiếp. Không
  bao giờ sửa phần tử người khác đang giữ.
- Script tự gom cảnh báo và lỗi của host rồi huỷ; không để hộp thoại mở. Nhắc lưu hay đồng bộ: Cancel,
  trừ khi skill model của gói host hay luật dự án nói khác (ví dụ model dùng chung: đồng nghiệp xin quyền
  thì đồng bộ kèm trả quyền rồi làm tiếp). Lưu và đóng model là việc của người dùng.
- **Gọi quá giờ không phải gọi hỏng**: host có thể vẫn chạy nó sau. Script sửa ghi dấu bắt đầu trước tiên
  và tự kiểm lại trạng thái nó chờ, để lần chạy trùng tự bỏ qua.
- Đổi phương án giữa chừng: dòng `Spec bổ sung <ngày>` trong Decisions với lời người dùng, sửa SPEC.md,
  trạng thái về `chờ duyệt`, trình lại **kèm liên kết mở được** tới từng tài liệu đang chờ duyệt, kết thúc lượt.

Tick `- [x]` chỉ kèm dòng bằng chứng.

## 5. Đóng

1. Chạy script kiểm của mọi mã trong `## Luật áp dụng` trên model; mỗi mã một dòng trong `## Kiểm tra luật`:
   `| C-01 | script | giá trị đo | đạt / không đạt / không đo được – lý do |`.
2. Cách dựng mới → SAMPLE; luật mới học được trong lúc làm → RULE.md (mục 1).
3. Mọi script đã chạy vào `harness/`, chạy lại được: đầu file ghi nó đổi gì, kiểm mã nào, đầu vào, cách
   chạy lại (chạy thử trước). README một dòng mỗi script kèm mã luật.
4. View, lớp hay dấu tạm dùng để chạy script: xoá hết khi xong. Chỗ **còn lỗi** mà người dùng phải xem thì
   để lại dưới dạng view hay lớp kiểm, đặt tên theo luật của dự án; nêu tên từng cái trong báo cáo.
5. Đóng SPEC.md như skill `spec` nói; `find-bug` trên các dòng của nó; bài học đáng giữ → hàng đợi
   `chờ xét` như `/task-verify` nói.
6. Cổng exit 0, trạng thái `xong <ngày>`. Báo cáo: bảng kết quả, bảng kiểm luật, cái gì đã thực hiện mà chưa
   lưu, cái gì bỏ qua và vì sao, lệnh chạy lại harness, thứ tạm còn sót, quyết định còn mở.
