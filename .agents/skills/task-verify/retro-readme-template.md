# Retro — hàng đợi bài học, chờ chủ dự án xét

<!--
Khuôn này do skill `task-verify` sở hữu. Chép thành `<docs>/retro/README.md` (thư mục cha của
`<featureDocs>`) lần đầu /task-verify có bài học để ghi; từ đó file thuộc về dự án. Xoá khối chú thích này.
-->

Đây là **hộp thư đến**, không phải kho luật. Agent ghi vào đây điều nó **phát hiện** khi chạy
`/task-verify`; chỉ điều chủ dự án **nhận** mới được chép vào nhà của nó và trở thành luật phải theo.

```text
/task-verify phát hiện → dòng `chờ xét` → /sync-docs trình chủ dự án → nhận: chép vào nhà → `đã nhận → <file>`
                                                                     → gạt:             → `không nhận — <lý do>`
```

**Tồn dư không phải lỗi.** Một dự án sinh ra nhiều bài học hơn số bài đáng thành luật. Cái sai chỉ là bài
học **không được ghi**, hoặc **đã nhận mà không ai chép** vào nhà của nó.

## Status — mỗi dòng đúng một cái

| Status | Nghĩa | Ai đặt |
|---|---|---|
| `chờ xét` | mới ghi, chưa ai xem | agent, ở `/task-verify` |
| `đã nhận → <file>` | chủ dự án nhận; đã chép vào `<file>` | agent, ở `/sync-docs`, sau khi chủ dự án gật |
| `không nhận — <lý do>` | chủ dự án đã xem và gạt | agent, ở `/sync-docs`, sau khi chủ dự án gạt |

Dòng đã xử lý (`đã nhận`, `không nhận`) **gạch ngang** cả dòng bằng `~~ ~~`, **không bao giờ xoá**: đó là
cách duy nhất để lần sau biết bài học này đã có nhà, và bối cảnh phát hiện ra nó vẫn còn.

**Nhà đề xuất** là một trong: dòng luật trong `CLAUDE.md`, dòng trong `SPEC.md` của feature, comment ở
code (`file:line`), hoặc một skill. `/task-spec` và khảo sát của `/task-do` đọc các dòng `đã nhận` chạm tới
việc của chúng; dòng `chờ xét` chưa ràng buộc ai.

## Hàng đợi

| Ngày | Phát hiện | Nhà đề xuất | Status |
|---|---|---|---|
| ~~YYYY-MM-DD~~ | ~~<ví dụ: điều đã học, kèm số đo và việc làm lộ ra nó>~~ | ~~`CLAUDE.md`~~ | ~~`đã nhận → CLAUDE.md`~~ |
