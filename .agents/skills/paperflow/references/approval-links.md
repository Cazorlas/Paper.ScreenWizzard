# Liên kết mở được khi mời người dùng đọc

Luật 9 của `SKILL.md`, viết đủ ở đây để `SKILL.md` giữ dưới giới hạn độ dài.

**Áp cho mọi lần mời đọc, không riêng lúc xin duyệt.** "Xem REFERENCE có số đo rồi", "luật mới nằm ở
SPEC", "bằng chứng ghi trong plan", "đọc lại giúp mình mục 3" — tất cả đều là một lần bảo người dùng mở
một file. Bảo họ mở file thì đưa họ đường dẫn bấm được; nêu tên suông là bắt họ đi tìm trong một repo họ
không mở sẵn. Lúc xin duyệt chỉ là trường hợp nghiêm ngặt nhất của cùng một luật.

**Mỗi lần trình duyệt kèm liên kết mở được, không trừ lần nào.** Lần đầu, lần duyệt lại sau khi sửa, lần thứ
ba và mọi lần sau, lời trình luôn có **một dòng liên kết markdown cho từng tài liệu đang chờ duyệt**:

- `SPEC.md` (mỗi tính năng bị chạm một dòng), brief và plan; plan `model` thêm RULE, SAMPLE, REFERENCE nếu việc
  này chạm tới; `/task-bug` thêm file bug.
- Dạng `[tên hiển thị](đường dẫn từ gốc dự án)` để bấm là mở trong editor. Đường dẫn tương đối, có `#L<dòng>`
  khi trỏ vào một dòng luật hay một task.
- Tài liệu bị đổi ở lần này ghi thêm chữ *đổi* cạnh liên kết của nó, để người duyệt biết mở cái nào trước.

**Không** viết "như lần trước", **không** chỉ nêu tên file, **không** gom nhiều tài liệu vào một liên kết:
người duyệt không phải đi tìm và không phải nhớ lần trước nằm ở đâu. Một lần trình lại sau khi sửa vẫn phải có
đủ liên kết, kể cả tài liệu không đổi.

Mọi skill dừng chờ duyệt (`task-spec`, `spec`, `task-bug`, `model-task`, `task-do` khi spec gap) tự nói lại luật
này bằng một câu, vì một skill được đọc một mình.

## Ngoài lúc duyệt

Cùng dạng liên kết, nhẹ hơn một bậc: **một dòng cho mỗi file được nhắc tới trong lượt đó**, không cần cụm
"chờ duyệt" và không cần nhắc lại file không đổi.

- **Báo cáo chặng 7**: mọi artifact đã ghi trong lượt — SPEC, plan, REFERENCE, RULE, CODEMAP, file bug —
  mỗi cái một liên kết. Người đọc báo cáo là người sẽ mở chúng.
- **Nhắc tới một dòng luật, một task, một hàng bằng chứng**: liên kết có `#L<dòng>`, vì "trong REFERENCE"
  của một file 700 dòng vẫn là đi tìm.
- **Trả lời một câu hỏi bằng một thứ đã ghi sẵn**: liên kết tới chỗ đã ghi, đừng chép lại nội dung rồi
  để người dùng tự đoán nó nằm đâu.

Không áp cho: file chỉ đọc để hiểu mà người dùng không cần mở, và code đã hiện nguyên trong lượt.

## Bấm được là điều kiện, không phải mong muốn

Một liên kết đúng dạng mà **bấm không mở** thì không tính là đã tuân luật này. Nó còn tệ hơn nêu tên
suông: nêu tên suông thì người dùng biết phải tự tìm, còn một liên kết hỏng trông y hệt một liên kết
tốt, và người duyệt chỉ phát hiện lúc đã bấm.

**Đã đo, 2026-09-20.** Một kho có **dấu cách trong đường dẫn**. Liên kết được gửi với dấu cách mã hoá
thành `%20` — người dùng bấm, **không mở**. Suốt một phiên dài mọi liên kết đều mang lỗi đó và không có
gì báo cho tới khi người dùng lên tiếng.

Nên:

- **Đường dẫn có dấu cách thì bọc destination trong dấu ngoặc nhọn**, đúng chuẩn markdown:
  `[tên](<thu muc co dau cach/docs/SPEC.md#L180>)`. Mã hoá `%20` **đã đo là hỏng** trên client đó.
- **Đường dẫn tính từ gốc thư mục làm việc của phiên** — không phải từ thư mục con đang đứng, không
  phải đường dẫn tuyệt đối.
- **Lần đầu gửi liên kết trong một kho có dấu cách hay ký tự lạ trong đường dẫn, hỏi một câu** "bấm mở
  được không?", rồi ghi câu trả lời vào `REFERENCE.md` của kho đó. Một câu hỏi rẻ hơn cả một phiên gửi
  liên kết hỏng.
- **Số dòng phải đo, đừng nhớ.** `#L<dòng>` lấy bằng `grep -n` ngay trước khi gửi; file vừa sửa trong
  cùng lượt thì số dòng cũ đã sai.

Hai điều trên được viết ra vì `payload-contract` bắt được chính đoạn này: bản nháp đầu của nó **chứa một
liên kết hỏng làm ví dụ**, và luật F14 ("mọi đường dẫn trong payload phải resolve") chặn lại. Ví dụ trong
tài liệu phải để trong dấu nháy ngược, không để thành liên kết thật.
