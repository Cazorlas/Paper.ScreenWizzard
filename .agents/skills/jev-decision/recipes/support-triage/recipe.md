# support-triage — phân SupportTicket vào hàng đợi

## Khi nào dùng

Bàn hỗ trợ của một add-in Revit và AutoCAD nhận ticket chữ tự do: khách gõ tiêu đề và một tin đầu, trộn tiếng Việt có
dấu, không dấu và tiếng Anh, thường kèm ảnh chụp màn hình. Công thức gợi ý hàng đợi (đội nào nhận) và độ gấp ngay lúc
ticket được tạo, để người trực mở ticket đã thấy nhãn. Người trực vẫn là người giao việc: nhãn chỉ là gợi ý, không tự
gửi ticket cho đội nào khi chưa đủ tin.

## State gửi Jev

Một dòng chữ, năm trường, thứ tự cố định:

`title: <tiêu đề> | text: <500 ký tự đầu của tin đầu, đã che> | app: <Revit 2024 / AutoCAD 2023 / web> | build: <bản add-in hay -> | folded: <tiêu đề và text đã bỏ dấu>`

- **Che trước, cắt sau:** che trên cả tin đầu, rồi mới cắt 500 ký tự, để một email hay một số bị cắt đôi không rò nửa
  còn lại.
- **Bảy chỗ che, chữ cố định trong ngoặc vuông:** `[email]`, `[phone]`, `[company]`, `[name]`, `[id]` (license key, mã
  số thuế, số đơn hàng, chuỗi chữ số dài), `[path]` (ổ đĩa, đường dẫn mạng, thư mục người dùng), `[add-in]` (tên thương
  hiệu của chính add-in). Email, điện thoại, id và đường dẫn che bằng mẫu. Tên người và tên công ty che bằng danh sách
  lấy từ tài khoản gửi ticket (tên hiển thị, công ty của tài khoản): một tên **không** có trong danh sách (đồng nghiệp,
  chủ đầu tư) vẫn có thể lọt qua. Đây là giới hạn đã biết của cách che này.
- `app` là app gửi ticket (Revit hay AutoCAD và năm), `build` là bản add-in; ticket gửi từ website thì
  `app: web | build: -`. `app` phân biệt hai bộ tool khi ticket không nói tool nào.
- `folded` là tiêu đề và text ở dạng chữ thường, bỏ dấu tiếng Việt, `đ` thành `d`, mọi chuỗi không phải chữ hay số
  thành một dấu cách. Gửi cả hai bản vì khách gõ lẫn có dấu và không dấu.
- **Không nằm trong state đã đo:** tên dự án, đường dẫn dự án hay file, tên view, loại view, phần tử đang chọn, log gần đây, ảnh
  và tệp đính kèm (cả số lượng), tên người gửi, id người dùng, các tin sau tin đầu, trả lời của nhân viên, độ ưu tiên
  người gửi đã chọn.
- Nhãn trường (`title`, `text`, `app`, `build`, `folded`) và chữ của chỗ che có ở mọi dòng, nên không bao giờ là từ khoá
  của luật: luật khớp trên cả dòng state. Vì vậy nhãn là `build`, không phải "add-in version".

## Câu hỏi

`questions.json` có hai câu `choice`:

- `queue`: 8 lựa chọn (`license`, `install`, `crash`, `revit tool bug`, `cad tool bug`, `feature request`, `billing`,
  `other`). Nhiều hàng đợi cùng hợp thì lấy cái đứng trước trong thứ tự license → billing → install → crash →
  revit tool bug / cad tool bug → feature request → other. Các ranh giới:
  - thông báo nói license, dùng thử, subscription → `license`, kể cả khi khách gọi là crash hay Revit tắt thật;
  - có dính tiền (giá, chuyển khoản, hoá đơn, hoàn tiền) → `billing`, kể cả đơn đã trả mà chưa nhận key: đội thu tiền
    xác nhận rồi mới cấp key;
  - add-in không vào được máy hoặc không nạp (cài, cập nhật, gỡ, bản nào hỗ trợ Revit nào, mất tab hay ribbon, lỗi lúc
    khởi động mà Revit hay AutoCAD vẫn chạy tiếp không có add-in) → `install`; cập nhật xong mà add-in chạy sai thì
    không phải `install`;
  - chính Revit hay AutoCAD đóng, treo, Not Responding, fatal error → `crash`;
  - một tool chạy mà ra sai, báo lỗi, không làm gì hay chậm hẳn đi → `revit tool bug` hay `cad tool bug`, theo tool
    ticket tả; app gửi ticket chỉ quyết khi ticket không nói;
  - tool làm đúng như được làm ra mà khách muốn thêm → `feature request`, kể cả khi ticket gọi là lỗi và đòi sửa;
  - hỏi cách dùng, ticket không nêu tool, thông báo hay triệu chứng nào (chỉ một chữ "lỗi" và ảnh), cảm ơn, spam →
    `other`.
- `urgency`: 3 lựa chọn, theo tác động lên việc của khách, không theo giọng (chữ hoa, dấu chấm than, chữ "gấp"):
  - `blocking work`: không làm việc được ngay lúc này — add-in không dùng được chút nào trên máy đó (license bị từ chối
    hay hết, mất tab, không nạp, Revit hay AutoCAD không mở hay tắt mỗi lần dùng), hoặc ticket nói việc đã dừng, có
    người đang chờ, hạn nộp hôm nay hay tuần này mà không có cách khác;
  - `annoying`: có thứ hỏng mà vẫn làm tiếp được (sửa tay, crash thỉnh thoảng, chậm, có cách vòng), và cả lỗi mà ticket
    không nêu tác động;
  - `question or request`: không có gì hỏng — hỏi cách dùng, tính năng mới, giá, thanh toán, hoá đơn, key đã trả tiền
    chưa tới, chuyển hay gia hạn license hỏi trước. Tên này thay cho `question` vì yêu cầu tính năng, báo giá, hoá đơn
    không phải câu hỏi, mà một lựa chọn tên `question` bị đọc nghĩa hẹp.

`instructions` của hai câu nêu lớp lỗi chứ không chép ví dụ: crash có thể là license, "lỗi" hay "bug" có thể là tính
năng, ticket có thể phủ định chính chữ nó dùng, tool Revit hay AutoCAD theo chữ ticket, giọng không quyết độ gấp, bỏ dấu
có thể biến từ này thành từ khác (`gặp` và `gấp` cùng thành `gap`).

## Luật trước và sau Jev

- **Trước Jev:** `rules.json`. Luật khớp từ khoá nguyên từ trên cả dòng state đã bỏ dấu, luật đầu tiên khớp thì thắng.
  Luật `queue` theo thứ tự ưu tiên ở trên, chen luật hỏi cách dùng (`other`) trước, và hai luật theo app (AutoCAD rồi
  Revit) đứng cuối: không thì mọi câu hỏi gửi từ Revit thành lỗi tool. Luật `urgency`: chữ chặn việc trước, rồi chữ báo
  hỏng, rồi chữ hỏi hay xin; mặc định `annoying` với độ tin 0.3, tức là vào hàng "cần xem".
- **Sau Jev:**
  - (a) `queue` là `feature request` hay `billing` mà `urgency` của Jev là `blocking work` hay `annoying` →
    `question or request`, nguồn `rules`: hai hàng đợi đó theo định nghĩa không có gì hỏng.
  - (b) `queue` là `crash`, `revit tool bug` hay `cad tool bug` mà `urgency` của Jev là `question or request` →
    `annoying`, nguồn `rules`.
  - (c) Độ tin `queue` dưới 0.70 → ticket vào hàng "cần xem" của người trực, không tự giao đội nào.
  - (d) `urgency` chỉ là gợi ý xếp thứ tự cho nhân viên: không ghi đè độ ưu tiên khách đã chọn, không tự gửi thông báo
    khẩn. Nhân viên đổi hàng đợi hay độ gấp thì nhãn mang nguồn `staff`.
  - (e) Jev chỉ chạy một lần khi ticket được tạo, sau khi ticket đã lưu; Jev lỗi hay quá 2 s thì lấy luật. Tạo ticket
    không bao giờ chờ Jev.

## Port

`ITicketTriageJudge.JudgeAsync(TicketTriageFacts) → TicketTriageJudgement`. `TicketTriageFacts(Title, MaskedText, App,
Build)` chỉ chứa bốn trường đã che. Một policy thuần `TicketTextMaskPolicy` trong UseCases che rồi cắt 500 ký tự
**trước** khi Facts tồn tại, nên judge không bao giờ thấy chữ chưa che; adapter dựng dòng state, kể cả `folded`.
`TicketTriageConsistencyPolicy` áp (a)–(b). Công tắc tắt là `PAPER_SUPPORT_TRIAGE_JEV=0` (Jev bật mặc định); `JevSwitchPolicy` chọn đường gọi.

Chạy ở máy chủ nhận ticket (web) hay ở bàn hỗ trợ của nhân viên (desktop), không bao giờ trong add-in của khách: khoá
Jev không đi theo bản cài. Unit test dùng judge giả. Khi công tắc bật, mỗi ticket thêm 0.4–0.7 s một lần, chạy sau khi
ticket đã lưu nên không chặn lúc tạo.

## Đo

Fixture có 45 dòng bịa: 27 dòng thường (đủ tám hàng đợi và ba độ gấp; tiếng Việt có dấu, không dấu và tiếng Anh; gửi
từ Revit, AutoCAD và web) và 18 dòng khó theo các lớp lỗi: crash mà thật ra là license, mất tab là install (có và không
có chữ tab), yêu cầu tính năng viết như lỗi, chỉ một chữ "lỗi" với ảnh, app gửi khác tool trong ticket, từ khoá license
trong ticket tiền, ticket phủ định chính từ khoá, lỗi khởi động trông như crash, treo khi chạy tool, giọng không quyết
độ gấp, cập nhật mà không phải install.

`rules: 30/45 rows, hard 3/18`
`jev (typesafe/jev-1.13, 2026-10-01): 43/45 rows, hard 16/18`

Một lần chạy qua OpenRouter trên fixture bịa; câu Jev không trả lời thì lấy của luật; chưa áp luật (a)–(e). Dòng khó
Jev vẫn sai: ST32, ST33. Theo từng câu: `queue` 45/45, `urgency` 43/45, và chỉ hai dòng sai, nên cả hai sai đúng câu
`urgency` và hàng đợi thì đúng:

- ST32 "Lỗi tool đánh số phòng" — chỉ đánh số trái sang phải, "mong sửa lỗi này" (đúng: `feature request`,
  `question or request`).
- ST33 "loi lenh xuat khoi luong" — không dấu, chỉ xuất Excel mà công ty cần CSV, "day la loi" (đúng:
  `feature request`, `question or request`).

Cả hai là yêu cầu tính năng viết như lỗi: Jev nhận đúng hàng đợi mà vẫn đọc độ gấp theo chữ "lỗi". Luật (a) đổi đúng
trường hợp này về `question or request`, nhưng số trên chưa áp nó. 45 lời gọi, 0 lỗi, 67 621 token vào, 30.1 s.

Luật, câu hỏi và fixture cùng một người viết: đây là số dev. Chưa có ticket thật nào được chấm.

## Nguồn

- Demo truyền cảm hứng, bàn phân loại ticket hỗ trợ: https://x.com/k2sbhai/status/2102053588818444542
- Việc của chủ dự án, 2026-10-01. Module ticket có sẵn mà công thức cắm vào ghi ở sổ nghiên cứu của kho kit.
