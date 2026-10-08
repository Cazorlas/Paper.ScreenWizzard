---
name: jev-decision
description: Use when a feature should pick, score or answer yes/no with Jev (TypeSafe System One) - classifying CAD layers, sheets, families, tickets or commands, mapping columns to parameters, gating a risky action - or when measuring such a decision against keyword rules, or writing the port and adapter that call Jev.
---

# jev-decision

Jev là mô hình "System One" của TypeSafe: gửi một **trạng thái bằng chữ** và vài **câu hỏi chọn một**, nhận lại
lựa chọn, độ tin cậy và số token đầu vào. Jev **không viết chữ**. Mỗi câu hỏi có tối đa 255 lựa chọn, đầu vào chỉ
là chữ, mỗi lần gọi mất khoảng 0.4–0.7 s qua OpenRouter (lần đầu 1.4–2.3 s) và tốn chừng $0.00002. Tra API ở
https://docs.typesafe.ai (`api.md`, `primitives/choice.md`, `models.md`) hoặc trong skill `typesafe:typesafe-ai`,
**không** viết từ trí nhớ.

Skill này là **kiến thức miền** (ADR-0009, ADR-0029 của kit): nó không giữ chặng nào của vòng đời. Việc có code đi theo
`/task-spec` → `/task-do` như mọi việc khác; skill này nói câu hỏi gửi Jev trông thế nào, cái gì được rời máy, và
đo thế nào.

## Bảy luật

1. **Luật từ khoá trả lời trước, Jev chỉ tinh chỉnh.** Không có khoá, Jev lỗi, hay Jev trả lời một lựa chọn ngoài
   danh sách thì câu đó lùi về luật. Tính năng phải chạy trọn mà không cần mạng.
2. **Jev bật theo mặc định** (ADR-0042). Công tắc chung `PAPER_JEV` và công tắc riêng của mỗi tính năng (`PAPER_<TÍNH NĂNG>_JEV`) đọc cùng một cách:
   `0`, `false`, `off` hay `no` (bỏ khoảng trắng, không phân biệt hoa thường) là tắt; không đặt hay giá trị khác là bật.
   Một trong hai tắt thì **không** có lời gọi mạng nào, kể cả khi có khoá. Bật mà không có khoá thì không gọi,
   `Outcome` là `no-key` và người dùng được nói rõ thiếu khoá.
3. **State gửi đi là đúng những dữ kiện SPEC liệt kê.** SPEC của tính năng có dòng nói trường nào nằm trong state.
   Dữ liệu thật được gửi, gồm tên dự án, tên khách, tên người và tài liệu riêng: chủ dự án đồng ý ngày 2026-10-04
   (máy chủ Jev ở US-West). Danh sách "không gửi" của một công thức là hình state đã đo, không phải luật riêng tư: thêm
   một trường là đổi câu hỏi và phải đo lại (luật 4). Khoá chỉ đọc từ môi trường, không bao giờ in ra hay ghi vào file,
   log hay báo cáo. Fixture của công thức vẫn là dữ liệu **bịa**: nó được chép vào mọi dự án host ai và số phải đo lặp
   lại được; đo trên dữ liệu thật làm ở dự án.
4. **Câu hỏi gửi đi là câu hỏi đã đo.** Chữ của `questions.json` mà eval đo được là đúng chữ sản phẩm gửi đi. Có
   một test so thân yêu cầu với file đó. Sửa một chữ trong câu hỏi là phải đo lại.
5. **Các câu hỏi độc lập có thể cho ra câu trả lời mâu thuẫn nhau.** Ví dụ `S-BEAM` ra Structure + duct + supply
   air. Đặt một luật nhất quán **sau** Jev (ví dụ: bộ môn không cơ điện thì hệ thống là `none`). Luật này chỉ đụng
   nhãn có nguồn `jev`, và nhãn bị thay mang nguồn `rules`.
   Luật cần một dữ kiện của máy mà state không gửi (ví dụ danh sách bộ môn của dự án) cũng chạy sau Jev, nhưng
   đụng cả nhãn nguồn `rules`, vì nó áp dữ kiện chứ không sửa mâu thuẫn; fixture không đo nó.
6. **Không chắc thì đưa người xem.** Độ tin dưới ngưỡng (mặc định 0.70) thì thành "cần xem", không đoán. Mọi nhãn
   mang theo nguồn (`rules` / `jev` / `default`) và độ tin.
7. **Đo trước khi tin.** Có một tập dev và một tập holdout. Số của luật được đo trước. Luật nào chọn **sau khi đã
   thấy** holdout thì holdout không còn sạch với luật đó, và phải nói rõ khi báo cáo. Một lần chạy Jev không phải
   là một con số ổn định.

## Hình dạng code (skill `clean-architecture`)

- Port nằm trong `UseCases`: `I<Việc>Judge.JudgeAsync(<Facts>) → <Judgement>`. `<Facts>` là record thuần, chỉ
  chứa những trường được rời máy. `<Judgement>` mang từng nhãn kèm lựa chọn, độ tin và nguồn, cộng
  `Outcome` (`answered` / `off` / `no-key` / `failed`).
- Interactor gọi luật, sau đó gọi judge nếu công tắc bật, sau đó áp luật nhất quán, rồi trả **plan**. Unit test
  dùng judge giả, không cần mạng.
- Adapter HTTP nằm ở `Infrastructure` (C# không có SDK): `POST {state, model, questions}`, khoá đặt ở header
  `Authorization: Bearer`, đọc `answers.<id>.choice`/`.confidence` và `usage.input_tokens`. Một trả lời ngoài
  danh sách, hay độ tin ngoài khoảng 0–1, thì bị coi là **không trả lời**.
- **Hết giờ và thử lại theo số đo, không theo mặc định.** `HttpClient` mặc định chờ 100 s; adapter đặt timeout
  5 s (lần gọi đầu đo được 1.4–2.3 s), hết giờ là `failed` và câu đó lùi về luật. Lệnh người dùng đang chờ thì gọi
  một lần, không thử lại. Chạy lô: `429`, `5xx` và hết giờ được thử lại tối đa 2 lần, chờ 0.5 s rồi 1 s, mỗi lần
  cộng thêm ngẫu nhiên tới một nửa (jitter) để các lần thử không dồn vào cùng lúc; `401`/`403` dừng gọi Jev cho
  **cả lô** ngay từ dòng đầu (`no-key`), không hỏng N lần; `4xx` khác không thử lại. Test adapter bằng
  `HttpMessageHandler` giả: chậm quá timeout, `401` ở dòng đầu, `429` rồi `200`.
- Có hai đường, chọn bằng một policy chứ không bằng URL tự do. **OpenRouter là đường chính**: khoá
  `OPENROUTER_API_KEY`, `https://openrouter.ai/api` + `/v1/systemone`, model `typesafe/jev-1.13`; base `/api/v1` trả 404.
  TypeSafe `https://api.typesafe.ai/v1/systemone` với model `jev-latest` chỉ khi không có khoá OpenRouter
  (`TYPESAFE_API_KEY`).
- Câu tiếng Việt thì gửi **cả bản gốc lẫn bản bỏ dấu** trong state. Đo trên chatbot: Jev v2 đúng 38/41, so với
  32/41 của luật.

## Đo một công thức

Mỗi công thức là một thư mục `recipes/<slug>/` gồm bốn file:

| File | Là gì |
| --- | --- |
| `recipe.md` | khi nào dùng, state, câu hỏi, luật trước và sau Jev, port, số đo, nguồn |
| `questions.json` | phần `questions` của thân yêu cầu, gửi nguyên văn; chỉ có `type: choice` |
| `rules.json` | luật từ khoá cho từng câu hỏi; luật đầu tiên khớp thì thắng; khớp trên chữ thường đã bỏ dấu; luật overlap chấm tỉ lệ từ của một trường state có trong trường khác |
| `fixture.jsonl` | ít nhất 20 dòng **bịa**, mỗi dòng `{id, input, expect, case, synthetic: true}`; `case` là `plain` hoặc `hard`, và ít nhất 1/3 số dòng là `hard` |

Dòng `hard` chép **mẫu lỗi đã gặp trên dữ liệu thật** (tên lạ, tiếng Việt có dấu, phủ định, từ khoá đánh lừa), viết
bằng dữ liệu bịa. Viết các dòng `hard` **trước**, rồi mới viết luật, và không sửa luật cho tới khi luật đúng các dòng
đó. Dòng thường được viết cùng lúc với luật nên luật luôn đúng chúng. Chỉ dòng khó mới cho biết Jev giúp được tới đâu.

```powershell
# offline, không tốn tiền: in số của luật
powershell -NoProfile -File .claude/skills/jev-decision/scripts/jev-eval.ps1 -Recipe .claude/skills/jev-decision/recipes/<slug>
# Jev thật: chỉ khi chủ dự án đồng ý tiêu tiền. Một lệnh duy nhất: khoá chỉ sống trong lệnh đó,
# vì script chỉ đọc khoá từ môi trường của lần chạy (không đọc registry)
if (-not $env:OPENROUTER_API_KEY) { $env:OPENROUTER_API_KEY = [Environment]::GetEnvironmentVariable('OPENROUTER_API_KEY', 'User') }; powershell -NoProfile -File .claude/skills/jev-decision/scripts/jev-eval.ps1 -Recipe .claude/skills/jev-decision/recipes/<slug> -Jev
```

Dòng `` `rules: X/Y rows, hard H/K` `` trong mục "Đo" của `recipe.md` phải khớp với lần chạy offline (test
`jev-recipes` của kit có kiểm). Luật và fixture do cùng một người viết, nên con số này là **số dev**, không phải số
holdout. Số Jev chỉ ghi khi đã chạy thật: script in dòng `` `jev (<model>, <yyyy-MM-dd>): X/Y rows, hard H/K` `` và
dòng đó được chép **nguyên văn**, riêng một dòng, ngay dưới dòng `rules:`. Nghĩa của nó: câu Jev trả lời trong
danh sách thì lấy của Jev, câu Jev không trả lời (hay trả ngoài danh sách, độ tin không phải số hay ngoài 0–1) thì
lấy của luật; **chưa** áp luật nhất quán sau Jev của công thức. Script chỉ in dòng này khi không lời gọi nào hỏng;
có lời gọi hỏng thì nó nêu số lời gọi hỏng và exit 1, không có số nào để ghi. Test `jev-recipes` kiểm dòng này đếm
đúng số dòng và số dòng khó của fixture hiện tại (exit: 0 đã chấm, 1 lời gọi hỏng, 2 công thức lệch, 5 tắt hay
không khoá).

Đổi chữ câu hỏi hay nhãn fixture sau một lần đo thì chạy lại offline và Jev thật một lần. Số cũ giữ thành một câu
lịch sử, không backtick (test chỉ đếm dòng `` `jev (…)` ``), ghi ngày và "đổi sau lần đo đầu"; số mới không sạch như
số đầu, và recipe nói vậy.

`recipe.md` được chép vào mọi dự án host ai: không nêu tên dự án, tên khách hay đường dẫn trên một máy (test
`payload-contract` đỏ); nguồn từ một dự án ghi ở sổ nghiên cứu.

Script `.ps1` phải chỉ dùng ký tự ASCII (PowerShell 5.1). Chữ tiếng Việt trong script thì viết bằng `\uXXXX` rồi
`[regex]::Unescape`.

## Công thức

Mỗi mục: công thức — việc · host dùng · nguồn ý tưởng. Mục cách nhau một dòng trống, để mỗi nhánh công thức đổi
mục của mình mà không đụng mục kề bên.

- [layer-map](recipes/layer-map/recipe.md) — gắn bộ môn, loại phần tử, hệ thống MEP cho lớp CAD · cad → revit · việc của chủ dự án; demo Proq

- [sheet-classify](recipes/sheet-classify/recipe.md) — cắt bộ PDF bản vẽ thành sheet, gắn bộ môn và loại sheet · revit, cad · Proq, DocJev

- [classification-code](recipes/classification-code/recipe.md) — gán mã Uniclass bằng cách đi cây phân loại · revit · IAB category tree walk

- [family-tags](recipes/family-tags/recipe.md) — gắn category và tag cho thư viện `.rfa` · revit · 1,018 papers → 24 topics

- [clash-resolution](recipes/clash-resolution/recipe.md) — chọn cách gỡ va chạm MEP (nâng, hạ, đổi cỡ, đi vòng, người xử lý) · revit · MuJoCo robot arm

- [excel-param-map](recipes/excel-param-map/recipe.md) — ghép cột Excel của tư vấn vào parameter Revit · revit · Spreadsheets that read intent

- [titleblock-fill](recipes/titleblock-fill/recipe.md) — điền title block hoặc parameter từ dữ liệu bẩn · revit, cad · Dirty-field PDF form fill

- [csharp-risk-gate](recipes/csharp-risk-gate/recipe.md) — chấm rủi ro trước khi chạy `execute_csharp` · revit · OpenCode shell-safety, Vercel fx

- [chatbot-confirm-gate](recipes/chatbot-confirm-gate/recipe.md) — lệnh phá huỷ hoặc câu phủ định thì dừng hỏi lại · revit · Healthcare voice safety gate

- [support-triage](recipes/support-triage/recipe.md) — phân SupportTicket vào hàng đợi · web, desktop · Support Ticket Desk

- [spec-clause-search](recipes/spec-clause-search/recipe.md) — chấm từng đoạn spec/tiêu chuẩn theo câu hỏi · ai · Question search over contract PDF

- [worker-turn-triage](recipes/worker-turn-triage/recipe.md) — lọc sơ lượt worker trước khi planner duyệt · ai · việc của chủ dự án; lỗi thật planner bắt 2026-10-05/06

Mục nào chưa có link thì công thức đó chưa có trong kit. Link demo nằm trong mục "Nguồn" của từng `recipe.md`. Danh
mục đầy đủ các demo ở https://webdevcody.github.io/jev-demos/. Sổ nghiên cứu (số đo thật, trạng thái từng công
thức) nằm ở `docs/reference/jev-ung-dung-cad-revit.md` của kho Paper-skills.
