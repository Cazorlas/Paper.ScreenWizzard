# Báo bản mới — 2026-09-26

Sau khi bản 0.1.2 được phát hành, Hùng hỏi: "nếu có bản update mới thì có tự động biết để báo user cài ko", kèm "ok làm nốt lun đi".
Tới bản 0.1.2, ứng dụng không biết có bản mới. Người dùng phải tự vào trang GitHub xem.

Muốn:
- Ứng dụng tự hỏi GitHub xem có bản mới hơn không. Có thì một thông báo của Windows nói rõ bản nào, và bấm vào thì mở trang tải.
- Thông báo tắt rồi thì trong menu khay vẫn còn dòng "Tải bản mới…".
- Ai không muốn ứng dụng hỏi mạng thì tắt được trong Cài đặt.
- Ứng dụng không tự tải và không tự cài. File cài chưa ký số, và tự cài là một quyết định riêng (một ADR), chưa làm.

## Tài liệu đi kèm

- Trang phát hành: https://github.com/Cazorlas/Paper.ScreenWizzard/releases (kho công khai; `GET /repos/.../releases/latest` trả 200
  khi không đăng nhập, đo từ phiên cloud 2026-09-26).

## SPEC.md đổi ở đâu

- **What the user does:** bước 2 (dòng "Tải bản mới…" ở menu khay), bước 4 (ô kiểm bản mới).
- **Inputs:** dòng "Kiểm bản mới, bật".
- **Nhóm luật mới "Báo bản mới":** sáu dòng.
- **When it does not do the job:** F9 (không có trả lời thì không hiện gì), F10 (không mở được trang thì báo kèm địa chỉ).
- **Assumptions:** kho công khai; chỉ gửi tên ứng dụng.
- **Clarifications:** câu hỏi của Hùng.
- **What it does not do yet:** "cập nhật tự động" đổi thành "tự tải và tự cài".
- Hai ngôn ngữ đổi cùng lúc.

## Được đụng tới trong đợt này

- Mạng: ứng dụng gọi `api.github.com` (chỉ đọc). Test không gọi mạng: unit dùng port giả, và E2E ghi `checkForUpdates: false` vào
  cài đặt gieo sẵn.

---

Luật: [SPEC.md](SPEC.md) · Việc làm và bằng chứng: [2026-09-26-bao-ban-moi-plan.md](2026-09-26-bao-ban-moi-plan.md)
