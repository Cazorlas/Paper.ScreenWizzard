# Liên kết mở được khi trình duyệt

Luật của mục *Dừng chờ duyệt* trong `SKILL.md`, viết đủ ở đây để `SKILL.md` giữ dưới giới hạn độ dài.

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
