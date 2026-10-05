# Planner và worker

Planner là phiên chính: session chính lập plan, viết test đỏ, duyệt từng task và rà cuối; không giao planner cho subagent. Việc chỉ có hai vai - planner (phiên chính) và worker (Codex hoặc Claude Sonnet).

## Planner viết test đỏ

Planner viết test đỏ trước khi giao worker: mọi test quyết định task xong và dữ liệu vào của nó, chạy một lần, ghi dòng đỏ vào plan, commit trên nhánh của việc.

Profile khai `live.loop`: test đỏ viết từ dòng baseline của task `live` trước nó.

Worker không sửa, không nới, không xoá test của planner.

Không có dòng baseline thì không viết test: hỏi task `live` đo trước.

## Worker theo lane

Lane `unit`: Codex qua collab - session chính restore trước (.NET `dotnet restore`, Python `uv sync`) trong thư mục làm việc của lần cộng tác, lệnh test của brief chạy không mạng (.NET `--no-restore`); Codex hết usage thì Claude Sonnet làm thay; máy không có Codex hay collab: `lane-logic`.

Lane `ui`, `e2e`, `live`, `model`: Claude Sonnet - `lane-ui`, `lane-live`, `lane-model`.

Đo cần mạng (Jev, OpenRouter, host `ai`) không giao Codex: Claude hay session chính.

## Planner duyệt và câu hỏi

Planner duyệt từng task: đọc phần đổi, chỉ chạy test của task đó, kiểm file nằm trong `{files:}`; đạt thì tick kèm dòng bằng chứng của planner, không đạt thì viết một test đỏ mới và trả task về worker.

Worker hỏi thì planner quyết ngay: lane agent trả `not verifiable (brief-lacks: <câu hỏi>)`, Codex dừng ở `ADVICE_REQUEST`; planner trả một quyết định, lý do, bước đầu, ghi vào Decisions, rồi giao lại.

## Rà cuối

Không chạy lại toàn bộ test: hết mọi lane, session chính chạy verb `build` một lần; test của mọi task chạy ở rà cuối (`/task-verify`).

Một worker hỏi thì câu hỏi đi tới planner ngay; nếu không tới được planner thì đi qua `main`.

