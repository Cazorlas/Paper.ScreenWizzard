---
name: paperflow
description: Use when the user types /paperflow with a request, or asks for a feature, fix or model change in a Paper project to be carried from request to verified result without further prompting.
---

# /paperflow <yêu cầu>

Người dùng gõ một yêu cầu; skill này chạy từ yêu cầu tới báo cáo và **chỉ dừng ở chỗ phải dừng**. Nó
không biết dự án nào hay host nào: mọi thứ riêng đến từ `.claude/paper.profile.json` và các skill của
gói host. Skill này **chỉ xếp thứ tự** — luật của mỗi chặng nằm ở skill chặng đó gọi; đọc nó, đừng chép lại.

**Yêu cầu là quyền** chạy mọi verb của profile (`build`, `test`, `ui`, `e2e`, `publish`, `live`) trên máy
này và trên phiên host **đang mở** — và chỉ thế. Phần còn lại nằm ở bảng ngay dưới.

## Khi nào dừng

Năm dòng này, cộng lần dừng khi một task chặn sau khi đã đổi giả thuyết (task-do mục 4), là **danh sách đầy đủ**
những lần `/paperflow` được dừng.

| Lúc | Hỏi gì |
| --- | --- |
| Loại việc không rõ, hay không thuộc `workTypes` (F16) | **một** câu chọn loại việc, rồi dừng |
| Sau chặng 1 | duyệt SPEC.md + plan — **lần dừng theo kế hoạch duy nhất** |
| Vừa ghi `đã duyệt` | không hỏi gì: **mỗi plan một phiên** — in khối bàn giao (mục 2) rồi kết thúc lượt; người dùng đã bảo "làm luôn" thì đi tiếp |
| Giữa chừng hoá ra SPEC.md sai (spec gap) | duyệt lại dòng luật mới |
| Sắp chạm thứ **không hoàn tác được**: commit, push, gộp nhánh, bỏ worktree; lưu hay đồng bộ model/bản vẽ/dữ liệu của người dùng; xoá hay ghi đè thứ nằm ngoài repo; mở/tắt/khởi động lại host kể cả bản copy (verb trả 3); phát hành | xin phép, nói rõ sắp chạm cái gì và vì sao |

Từ lúc `/task-do` bắt đầu (phiên mới, hay ngay sau "làm luôn") tới báo cáo chặng 7 là **một lượt liền mạch**: không báo tiến độ rồi chờ, không
hỏi "có làm tiếp không", không dừng vì một nhóm task vừa xong. Hết việc mới dừng.

## Luật vận hành

Mười một luật này đúng cho mọi chặng dưới đây, và mỗi luật có một điều kiện xong riêng — một luật không nói
được lúc nào nó xong thì nó là lời khuyên, không phải luật.

1. **Hồi phục trước đã.** Trước bất cứ việc gì, tìm plan của tính năng này. Có plan thì **đọc nó và mọi
   artifact nó nhắc**, tóm tắt ngắn trạng thái, rồi đi tiếp từ chặng plan đang dừng. *Xong khi:* báo cáo
   nêu chặng vào và vì sao. **Một việc đã có plan thì được tiếp, không bao giờ bị bắt đầu lại.**
2. **Một quyết định im lặng là một lỗi.** Mọi lựa chọn — kể cả lựa chọn mình tự tin — phải nằm trong
   `Decisions` của plan hay trong dòng bằng chứng, kèm phương án đã loại. *Xong khi:* đọc plan là biết vì
   sao, không cần đọc hội thoại.
3. **Đo trước khi kết luận.** Một câu nói về code phải có lệnh sinh ra nó. *Xong khi:* dòng bằng chứng
   mang lệnh, id và giá trị đọc lại.
4. **Đọc trước khi ghi.** Artifact đã có thì mở rộng, giữ nguyên mục của người khác; tên mục là hợp đồng
   (`paper-kit/docs/ARTIFACT-REGISTRY.md` trong kho Paper-skills, không phải kho dự án). *Xong khi:* file giữ nguyên mọi mục nó có trước đó.
5. **Cổng có thể hoãn, không thể bỏ.** Chặng 2 (duyệt) và chặng 5 (kiểm) là cổng. *Xong khi:* cổng chạy
   và verdict được ghi.
6. **Nói rõ đang ở chế độ nào.** Thiếu lane agent, thiếu host, thiếu skill của gói host — vẫn chạy được,
   nhưng phải nói ra và ghi vào báo cáo. *Xong khi:* báo cáo nêu tên thứ đã thiếu.
7. **Không bao giờ lặp lại một lần thử không đổi.** Cùng một test đỏ ba lần trên cùng giả thuyết thì đổi giả thuyết, không đổi
   tham số. *Xong khi:* có một dòng `đổi giả thuyết: <cũ> → <mới>`.
8. **Chỉ dừng ở một dòng của bảng "Khi nào dừng".** Một plan đã duyệt là uỷ quyền chạy **tới cổng kế
   tiếp**, không phải tới task kế tiếp rồi xin phép lại. *Xong khi:* lần dừng gần nhất khớp một dòng của
   bảng đó, và nói được nó là dòng nào.
9. **Mời người dùng đọc gì thì đưa liên kết bấm được tới đúng chỗ đó** — không chỉ lúc xin duyệt, mà
   mọi lần mời xem, kiểm hay chốt: mỗi tài liệu một dòng `[tên](<đường dẫn tuyệt đối>)`, số dòng ghi trong
   chữ của liên kết, không bao giờ `#L` trong đích — cả hai đều không mở được từ terminal. Luật đủ: `references/approval-links.md`. *Xong khi:* mỗi tài liệu được nhắc có một liên kết. Có hook Stop `link-nag` giữ lượt khi thiếu.
10. **Effort cao để nghĩ, effort đang set để làm; model không đổi.** Phần *nghĩ* (suy luận, `SPEC.md`,
   plan, chia task, rà soát) chạy ở effort ít nhất `high` — đang `high` thì lên `xhigh`, đang cao hơn thì giữ;
   phần *làm* (code, task, build, test, lái host) chạy ở effort người dùng đang set. Model người dùng chọn chạy
   cả hai phần, mọi host, mọi loại việc (ADR-0026 của kit). Session xin `/effort` một câu rồi đi tiếp, không dừng lượt.
   Luật đủ: `references/effort-switch.md`. *Xong khi:* báo cáo nói effort nào cho phần nghĩ và phần làm.
11. **Host kiểm được thì đo trước, rồi mới đỏ.** Profile khai `live.loop` nghĩa là host tự kiểm được một
   thay đổi code: thứ tự là **ensure → đo trên host (dòng `baseline`) → test đỏ viết từ số đo → xanh → đo
   lại, lặp tới khi đạt**. Không khai thì đỏ trước. Luật đủ: skill `task-do`, "Host kiểm được thì đo
   trước". *Xong khi:* mỗi nhóm có code kiểm được trên host mang một dòng `baseline` trước dòng đỏ của nó,
   hay `baseline: not checkable - <vì sao>`.

## 0. Loại việc — trước mọi thứ

Đọc `.claude/paper.profile.json`: `workTypes` (mỗi khoá là một loại việc, kèm `lanes`), `lanes`, `verbs`,
`hosts`. Dự án chưa có profile thì dừng: bảo người dùng chạy setup của kit.

Không gõ `/paperflow` cũng tới đây: hook `paperflow-route` đọc lời nhắn, khớp từ khoá của từng loại việc và
từng host (`routing` của profile cộng file route của gói host) và thêm một đoạn nêu loại việc, host và thứ tự
vòng lặp của host đó. Nó chỉ gợi ý — loại việc vẫn do bảng dưới quyết định.

| Yêu cầu | Loại việc | Chặng 1 chạy |
| --- | --- | --- |
| đổi chương trình: tính năng, giao diện, refactor | `code` | `/task-spec` |
| báo một thứ đang sai trong chương trình | `code` | `/task-bug` |
| dựng, sửa, kiểm model hay bản vẽ của người dùng; "học luật này" | `model` | skill `model-task` |

Yêu cầu nêu cả hai: tách thành hai plan, mỗi plan một loại. Không đoán: loại việc không rõ, hay profile
khai `workTypes` mà không có loại này → hỏi **một** câu với các loại profile khai làm lựa chọn, và **không
chạy tiếp** (F16). Profile không khai `workTypes`: việc là `code`.

Thêm, chỉ khi yêu cầu có: người dùng muốn plan bị chất vấn → skill chất vấn của dự án trước khi chốt
chặng 1; video hay ảnh → đọc nó trước, lấy dòng luật từ điều được nói và được thấy.

## 1. Yêu cầu, brief, plan

- `code`: `/task-spec <yêu cầu>` — SPEC.md trước, brief, plan có `**Loại việc:** code`.
- bug: `/task-bug <cái sai>` — **dựng lại trước** (test đỏ ở assertion, hay đo trên host đang mở). Không
  dựng lại được → **dừng**, báo đã thử gì và đo được gì; không viết kế hoạch sửa.
- `model`: skill `model-task` — đọc luật, mẫu, tài liệu tham chiếu từ các glob của `workTypes.model`
  trước; plan `**Loại việc:** model`, mỗi task nêu mã luật.

Mỗi task mang lane của loại việc đó và `{files: …}` khi nó sửa file; các lane trong một nhóm `### n.` sẽ
chạy song song. **Cổng:** `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` exit **4** (2 là sai khuôn —
F3, F12, lane ngoài loại việc: sửa plan rồi kiểm lại).

## 2. Dừng chờ duyệt
Trình tài liệu chờ duyệt **kèm liên kết mở được, mỗi lần trình duyệt, không trừ lần nào** (luật 9), các
dòng luật mới hay đổi, task theo nhóm, và câu nào còn cần trả lời. **Kết thúc lượt.** Đồng ý → ghi
`**Trạng thái:** đã duyệt <ngày> ("lời họ nói")` vào plan. **Cổng:** exit không còn là 4 vì người dùng đã
duyệt, không bao giờ vì agent tự sửa dòng trạng thái.

**Mỗi plan một phiên** (ADR-0023 của kit): plan là bản bàn giao, còn phiên viết spec thì đã dài. In liên kết tới plan
và hai lệnh `/clear` rồi `/task-do <đường dẫn plan>` cho phiên mới, rồi **kết thúc lượt**; hook `session-anchor`
nhắc plan đang dở khi phiên mới mở. Effort đổi ở đây nếu đổi effort vỡ cache (luật 10). Người dùng nói "làm luôn" thì
đi tiếp ngay trong phiên này, và ghi một dòng Decisions.

## 3. Worktree
Mỗi worktree task của Claude và Codex phải gắn với checkout cha tạo ra nó; tạo từ worktree con thì
gắn cha trực tiếp đó. Theo skill `task-worktree` để ghi và xác nhận parent; trong Orca không dùng
`--no-parent` trừ khi người dùng yêu cầu tách riêng. Parent là quan hệ quản lý, khác nhánh Git gốc.
Lane `unit` và lane `ui` **luôn** chạy trong worktree riêng của chúng (`isolation: worktree`) và trả việc
về bằng một nhánh; `/task-do` mục 3 giữ luật gộp. Lane `live` và lane `model` ở lại cây chính, vì host chỉ
có một. Thêm một worktree cho **cả việc** (skill `task-worktree`: `create` → `carry` → `baseline`) khi
người dùng bảo, khi phiên khác đang làm ở cây chính, hay khi build và test đỏ của việc sẽ chặn người khác.

## 4. Làm — `/task-do <plan>`

Planner viết hợp đồng test; worker lượt đỏ viết mã test, planner duyệt và khoá; worker lượt xanh không sửa, không nới, không xoá test đã khoá. Việc nhỏ (cấu hình, câu chữ, một chỗ) session chính làm thẳng, không planner, không worker. Mọi lane giao worker theo chuỗi Codex → Antigravity → DeepSeek → Claude Sonnet, bỏ qua worker không chạy được phép kiểm của task (`task-do/references/planner-worker.md`). Planner có thể viết thêm test đỏ khi duyệt và trả task về worker.

`/task-do` giữ luật; tóm tắt để biết chờ gì:

- Theo từng nhóm `### n.`: mỗi lane trong nhóm một lane agent, gọi **trong cùng một lượt**, chạy nền —
  `lane-logic` (`unit`), `lane-ui` (`ui`, `e2e`), `lane-live` (`live`), `lane-model` (`model`) — mỗi worker
  nhận đầu ra `paperflow.ps1 brief -Task <mã>` cùng đúng các dòng `Cho … →` task phủ - không nhận đường dẫn plan hay SPEC.md.
- Nối tiếp: task ghi `(sau T<n>)`, nhóm sau chờ nhóm trước, `ui` và `live` không bao giờ cùng lúc (cùng
  màn hình).
- Session chính giữ plan, SPEC.md, dấu tick, mọi câu hỏi cho người dùng và mọi bước cần công cụ host mà
  lane agent không có; tick từ dòng bằng chứng agent trả về.
- Mọi lane xong: session chính chạy **một lần** verb `build`; planner duyệt mọi task đã tick kèm bằng chứng và verb `build`
  phải pass. Không chạy lại toàn bộ test ở đây.

**Cổng:** mọi task đã tick kèm bằng chứng (được planner duyệt), và verb `build` đã chạy một lần và pass.

## 5. Kiểm — `/task-verify <plan>`
`find-bug` trên SPEC.md, agent `architecture-reviewer` trên file đã đổi (plan code), bảng kiểm luật (plan
model), đóng SPEC.md, sổ bug, hàng đợi bài học, cổng exit **0** và trạng thái `xong <ngày>`. Một phát hiện
có input hay một vi phạm kiến trúc → về chặng 4 với task sửa; không đi tiếp qua cổng đỏ.

## 6. Tài liệu — `/sync-docs`
SPEC.md và `CODEMAP.md` khớp code đã đổi, plan còn mở, dấu nháp còn sót. Việc còn dang dở → handover.

## 7. Báo cáo

Bằng ngôn ngữ của người dùng, ngắn:

1. **Kết quả** — một dòng mỗi dòng luật và mỗi mã `F<n>` việc này chạm:
   `| Luật (SPEC.md) | Lane | Bằng chứng (lệnh, id, giá trị đọc lại, ảnh) | pass / fail / not verifiable |`.
   Lane dự án không khai: `không áp dụng` (F2). Plan model: thêm bảng kiểm luật theo mã.
2. **Đã đổi** — file, theo lane.
3. **Harness** — mọi bằng chứng chạy lại được: test, case UI, entry point hay script, các lời gọi MCP đúng
   như đã gọi, kèm verb chạy lại.
4. **Phát hiện** — `find-bug` xác nhận gì, nghi ngờ nào chưa có input, cái gì không kiểm được.
5. **Còn lại** — thứ đã thực hiện mà chưa lưu, thứ tạm còn sót, việc hoãn, và việc chờ quyền người dùng
   (commit, gộp, lưu, khởi động lại).

## Plugin ngoài: ai làm gì

Một plugin ngoài không bao giờ thay một chặng; nó chỉ được gọi ở đúng ô của nó, và skill riêng của dự án
không được giữ một chặng kit đã giữ. Bảng từng chặng và luật đủ: `references/plugins.md`.

## Ba verdict ở mọi cổng

`pass` (đo được, đúng) · `fail` (đo được, sai → `systematic-debugging`, về chặng sớm nhất sửa được) ·
`not verifiable` (host không kết nối, 0 test chạy, verb trả 4) → chạy lại **đúng một lần**; lần hai là môi
trường, ghi lý do, **không sửa code**. Verb trả 5 là `không áp dụng`, không phải lỗi. Cùng một test đỏ ba
lần không tiến triển → đổi giả thuyết, không đổi tham số (luật 7).

## Mẫu dự án mới dùng lệnh này thế nào

Không sửa skill này: chạy setup của kit với host của dự án, rồi viết một
`.claude/paper.profile.json`. Khuôn đầy đủ, từng khoá và cách đổi quy trình cho mọi dự án:
`references/new-project.md`.
