# Plugin ngoài: ai làm gì

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
| UI/UX design review | wireframe in the plan, the host's style skill | `/design-critique`, `/accessibility-review`, `/ux-copy`, `/design-system`, `/ui-ux-pro-max` ship in the kit as **manual-only** skills (`disable-model-invocation`, ADR-0024): they run only when the user types them, and a finding that should change the UI becomes a task in the plan, never an edit outside it |

Một plugin ngoài chạy ngoài ô của nó là một lần đi tắt: kết quả không có dòng nào trong plan, và cổng
`tasks` không thấy nó.

**Cùng luật đó áp cho skill của chính dự án.** Một chặng chỉ có một chủ, và chủ là kit: dự án **không**
được có skill thứ hai cho một chặng kit đã giữ, kể cả khi nó lễ phép bảo "đọc skill kia trước". Cái được
phép ở lại dự án là **kiến thức miền** (P/Invoke, installer, harness của một mảng) và **giá trị cụ thể**
— những giá trị đó đi vào `paper.profile.json` và các file nó trỏ tới (`live.notes`, `live.dialogRules`),
không đi vào một skill. Skill riêng của dự án phải có tên trong `profile.projectSkills` kèm một dòng lý do.
Lý do và cái giá đã trả: `Paper-skills/docs/adr/0009`.
