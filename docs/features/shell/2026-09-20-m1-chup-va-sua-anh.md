# Làm ứng dụng chụp màn hình và sửa ảnh của mình, đợt 1 — 2026-09-20

Hùng muốn một ứng dụng desktop của riêng mình, tên Paper.ScreenWizzard, làm được việc như FastStone:
chụp màn hình theo bốn kiểu (vùng chữ nhật, vùng tự do, một cửa sổ, cả màn hình), chụp xong sửa được ảnh (vẽ thêm,
bớt), sau đó quay được màn hình, và thêm phần ghi chú lên cửa sổ như EpicPen. Ông muốn áp dụng bộ skill Paper-skills
ngay từ đầu để cùng một vòng đời làm việc.

Việc này rất lớn, nên chia đợt. **Đợt 1 (brief này): khung ứng dụng, chụp bốn kiểu, sửa ảnh.** Quay màn hình và
ghi chú lên màn hình để đợt 2 và 3 (xem lộ trình), mỗi đợt viết SPEC riêng khi tới lượt.

## Tài liệu đi kèm

- Hai kho tham khảo Hùng đưa: PaperTodo (ứng dụng WPF, ghi chú dán lên desktop, có tài liệu kiến trúc riêng) và
  Fluent-Screen-Recorder (quay màn hình bằng cách quay có sẵn của Windows rồi nén thành video). Đã đọc cấu trúc, ghi
  vào lộ trình cho đợt 2 và 3; đợt 1 không dùng mã của chúng.
- Ứng dụng tham chiếu về cách dùng: FastStone Capture, EpicPen. Không có tệp nào chép vào thư mục input.
- Thư mục dự án trống, chỉ có LICENSE và README của kho mới tạo.

## SPEC.md đổi ở đâu

- **Khung ứng dụng** (mới, thư mục shell): khay hệ thống, thanh chụp, phím tắt, cài đặt được giữ lại, chỉ một bản
  chạy, khởi động cùng Windows, ngôn ngữ và giao diện.
- **Chụp màn hình** (mới, thư mục capture): bốn kiểu chụp, ảnh đóng băng, đúng pixel thật, độ trễ, con trỏ, nơi nhận
  ảnh, đặt tên file.
- **Sửa ảnh** (mới, thư mục editor): công cụ vẽ, lịch sử, chữ tiếng Việt, số bước, làm mờ che thông tin, cắt, lưu và
  chép.

## Được đụng tới trong đợt này

- Chỉ thư mục dự án này. Trong lúc thử thật, ứng dụng được phép chụp màn hình đang mở, ghi file ảnh vào một thư mục
  tạm của phiên thử (dọn sau khi thử), ghi mục khởi động của người dùng hiện tại rồi gỡ đi, và giữ phím tắt trong
  lúc chạy. Không đụng model Revit hay bản vẽ nào, dù máy có Revit mở.
- Kho `Cazorlas/Paper.ScreenWizzard` đã có; commit và đẩy được, theo thẩm quyền đã có với các kho Paper.

---

Luật: [../capture/SPEC.md](../capture/SPEC.md) · [../editor/SPEC.md](../editor/SPEC.md) · [SPEC.md](SPEC.md) · Việc làm và bằng chứng: [2026-09-20-m1-chup-va-sua-anh-plan.md](2026-09-20-m1-chup-va-sua-anh-plan.md)
