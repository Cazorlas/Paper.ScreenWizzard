# Xếp lại thư mục mã nguồn theo hình mới của kit, không đổi hành vi — 2026-09-26

Hùng yêu cầu ngày 2026-09-26: "refactor lại theo adr paperflow mới nha". Bộ kit vừa lên bản 1.32.0 với luật mới
về thư mục (ADR-0019 của kit): trong mỗi tầng, thư mục cấp một là một vùng bài toán (chụp, sửa ảnh, khung ứng
dụng, phần dùng chung), còn vai của file (hợp đồng, nơi quyết định, dữ liệu) là cấp hai. Mã nguồn đợt 1 viết theo
hình cũ, nên công cụ review kiến trúc của kit giờ báo lỗi trên cả ba tính năng.

Vì sao làm ngay: đợt 2 (quay màn hình) sắp thêm một vùng thứ tư. Chuyển trước thì đợt 2 viết thẳng theo hình mới,
và chỉ có một hình sống trong kho.

Người dùng **không thấy gì đổi**: mọi cửa sổ, phím tắt, file lưu, cài đặt và thông báo giữ nguyên. Đây là refactor
thuần.

Việc sửa cách đọc cài đặt (trường thiếu lấy mặc định), mình đề xuất trước đó, **tạm dừng** tới khi việc này xong,
vì hai việc sửa cùng các file.

## Tài liệu đi kèm

- Luật mới của kit: skill `clean-architecture` mục "Hình dạng", test mẫu ở
  `clean-architecture/references/architecture-tests-csharp.md` mục 5 và 6, agent `architecture-reviewer` mục 10.
- Quyết định của dự án: ADR-0003 (`docs/decisions/0003-thu-muc-cap-mot-la-domain.md`), chờ Hùng duyệt.

## SPEC.md đổi ở đâu

- Không mục nào. Refactor không thêm, bớt hay đổi dòng nghiệm thu nào của khung, chụp, sửa ảnh hay file cài.

## Được đụng tới trong đợt này

- Mã nguồn và test trong `src/` và `tests/`, `CLAUDE.md`, `CODEMAP.md` của từng project, sổ ADR.
- Không đụng cài đặt, file ảnh hay mục khởi động của Hùng. Bộ ui và e2e chạy trên máy Hùng, với biến môi trường
  test như mọi lần.

---

Luật: [SPEC.md](SPEC.md) · Việc làm và bằng chứng: [2026-09-26-thu-muc-theo-domain-plan.md](2026-09-26-thu-muc-theo-domain-plan.md)
