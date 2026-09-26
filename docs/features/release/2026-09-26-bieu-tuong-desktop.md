# File cài tự tạo biểu tượng trên màn hình nền — 2026-09-26

Hùng dùng thử bản 0.1.1 ngày 2026-09-26 và hỏi "ko tạo đc shortct trong window hả bạn", rồi "tự tạo á". Cài xong, ứng dụng chỉ
có mục trong menu Start, **không có biểu tượng trên màn hình nền**. Hùng muốn file cài tự tạo biểu tượng đó.

Muốn:
- Trình cài có ô "Tạo biểu tượng trên Màn hình chính (Desktop)", **đã chọn sẵn**, nên cứ bấm tiếp là có biểu tượng.
- Ai không muốn thì bỏ chọn ô đó.
- Gỡ ứng dụng thì biểu tượng cũng mất.

## Tài liệu đi kèm

- Không có ảnh chụp. Bản dùng thử là artifact `Paper.ScreenWizzard-0.1.1` của lượt dựng tay `release` số 7 (run 36253116639).

## SPEC.md đổi ở đâu

- **What the user does:**
  - thêm bước 2: trang "Các thiết lập bổ sung" có ô biểu tượng màn hình nền, chọn sẵn;
  - bước 3 nói có biểu tượng trên màn hình nền.
- **Outputs:** sau khi cài có biểu tượng trên màn hình nền, trừ khi người dùng bỏ chọn.
- **Cài đè và gỡ:**
  - thêm nhóm luật "Có lối tắt trên màn hình nền, trừ khi người dùng không muốn" (hai dòng);
  - gỡ thì biểu tượng màn hình nền cũng không còn.
- Viết thêm phần `# English` cho cả SPEC file cài, theo luật hai ngôn ngữ của kit.

## Được đụng tới trong đợt này

- Máy của runner GitHub (script kiểm cài và gỡ thật trong thư mục thử, rồi trả máy về như cũ). Không đụng máy của Hùng.

---

Luật: [SPEC.md](SPEC.md) · Việc làm và bằng chứng: [2026-09-26-bieu-tuong-desktop-plan.md](2026-09-26-bieu-tuong-desktop-plan.md)
