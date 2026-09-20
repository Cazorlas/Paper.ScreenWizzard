---
name: task-do
description: Implement an approved plan - gate first, then survey the code or the model, then each task group in order, its lanes dispatched in parallel to lane agents (logic, ui, live, model) with their task ids and the files each task may write, test-first inside every lane (a red test from the SPEC.md lines before its code, a UI mock before the real host, the open host driven on real data and read back, a model change dry-run before commit), ticking each task from the evidence rows the agents return, one full suite and build by the main session at the end, and hand over to /task-verify. Use when the user types /task-do with a plan or approves a plan.
---

# /task-do <plan>

Everything here is a verb. What a verb runs in this project is `.claude/paper.profile.json`; a verb
the profile does not declare exits **5 = không áp dụng**, which is a valid verdict — ghi lại rồi đi
tiếp, đừng khai một lệnh giả để cổng xanh.

`<plan>` là `<featureDocs>/<slug>/YYYY-MM-DD-<task>-plan.md`. Brief cùng tên không có đuôi `-plan` và
`SPEC.md` nằm cạnh nó.

## 1. Cổng — trước mọi thứ khác

```powershell
.claude/paperflow/paperflow.ps1 tasks -Path <plan>
```

| Exit | Làm gì |
| --- | --- |
| 4 | Chưa duyệt. Nói ra và **dừng** — không đọc code, không sửa gì |
| 2 | Sai khuôn. Sửa hình thức của plan (không đổi luật trong `SPEC.md`, không sửa brief) rồi chạy lại cổng |
| 1 | Đã duyệt, còn task mở — làm task chưa tick đầu tiên |
| 0 | Xong rồi — chỉ chạy `/task-verify` |

## 2. Khảo sát — thay đổi này đụng vào đâu

Đọc `SPEC.md` của feature (cả các mục mang dấu `chờ kiểm`), brief, plan, `CLAUDE.md` của kho và của
project con, `CODEMAP.md` trên đường đi, rồi source và test của nó. Tra mọi API member mà thay đổi sẽ
gọi ở `docsSource` của profile và ghi vào bảng **API đã tra** của plan — ghi **trước** dòng code gọi nó,
không phải sau.

Đọc thêm các dòng `đã nhận → <file>` của `<docs>/retro/README.md` (thư mục cha của `<featureDocs>`) —
bài học chủ dự án đã nhận là luật; dòng `chờ xét` chưa phải.

Plan `**Loại việc:** model`: khảo sát là đọc luật, mẫu và tài liệu tham chiếu mà `## Luật áp dụng` nêu
(glob `workTypes.model` của profile) và README của `harness/`; rồi chạy theo skill `model-task`. Câu hỏi
dịch vụ dùng chung bên dưới chỉ dành cho plan code.

**Trả lời một câu trước khi viết code, và ghi câu trả lời vào Context của plan:** các dịch vụ dùng
chung mà thay đổi này cần — transaction/undo, progress có cancel, thông báo/báo cáo, lưu settings, xử lý
failure/warning, logging — đã có sẵn API nó cần chưa? Chưa có thì **một task riêng thêm nó trước**:
thêm task đó lên trên task đầu tiên dùng nó, kèm một dòng Decisions. Thiếu Cancel hay RollBack phát
hiện ở task cuối thì phải làm lại mọi task đã gọi dịch vụ đó.

Tìm trong thư viện của kho trước khi viết helper mới. Việc tái cấu trúc chạy skill `parity-refactor`.

**Sắp viết một vòng lặp lồng trên tập phần tử, hay một phép tìm đường, hay một phép hình học? Hỏi bài
toán này đã có tên chưa.** Thứ tự tra: thư viện của kho → `System.Collections.Generic` và LINQ → rồi mới
tới tri thức chung. Biết tên thì được miễn phí độ phức tạp đã có người chứng minh và danh sách ca biên
người khác đã vấp; không biết tên thì viết lại một bản kém hơn **mà không biết mình đang viết lại**.

Luật không phải là "đổi thuật toán", mà là **gọi tên nó trong comment**. Một `O(n²)` chạy 8 ms không
phải bug; một `O(n²)` **không ai biết là `O(n²)`** mới là thứ treo máy người dùng ở model thật.

## 3. Các task, theo đúng thứ tự trong plan

**Test lấy từ các dòng `Cho … →` của `SPEC.md`, không lấy từ code vừa viết**: mỗi dòng là một case,
với đúng con số của nó. Mỗi dòng `F<n>` của bảng "When it does not do the job" là **một** test, tên
bắt đầu bằng mã (`F3_…`), kể cả dòng skip im lặng. Một test viết để khớp với cái code đang trả ra chỉ
chứng minh code bằng chính nó.

| Lane | Lane agent | Bằng chứng nào được tính |
| --- | --- | --- |
| `unit` | `lane-logic` | Test **trước**, thấy đỏ **ở assertion**; rồi code tới khi xanh. Verb `test` |
| `ui` / `e2e` | `lane-ui` | Mock trên dữ liệu giả theo wireframe trong plan trước khi chạy host thật; mở ảnh ra **xem** và tự phán xét. Verb `ui` / `e2e` |
| `live` | `lane-live` | Verb `publish`, rồi lái **phiên host đang kết nối** trên một case thật và **đọc lại** kết quả từ chính host. Dọn thứ đã tạo. Không bao giờ lưu dữ liệu của người dùng. Mở, tắt hay restart host - kể cả bản copy - phải hỏi người dùng trước, và ghi câu trả lời vào dòng bằng chứng |
| `model` | `lane-model` | Mỗi nhóm: khảo sát chỉ đọc → chạy thử không giữ gì → thực hiện → **đọc lại bằng script khác**; mỗi dòng nêu mã luật. Script giữ lại trong `harness/` |

### Một test xanh ngay lần đầu chưa phải là một test

Luật "thấy đỏ ở assertion" có một lỗ, và nó là lỗ hay dính nhất: **test viết cho hành vi đang đúng thì
xanh ngay**, không có gì để thấy đỏ. Lúc đó test chưa chứng minh được gì cả — nó có thể đang không kiểm
thứ nó tưởng mình kiểm.

**Xanh ngay lần đầu thì phải chứng minh nó không rỗng: phá đúng cái luật đó trong code sản phẩm, thấy nó
đỏ ở assertion, rồi `git checkout` trả code về.** Ghi vào dòng bằng chứng cả câu đỏ đã thấy. Chưa làm
bước này thì chưa được tick.

Đo 2026-09-19, hai lần trong một phiên: một test lọc thư mục build bằng so chuỗi con nên bỏ sót cả một
project (`Plumbing` chứa `bin`) và xanh trong khi thứ nó canh đang hỏng — chỉ lòi ra khi gỡ luật khỏi một
project để ép nó đỏ.

**Bốn hình dạng của một test không chứng minh gì** (đặt tên theo `wondelai/skills`, và xoá thẳng chứ đừng
sửa — nó không mang theo độ phủ nào để giữ):

| Hình dạng | Vì sao nó vô nghĩa |
|---|---|
| Khẳng định một mock trả về thứ chính test vừa bảo nó trả | Nó kiểm cái mock, không kiểm code |
| Tính giá trị mong đợi **bằng chính biểu thức** code đang dùng | Sai cùng nhau thì vẫn xanh |
| `Assert(HẰNG, Is.EqualTo(HẰNG))` | Xanh với mọi code biên dịch được |
| Snapshot một output chưa ai đọc mà đã duyệt | Đóng băng cái sai thành "đúng" |

Một test đặc tả ghim một giá trị **đã quan sát được** thì không nằm trong danh sách này; ghim thứ code tự
tính lại lúc assert thì có.

- Tick `- [x]` **chỉ khi** bằng chứng của task đã chạy, và thêm dòng bằng chứng: lệnh, id, giá trị đọc
  lại. Tick không có dòng bằng chứng làm cổng đỏ.
- **Không bao giờ viết lại một task đã tick.** Task xong mà hoá ra sai thì thêm task sửa bên dưới và một
  dòng Decisions nói vì sao. Dưới dấu tick cuối thì sắp lại task tự do.

### Chia cho lane agent

Đơn vị song song là **nhóm** `### n.` của plan: các lane trong một nhóm chạy cùng lúc, nhóm sau chờ nhóm
trước trả hết.

1. Với nhóm đầu còn task mở, gom task theo lane. Trong **một** lượt, gọi mỗi lane một `Agent` chạy nền, tên
   theo bảng trên, và đưa cho nó: các mã task của lane đó, đường dẫn plan và `SPEC.md`, và `{files:}` của
   từng task. Lane agent chỉ sửa file trong các glob đó, không commit, không push, không mở hay tắt host.
2. **Nối tiếp, không song song:** task ghi `(sau T<n>)` chờ `T<n>` trả về; lane `ui` và lane `live` không
   bao giờ chạy cùng lúc (driver UI giữ chuột và bàn phím, ảnh chụp host cần cửa sổ host ở trước); `live`
   chỉ chạy sau khi `unit` của cùng việc đã xanh. Một nhóm chỉ có một lane thì giao một agent, vẫn chạy nền.
   Task không có `{files:}` mà cần sửa file thì plan sai khuôn: thêm glob vào plan trước khi giao.
3. **Chờ** mọi agent của nhóm trả về. Session chính **giữ** plan, `SPEC.md`, dấu tick, bảng API, mọi câu
   hỏi cho người dùng và mọi bước cần công cụ host mà agent không có (agent trả `not verifiable: cần session
   chính` thì session chính tự chạy bước đó).
4. **Gộp bằng chứng:** mỗi dòng `| T<n> | … | verdict |` agent trả về thành một dòng dưới `## Bằng chứng`,
   tick task `pass`; dòng API vào bảng API; file agent báo đã sửa phải nằm trong glob của task — một file
   ngoài glob là fail của task đó. Task `fail` hay `not verifiable` không tick: xử lý theo phần 4 (vòng test).
5. Sang nhóm kế. Nhóm không lane (Close) để cho `/task-verify`.
6. **Hết mọi lane: session chính chạy một lần** verb `build` rồi verb `test` **đầy đủ** — lane agent chỉ
   chạy phần của mình, nên đây là lần duy nhất mọi thay đổi gặp nhau. Pass cần exit 0 **và** số test đã
   chạy > 0; fail ngoài `knownFailures` là fail, bất kể lane nào gây ra. Ghi dòng số test vào bằng chứng
   của task lane cuối cùng. Plan model không đổi code nào: bước này là `không áp dụng`, ghi rõ như vậy.

- Code tính năng đặt theo skill `clean-architecture`. Agent `architecture-reviewer` chạy trong
  `/task-verify` trên các file plan đã đổi (hook `layer-guard` không thấy file ghi qua lệnh shell); một
  vi phạm nó báo là **fail** và việc quay về đây.
- Task không chứng minh được ở lane của nó thì đổi lane ngay trong dòng bằng chứng, kèm lý do.
- Plan không có lane agent nào dùng được (dự án chưa vendored agent): session chính tự chạy từng lane theo
  đúng thứ tự trên, cùng các luật đó.

## 4. Vòng test — ba verdict, và chỉ một là lý do sửa code

| Verdict | Nghĩa | Làm gì |
| --- | --- | --- |
| `pass` | đo được, đúng | tick, ghi bằng chứng |
| `fail` | đo được, sai | skill `systematic-debugging`; về chặng sớm nhất sửa được |
| `not verifiable` | không đo được (host tắt, runner chạy 0 test, verb trả 4 hoặc 5) | chạy lại **đúng một lần** |

Bốn luật, vì đây là bốn cách một vòng test tự lừa mình:

1. **Exit 0 với 0 test đã chạy không phải pass** — nó là `not verifiable`. Luôn đọc số test đã chạy,
   đừng chỉ đọc exit code.
2. **`not verifiable` lần thứ hai là môi trường.** Ghi `môi trường: <lý do>` vào bằng chứng và
   **không** sửa một dòng code nào vì nó.
3. **Cùng một test đỏ ba lần** với cùng giả thuyết: viết một dòng `đổi giả thuyết: <cũ> → <mới>` vào
   plan trước lần thử thứ tư. Sau ba lần, cái sai là giả thuyết, không phải tham số.
4. **Baseline đỏ**: fail nằm trong `knownFailures` của profile thì cho qua; fail ngoài danh sách là
   **fail**, bất kể ai gây ra.

Không bao giờ lặp lại một lần thử không đổi.

**Kết quả cho thấy `SPEC.md` sai** — code làm đúng dòng luật mà kết quả vẫn sai — thì dừng việc: đó là
spec gap. Sửa `SPEC.md` (luật mới đề xuất **bằng số**, dấu `> Đổi bởi <brief>, chờ kiểm` dưới heading
của mục), thêm dòng `Spec bổ sung <ngày>` kèm số đo vào Decisions của plan, đặt dòng trạng thái về
`chờ duyệt`, trình ra **kèm liên kết mở được** tới `SPEC.md` (mục đổi), brief và plan — dù đây là lần duyệt lại
thứ hai hay thứ ba — rồi kết thúc lượt. Không sửa code cho khớp trước khi người dùng duyệt lại.

## 5. Giao lại

Khi mọi task có lane đã tick và bản chạy đầy đủ ở mục 3 đã `pass`, chạy `/task-verify <plan>` và đưa
nó dòng số test đó. Đừng báo xong trước khi nó chạy.
