# chatbot-confirm-gate — lệnh phá huỷ hoặc câu phủ định thì dừng hỏi lại

## Khi nào dùng

Một chatbot trong add-in Revit gửi câu của người dùng cho một mô hình chạy tool trên model đang mở. Lệnh xoá hay
đổi phần tử đã có, hoặc câu có phủ định hay ngoại trừ mà một bộ đọc từ khoá có thể đảo nghĩa ("ẩn hết ống gió, chỉ để
lại ống nước"), phải dừng lại hỏi người dùng trước khi gửi. Cổng này chỉ đọc chữ của câu lệnh: nó không lập kế hoạch,
không chọn tool, không đụng model.

## State gửi Jev

Một dòng cho mỗi tin nhắn: `request: <câu như người dùng gõ> | folded: <câu đó đã bỏ dấu>`. Bản bỏ dấu là chữ thường,
bỏ dấu tiếng Việt, `đ` thành `d`, mọi chuỗi không phải chữ hay số thành một dấu cách. Gửi cả hai bản vì người dùng gõ
lẫn có dấu và không dấu.

Không nằm trong state đã đo: lịch sử hội thoại, câu trả lời trước của chatbot, tên dự án, đường dẫn file, id phần tử, tên người
dùng, nội dung hay số lượng phần tử đang chọn.

Hai nhãn `request` và `folded` không bao giờ là từ khoá của luật: luật khớp trên cả dòng state.

## Câu hỏi

`questions.json` có ba câu `choice`:

- `destructive`: 2 lựa chọn (`yes` / `no`). `yes` nghĩa là xoá **hoặc đổi** phần tử đã có: dời, xoay, đổi type, đặt
  parameter, đánh số, làm lại đều là `yes`. Chỉ đổi cách view hiển thị (ẩn, bỏ ẩn, isolate, override), tạo mới, đọc,
  hỏi cách làm, mở tool, hay cấm mọi thay đổi là `no`.
- `has_negation_or_exception`: 2 lựa chọn (`yes` / `no`). `yes` khi câu loại trừ một phần (trừ, ngoại trừ, except,
  keep only) hay cấm chính hành động của nó (đừng, không được, don't). Từ phủ định chỉ mô tả phần tử, `không` cuối
  câu hỏi có/không, hay dấu trừ số học thì là `no`.
- `scope`: 5 lựa chọn (`selection`, `view`, `level`, `whole model`, `not stated`). `not stated` dùng khi câu không nêu
  chỗ nào và hành động không ngầm chỉ một chỗ, hoặc không phải lệnh (hỏi cách làm, mở tool, chào hỏi). Nêu nhiều chỗ thì
  chỗ hẹp nhất thắng: selection → view → level → whole model. Chỗ bị loại trừ ("trừ tầng 1") không phải phạm vi.

## Luật trước và sau Jev

- **Trước Jev:** `rules.json`. Luật khớp từ khoá trên chữ thường đã bỏ dấu, luật đầu tiên khớp thì thắng. Lệnh gạch
  chéo và câu rỗng đi thẳng, cố định trong sản phẩm, nên không có trong fixture.
- **Sau Jev:**
  - (a) Jev chỉ gỡ được một `yes` của luật (`destructive` hay `has_negation_or_exception`) khi độ tin ≥ 0.90; dưới đó
    giữ `yes`. Cổng an toàn: sai thì sai về phía hỏi thừa.
  - (b) Jev trả `no` với độ tin < 0.70 thì coi là `yes`: ở cổng này "cần xem" nghĩa là hỏi lại người dùng.
  - (c) `scope` là `selection` mà lúc gửi không có phần tử nào đang chọn (đếm tại máy, không gửi) thì thành
    `not stated`.
- **Cổng:** hỏi lại khi `destructive` hay `has_negation_or_exception` là `yes`, còn lại gửi luôn. Câu hỏi lại là mẫu
  cố định (Jev không viết chữ) nêu lý do và phạm vi, với hai nút Gửi / Sửa lại. Ngưỡng 0.90 là mặc định chưa đo: sản
  phẩm đo nó.

## Port

`IConfirmGateJudge.JudgeAsync(ConfirmGateFacts) → ConfirmGateJudgement`. `ConfirmGateFacts(Request, Folded)` chỉ
chứa hai trường ở mục State. `ConfirmGatePolicy.Decide(ruleAnswer, judgement, selectedCount)` trả `Proceed` hoặc
`Ask(reasons, scope)`. Công tắc tắt là `PAPER_CHATBOT_CONFIRM_JEV=0` (Jev bật mặc định); `JevSwitchPolicy` chọn đường gọi; Jev quá 2 s thì lấy
luật. Cổng nối ở đầu `SendAsync` của interactor chat, trước khi xếp hàng hay gửi. Unit test dùng judge giả. Khi công
tắc bật, mỗi tin nhắn chậm thêm 0.4–0.7 s.

## Đo

Fixture có 50 dòng bịa: 30 dòng lệnh thường (tiếng Việt có dấu, không dấu và tiếng Anh) và 20 dòng khó theo các lớp
lỗi: động từ xoá hay đổi ngầm (làm lại, kéo dài, clean up), động từ bị cấm, ngoại trừ trên lệnh phá huỷ và trên lệnh
ẩn, `không` không phải phủ định, `trừ` số học, bỏ ẩn, reset override, câu hỏi cách làm, tên tool chứa động từ, view là
đối tượng chứ không là chỗ, chỗ bị loại trừ.

`rules: 36/50 rows, hard 6/20`
`jev (typesafe/jev-1.13, 2026-10-01): 47/50 rows, hard 18/20`

Một lần chạy qua OpenRouter trên fixture bịa; câu Jev không trả lời thì lấy của luật; chưa áp luật (a)–(c). Dòng khó
Jev vẫn sai: CG35, CG40. Theo từng câu: `destructive` 49/50, `has_negation_or_exception` 50/50, `scope` 48/50, nên mỗi
dòng sai chỉ sai một câu, và câu sai là `destructive` hoặc `scope`:

- CG35 "dung xoa gi het, chi dem cua tang 2 thoi" (đúng: `no`, `yes`, `level`).
- CG40 "xoá hết tag trong cả dự án, trừ tầng 1" (đúng: `yes`, `yes`, `whole model`).
- Một dòng thường cũng sai. Script không giữ câu trả lời từng dòng, nên dòng nào và câu nào chưa biết.

Câu phủ định và ngoại trừ Jev đúng cả 50 dòng; luật đúng 47.

Luật và fixture cùng một người viết: đây là số dev. 266 câu thật trong log của chatbot chỉ chấm bằng luật, không gửi
đi.

## Nguồn

- Demo truyền cảm hứng, cổng an toàn giọng nói trong y tế: https://x.com/bhatsy/status/2102038708379603404
- Việc của chủ dự án, 2026-10-01; lỗi phủ định đo 2026-09-29 trên tám câu lệnh Revit tiếng Việt. Nguồn cụ thể ghi ở
  sổ nghiên cứu của kho kit.
