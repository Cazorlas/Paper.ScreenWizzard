# Quay màn hình ra MP4 (đợt 2a) — 2026-09-27

Hùng dùng bản 0.1.3 ngày 2026-09-27. Sau một lần chụp toàn màn hình, Hùng hỏi: "Cái chế độ scrren chưa cho chọn màn hình, chưa cho
scren theo vùng hay toàn màn hình", rồi "Chưa có thể hiện webcame nhỉ, rùi làm thêm mí filter icon chèn mặt dc ko". Mình trả lời
rằng ứng dụng chưa có phần quay. Hùng nói "à chưa có record", rồi "làm đợt 2".

Yêu cầu quay, Hùng đã nêu từ 2026-09-20 và ghi ở `docs/roadmap.md` (mục "Yêu cầu đã nhận cho đợt 2"), làm như FastStone Screen
Recorder:
- chọn cái để quay: một màn hình, một vùng tự kéo, một cửa sổ, hoặc toàn bộ desktop;
- tiếng hệ thống và micro, bật tắt độc lập;
- kèm con trỏ, phím tắt bắt đầu/dừng/tạm dừng, đếm ngược, ra MP4;
- webcam chồng lên video, chỉnh hình dạng khung, kéo và đổi cỡ.

Hôm nay Hùng thêm yêu cầu dán icon lên mặt (filter).

Đợt 2 lớn nên chia ba, mỗi phần là một bản phát hành để Hùng dùng thử dần:
- **2a, đợt này:** quay ra MP4.
- **2b:** webcam chồng lên video.
- **2c:** icon bám theo khuôn mặt trong khung webcam.

## Tài liệu đi kèm

- Ảnh Hùng gửi 2026-09-27: hộp "Captured" 1920 × 1080 sau một lần chụp toàn màn hình (bản 0.1.3). Ảnh chỉ ở trong cuộc trò chuyện,
  không có file để chép vào `input/`.
- Ảnh FastStone Screen Recorder Hùng gửi 2026-09-20: nội dung đã ghi ở `docs/roadmap.md`.

## SPEC.md đổi ở đâu

- SPEC mới `docs/features/recorder/SPEC.md`, viết hai ngôn ngữ. Có:
  - các mục chung: User story, What the user does, Inputs, Outputs;
  - các nhóm luật: "Quay cái gì", "Tiếng", "Tạm dừng và dừng", "File";
  - F1 tới F8.
- SPEC khung ứng dụng (`shell`) sẽ đổi ở bước làm: menu khay có "Quay màn hình…", thanh chụp có nút Quay, Cài đặt có nhóm quay.
  Các chỗ này được mô tả trong SPEC quay, và plan có một task để ghi chúng vào SPEC shell.

## Được đụng tới trong đợt này

- Máy của Hùng: test quay thật (E2E) ghi file vào thư mục tạm rồi xoá. Không đụng thư mục Video thật.
- CI chỉ dựng và chạy unit; runner không có màn hình để quay.

---

Luật: [SPEC.md](SPEC.md) · Việc làm và bằng chứng: [2026-09-27-quay-man-hinh-plan.md](2026-09-27-quay-man-hinh-plan.md)
