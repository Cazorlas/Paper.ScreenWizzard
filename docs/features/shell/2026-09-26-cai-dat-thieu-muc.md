# Tập tin cài đặt thiếu một mục không còn bị coi là hỏng — 2026-09-26

Hùng, 2026-09-26. Lúc xem repo, mình chỉ ra rằng một tập tin cài đặt thiếu **bất kỳ mục nào** đang bị coi là hỏng:
ứng dụng đổi nó thành `.bak`, chạy với mặc định và báo "cài đặt cũ bị hỏng". Đợt 2 (quay màn hình) sẽ thêm mục cài đặt
mới. Khi đó mọi người đang dùng `v0.1.0` mở bản mới sẽ mất hết cài đặt: phím tắt, thư mục lưu, giao diện. Hùng trả lời
"duyệt nha" cho đề xuất sửa việc này trước đợt 2, rồi "còn việc gì thì làm đi".

Muốn: thiếu một mục thì mục đó lấy mặc định, các mục khác giữ nguyên, không thông báo. Chỉ một mục **sai kiểu** hay **ngoài
miền** mới làm tập tin hỏng như trước. Mục lạ do bản mới hơn ghi thì bỏ qua, để hạ bản không làm hỏng cài đặt. Độ trễ có
giới hạn trên (10 giây, dài nhất mà cửa sổ Cài đặt cho chọn); trước đây chỉ chặn số âm.

## Tài liệu đi kèm

- Không có. Tập tin mẫu là một `settings.json` của `v0.1.0` bỏ đi một mục; test tự dựng nó.

## SPEC.md đổi ở đâu

- **Cài đặt được giữ lại:** thêm ba dòng: thiếu một mục thì lấy mặc định; một mục sai kiểu hay ngoài miền thì tập tin hỏng;
  mục lạ bị bỏ qua.
- **When it does not do the job:** F1 nói rõ "hỏng" gồm sai kiểu và ngoài miền; thêm F8 (thiếu một mục: im lặng, chỉ ghi nhật ký).
- **What it does not do yet:** bỏ dòng "một trường thiếu vẫn bị coi là hỏng".
- Viết thêm phần `# English` cho cả SPEC khung, theo luật hai ngôn ngữ của kit.

## Được đụng tới trong đợt này

- Không đụng cài đặt thật của Hùng. Test adapter chạy trong thư mục tạm; test ghép chạy với `PAPER_SCREENWIZZARD_DATA`.

---

Luật: [SPEC.md](SPEC.md) · Việc làm và bằng chứng: [2026-09-26-cai-dat-thieu-muc-plan.md](2026-09-26-cai-dat-thieu-muc-plan.md)
