# Planner và worker

Planner là phiên chính: session chính lập plan, viết test đỏ, duyệt từng task và rà cuối; không giao planner cho subagent. Việc chỉ có hai vai - planner (phiên chính) và worker (Codex hoặc Claude Sonnet).

## Planner viết hợp đồng test

Planner viết hợp đồng test trong plan trước khi giao worker: một bảng ghi mã ca, đầu vào, file test và kết quả mong đợi đến từng dòng in hoặc mã thoát.

## Lượt đỏ

Worker lượt đỏ viết mã test theo bảng hợp đồng, chạy mỗi file một lần và ghi bằng chứng đỏ; lượt này không sửa mã sản phẩm. Planner đọc diff test, kiểm đủ ca và lý do đỏ, rồi duyệt test rồi khóa chúng.

Profile khai `live.loop`: test đỏ viết từ dòng baseline của task `live` trước nó.

Worker không sửa, không nới, không xoá test đã khóa của planner.

## Lượt xanh

Worker lượt xanh ở lượt khác làm mã sản phẩm cho test xanh, không sửa file test đã khóa. Planner duyệt task sau khi lượt xanh báo xong.

Không có dòng baseline thì không viết test: hỏi task `live` đo trước.

## Worker theo lane

Mọi lane - `unit`, `ui`, `e2e`, `live`, `model` - giao worker theo thứ tự: Codex qua collab, rồi DeepSeek (dsh), rồi Claude Sonnet (`lane-ui`, `lane-live`, `lane-model`, hay `lane-logic` khi máy không có Codex hoặc collab); bỏ qua worker không chạy được phép kiểm của task. Codex và DeepSeek không mở được chương trình hay xem màn hình; Codex không có internet nhưng gọi được localhost (MCP của host đang chạy): chỉ task cần những thứ chúng thiếu mới đi tới worker kế tiếp làm được. Session chính không tự làm task: nó phân tích, giao, kiểm và chịu trách nhiệm cuối.

Trước lượt Codex, session chính restore trước (.NET `dotnet restore`, Python `uv sync`) trong thư mục làm việc của lần cộng tác; lệnh test của brief chạy không mạng (.NET `--no-restore`).

Đo cần mạng (Jev, OpenRouter, host `ai`) không giao Codex: DeepSeek hay Claude Sonnet.

## Planner duyệt và câu hỏi

Planner duyệt từng task: đọc phần đổi, chỉ chạy test của task đó, kiểm file nằm trong `{files:}`; đạt thì tick kèm dòng bằng chứng của planner, không đạt thì viết một test đỏ mới và trả task về worker.

Worker hỏi thì planner quyết ngay: lane agent trả `not verifiable (brief-lacks: <câu hỏi>)`, Codex dừng ở `ADVICE_REQUEST`; planner trả một quyết định, lý do, bước đầu, ghi vào Decisions, rồi giao lại.

## Rà cuối

Không chạy lại toàn bộ test: hết mọi lane, session chính chạy verb `build` một lần; test của mọi task chạy ở rà cuối (`/task-verify`).

Một worker hỏi thì câu hỏi đi tới planner ngay; nếu không tới được planner thì đi qua `main`.

