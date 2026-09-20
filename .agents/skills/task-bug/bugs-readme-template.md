# Bugs — sổ báo lỗi, gồm cả cái kết luận là không phải bug

<!--
Khuôn này do skill `task-bug` sở hữu. Chép thành `<docs>/bugs/README.md` (thư mục cha của `<featureDocs>`)
lần đầu một dự án có báo lỗi; từ đó file thuộc về dự án. Xoá khối chú thích này khi chép.
-->

Mỗi báo cáo một file `<PREFIX>-NNN-<slug>.md`, viết từ `task-bug/bug-template.md`. **Không bao giờ xoá
file bug** — kể cả khi đã `fixed` hay `not-a-bug`: chi phí thật của một báo cáo là *lần thứ hai có người
báo lại nó*, và lúc đó cần đúng một chỗ để tra.

- `/task-bug` tạo file và dòng sổ lúc **nhận** báo lỗi (`triaging`), rồi đặt `open` / `not-a-bug` /
  `duplicate` sau khi dựng lại. Nó **không bao giờ** đặt `fixed`.
- Bug `open` được link từ `SPEC.md` của feature: dòng `F<n>` của bảng "When it does not do the job", hoặc
  mục "What it does not do yet".
- `/task-verify` của plan sửa bug đặt `fixed` khi mọi dòng luật của nó pass, cập nhật dòng ở sổ dưới, và
  gỡ link khỏi `SPEC.md`. File giữ lại làm lịch sử.
- Bug phát hiện ngoài phạm vi của việc đang làm cũng vào đây — **cấm sửa im lặng**.

## Status

| Status | Nghĩa |
|---|---|
| `triaging` | Đã nhận, đang dựng lại và chẩn đoán, chưa có kết luận |
| `open` | Bug thật, chưa sửa |
| `open — giữ có chủ ý` | Bug thật, chủ dự án quyết định **không** sửa; lý do ghi trong file |
| `fixed` | Đã sửa và đã kiểm; link plan sửa ghi trong file |
| `not-a-bug` | Đã chẩn đoán: hành vi của host, đúng thiết kế, hoặc người dùng hiểu sai công cụ |
| `duplicate` | Trùng một bug đã có; trỏ về mã đó |

## Prefix

Chữ cái đầu tên feature; không thuộc feature nào thì `APP-`. Đăng ký ở đây trước khi dùng để không sinh trùng.

| Prefix | Feature |
|---|---|
| `APP-` | Không thuộc feature nào — khởi động, license, menu/ribbon, localization |

## Sổ

| Mã | Mô tả một dòng | Status | Feature | Phát hiện |
|---|---|---|---|---|
