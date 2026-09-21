---
name: paperflow
description: Run one request end to end in any Paper project - decide the work type (code or model) from the profile, asking one question when it is unclear, write the requirement, brief and plan through /task-spec (a bug is reproduced first through /task-bug, a model change goes through model-task), stop for the user's approval, optionally open a task worktree, dispatch lane agents in parallel by the plan's file ownership through /task-do, run the full suite and build once, verify through /task-verify, sync the docs, and report every SPEC rule as pass, fail or not verifiable. Every command is a profile verb, so the same stages run in every project. Use when the user types /paperflow with a request, or asks for a feature, fix or model change to be carried through without further prompting.
---

# /paperflow <yêu cầu>

Người dùng gõ một yêu cầu; skill này chạy từ yêu cầu tới báo cáo và **chỉ dừng ở chỗ phải dừng**. Nó
không biết dự án nào hay host nào: mọi thứ riêng đến từ `.claude/paper.profile.json` và các skill của
gói host. Skill này **chỉ xếp thứ tự** — luật của mỗi chặng nằm ở skill chặng đó gọi; đọc nó, đừng chép lại.

**Yêu cầu là quyền** chạy mọi verb của profile (`build`, `test`, `ui`, `e2e`, `publish`, `live`) trên máy
này và trên phiên host **đang mở** — và chỉ thế. Phần còn lại nằm ở bảng ngay dưới.

## Khi nào dừng

Bốn dòng này là **danh sách đầy đủ** những lần `/paperflow` được dừng. Không có dòng thứ năm.

| Lúc | Hỏi gì |
| --- | --- |
| Loại việc không rõ, hay không thuộc `workTypes` (F16) | **một** câu chọn loại việc, rồi dừng |
| Sau chặng 1 | duyệt SPEC.md + plan — **lần dừng theo kế hoạch duy nhất** |
| Giữa chừng hoá ra SPEC.md sai (spec gap) | duyệt lại dòng luật mới |
| Sắp chạm thứ **không hoàn tác được**: commit, push, gộp nhánh, bỏ worktree; lưu hay đồng bộ model/bản vẽ/dữ liệu của người dùng; xoá hay ghi đè thứ nằm ngoài repo; mở/tắt/khởi động lại host kể cả bản copy (verb trả 3); phát hành | xin phép, nói rõ sắp chạm cái gì và vì sao |

Từ lúc plan được duyệt tới báo cáo chặng 7 là **một lượt liền mạch**: không báo tiến độ rồi chờ, không
hỏi "có làm tiếp không", không dừng vì một nhóm task vừa xong. Hết việc mới dừng.

## Luật vận hành

Mười một luật này đúng cho mọi chặng dưới đây, và mỗi luật có một điều kiện xong riêng — một luật không nói
được lúc nào nó xong thì nó là lời khuyên, không phải luật.

1. **Hồi phục trước đã.** Trước bất cứ việc gì, tìm plan của tính năng này. Có plan thì **đọc nó và mọi
   artifact nó nhắc**, tóm tắt trạng thái trong 3–5 dòng, rồi hỏi vào chặng nào. *Xong khi:* người dùng
   đã chọn điểm vào. **Một việc đã có plan thì được tiếp, không bao giờ bị bắt đầu lại.**
2. **Một quyết định im lặng là một lỗi.** Mọi lựa chọn — kể cả lựa chọn mình tự tin — phải nằm trong
   `Decisions` của plan hay trong dòng bằng chứng, kèm phương án đã loại. *Xong khi:* đọc plan là biết vì
   sao, không cần đọc hội thoại.
3. **Đo trước khi kết luận.** Một câu nói về code phải có lệnh sinh ra nó. *Xong khi:* dòng bằng chứng
   mang lệnh, id và giá trị đọc lại.
4. **Đọc trước khi ghi.** Artifact đã có thì mở rộng, giữ nguyên mục của người khác; tên mục là hợp đồng
   (`paper-kit/docs/ARTIFACT-REGISTRY.md`). *Xong khi:* file giữ nguyên mọi mục nó có trước đó.
5. **Cổng có thể hoãn, không thể bỏ.** Chặng 2 (duyệt) và chặng 5 (kiểm) là cổng. *Xong khi:* cổng chạy
   và verdict được ghi.
6. **Nói rõ đang ở chế độ nào.** Thiếu lane agent, thiếu host, thiếu skill của gói host — vẫn chạy được,
   nhưng phải nói ra và ghi vào báo cáo. *Xong khi:* báo cáo nêu tên thứ đã thiếu.
7. **Không bao giờ lặp lại một lần thử không đổi.** Cùng một lỗi hai lần thì đổi giả thuyết, không đổi
   tham số. *Xong khi:* có một dòng `đổi giả thuyết: <cũ> → <mới>`.
8. **Chỉ dừng ở một dòng của bảng "Khi nào dừng".** Một plan đã duyệt là uỷ quyền chạy **tới cổng kế
   tiếp**, không phải tới task kế tiếp rồi xin phép lại. *Xong khi:* lần dừng gần nhất khớp một dòng của
   bảng đó, và nói được nó là dòng nào.
9. **Mời người dùng đọc gì thì đưa liên kết bấm được tới đúng chỗ đó** — không chỉ lúc xin duyệt, mà
   mọi lần mời xem, kiểm hay chốt: mỗi tài liệu một dòng `[tên](đường dẫn)`, thêm `#L<dòng>` khi trỏ vào
   một dòng. Luật đủ: `references/approval-links.md`. *Xong khi:* mỗi tài liệu được nhắc có một liên kết. Có hook Stop `link-nag` giữ lượt khi thiếu.
10. **Model mạnh để nghĩ, model đang set để làm.** Phần *nghĩ* (suy luận, `SPEC.md`, plan, chia task, rà
   soát) chạy trên model mạnh nhất; phần *làm* (code, task, build, test, lái host) chạy trên model người
   dùng đang set. Hai cổng, và mặc định nghiêng về **không đổi**: tài khoản phải khớp
   `models.strongOnAccount` của profile (máy có thể đang ở tài khoản công ty — hạn mức đó là tiền người
   khác trả), rồi mới tới hạn mức của model mạnh. Luật đủ: `references/model-switch.md`. *Xong khi:* báo
   cáo nói model nào cho phần nghĩ và phần làm, hay `mặc định — sai tài khoản` / `— không khả dụng`.
11. **Host kiểm được thì đo trước, rồi mới đỏ.** Profile khai `live.loop` nghĩa là host tự kiểm được một
   thay đổi code: thứ tự là **ensure → đo trên host (dòng `baseline`) → test đỏ viết từ số đo → xanh → đo
   lại, lặp tới khi đạt**. Không khai thì đỏ trước như cũ. Luật đủ: skill `task-do`, "Host kiểm được thì đo
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

## 3. Worktree
Lane `unit` và lane `ui` **luôn** chạy trong worktree riêng của chúng (`isolation: worktree`) và trả việc
về bằng một nhánh; `/task-do` mục 3 giữ luật gộp. Lane `live` và lane `model` ở lại cây chính, vì host chỉ
có một. Thêm một worktree cho **cả việc** (skill `task-worktree`: `create` → `carry` → `baseline`) khi
người dùng bảo, khi phiên khác đang làm ở cây chính, hay khi build và test đỏ của việc sẽ chặn người khác.

## 4. Làm — `/task-do <plan>`

`/task-do` giữ luật; tóm tắt để biết chờ gì:

- Theo từng nhóm `### n.`: mỗi lane trong nhóm một lane agent, gọi **trong cùng một lượt**, chạy nền —
  `lane-logic` (`unit`), `lane-ui` (`ui`, `e2e`), `lane-live` (`live`), `lane-model` (`model`) — mỗi agent
  nhận mã task, đường dẫn plan và SPEC.md, và `{files:}` của từng task.
- Nối tiếp: task ghi `(sau T<n>)`, nhóm sau chờ nhóm trước, `ui` và `live` không bao giờ cùng lúc (cùng
  màn hình).
- Session chính giữ plan, SPEC.md, dấu tick, mọi câu hỏi cho người dùng và mọi bước cần công cụ host mà
  lane agent không có; tick từ dòng bằng chứng agent trả về.
- Mọi lane xong: session chính chạy **một lần** verb `build` và verb `test` đầy đủ.

**Cổng:** mọi task đã tick kèm bằng chứng; bản chạy đầy đủ `pass` theo `knownFailures`.

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

Máy Paper cài sẵn vài plugin của marketplace chính thức (`skills.json`). **Vòng đời vẫn là của skill này** —
một plugin ngoài không bao giờ thay một chặng, nó chỉ được gọi ở đúng ô dưới đây. Lý do và bài học:
`Paper-skills/docs/adr/0008`.

**Luật trước bảng: một plugin được bật chỉ khi nó không tự nhận việc.** Bảng dưới chỉ có tác dụng sau khi
skill này đã được đọc, nên nó không cứu được một plugin đã nói trước — một plugin có hook `SessionStart`,
hoặc có skill tự kích hoạt trùng vai. Những cái đó bị tắt trong `skills.json` chứ không bị fence bằng lời
(đo 2026-09-19: `microsoft-docs`, `frontend-design`, `mattpocock-skills` tắt; `superpowers` không cài).
Cái còn bật chỉ chạy khi được gọi đích danh — lệnh, agent, hoặc tool MCP — nên bảng là đủ cho chúng.

| Chặng | paper-kit giữ | Plugin ngoài vào ở đâu |
|---|---|---|
| Yêu cầu → SPEC, brief, plan, cổng duyệt | `/task-spec`, `/task-bug`, cổng `tasks` | `feature-dev` **không** thay chặng 1; chỉ mượn agent khảo sát của nó khi plan cần một vòng đọc rộng, kết quả đổ vào plan |
| Viết code theo lane | `/task-do` + lane agent | `code-simplifier` chạy **sau** khi lane đã xanh, chỉ trên file của task đó, không tự nới phạm vi |
| Rà trước khi đóng | `review-files` → `find-bug` → `architecture-reviewer` | `code-review` chạy **trên PR đã mở**, sau `/task-verify`; không thay `find-bug` — chỉ `find-bug` có bước đọc lại độc lập (F21) |
| Tra API | `api-lookup` + `docsSource` của host | `context7` cho **gói bên thứ ba**; không dùng cho API của host. `microsoft-docs` đã tắt: nó tự nhận mọi task C# và giành chỗ của `api-lookup` |
| Đọc/hiểu code C# | — | `csharp-lsp` tự do: nó không chạm vòng đời |
| E2E, giao diện web | verb `e2e`, skill style của host | `playwright` **chỉ** cho host `web`. `frontend-design` đã tắt tới khi có host `web`: nó tự nhận mọi cửa sổ WPF, mà chủ thật là skill style của host cộng wireframe đã duyệt |

Một plugin ngoài chạy ngoài ô của nó là một lần đi tắt: kết quả không có dòng nào trong plan, và cổng
`tasks` không thấy nó.

**Cùng luật đó áp cho skill của chính dự án.** Một chặng chỉ có một chủ, và chủ là kit: dự án **không**
được có skill thứ hai cho một chặng kit đã giữ, kể cả khi nó lễ phép bảo "đọc skill kia trước". Cái được
phép ở lại dự án là **kiến thức miền** (P/Invoke, installer, harness của một mảng) và **giá trị cụ thể**
— những giá trị đó đi vào `paper.profile.json` và các file nó trỏ tới (`live.notes`, `live.dialogRules`),
không đi vào một skill. Skill riêng của dự án phải có tên trong `profile.projectSkills` kèm một dòng lý do.
Lý do và cái giá đã trả: `Paper-skills/docs/adr/0009`.

## Ba verdict ở mọi cổng

`pass` (đo được, đúng) · `fail` (đo được, sai → `systematic-debugging`, về chặng sớm nhất sửa được) ·
`not verifiable` (host không kết nối, 0 test chạy, verb trả 4) → chạy lại **đúng một lần**; lần hai là môi
trường, ghi lý do, **không sửa code**. Verb trả 5 là `không áp dụng`, không phải lỗi. Cùng một test đỏ ba
lần không tiến triển → đổi giả thuyết, không đổi tham số (luật 7).

## Mẫu dự án mới dùng lệnh này thế nào

Không sửa skill này: chạy setup của kit với host của dự án, rồi viết một
`.claude/paper.profile.json`. Khuôn đầy đủ, từng khoá và cách đổi quy trình cho mọi dự án:
`references/new-project.md`.
