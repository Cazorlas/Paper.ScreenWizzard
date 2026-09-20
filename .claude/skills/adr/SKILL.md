---
name: adr
description: Record an architecture decision as an ADR in the project's decision log - only when it is hard to reverse, surprising without context and a real trade-off - numbered next in the log, never edited once accepted but superseded by a new ADR, and pointed to from the project's CLAUDE.md where the rule applies. Use when a task settles where layers, projects or module boundaries go, when a reviewer or a reader would "fix" something deliberate, or when an accepted decision is being changed.
---

# ADR — quyết định kiến trúc có ghi chép

**Mục tiêu một câu:** người đọc code sau này thấy một điều bất thường, và tìm được **vì sao** nó được chọn
cùng những gì đã bị loại — mà không ai phải nhớ.

## Sổ nằm ở đâu

Theo thứ tự, lấy chỗ đầu tiên có file `.md`:

1. `adrFolder` trong `.claude/paper.profile.json`, nếu dự án khai.
2. `docs/decisions/`
3. `docs/adr/`

Không chỗ nào có thì setup của paper-kit đã tạo `docs/decisions/README.md` và
`docs/decisions/0001-clean-architecture.md` — từ đó chúng là của dự án, setup không đụng lại. Dự án giữ ADR
ở chỗ khác thì dùng chỗ đó; không mở sổ thứ hai.

## Khi nào viết

Phải đủ **cả ba**. Hai trên ba là không viết.

| Điều kiện | Hỏi |
|---|---|
| **Khó đảo ngược** | đổi ý về sau có tốn công thật không — dời code giữa project, đổi hướng tham chiếu, đổi dữ liệu đã lưu? |
| **Bất ngờ nếu thiếu ngữ cảnh** | người đọc có muốn "sửa" nó không? |
| **Đánh đổi thật** | có phương án khác đáng cân nhắc, và lý do cụ thể để loại nó không? |

Không viết ADR cho:

- **Hành vi của tính năng** — cái đó là `SPEC.md` của tính năng, được viết lại mỗi lần tính năng đổi.
- **Luật code hằng ngày** — cái đó là `CLAUDE.md` hay quy ước của dự án.
- **Một lựa chọn dễ đảo** — cứ đảo khi cần.

## Viết

1. **Số:** lớn nhất trong sổ cộng một, bốn chữ số. Không dùng lại số của ADR đã bị thay.
2. **Tên file:** `NNNN-quyet-dinh-viet-thuong.md`; tiêu đề nói **quyết định** như một mệnh đề, không phải
   chủ đề.
3. **Khuôn** — một đoạn văn là đủ khi quyết định nhỏ; khi cần đủ phần:

   ```markdown
   # ADR-NNNN: quyết định, viết thành một mệnh đề

   ## Status
   Proposed

   ## Date
   YYYY-MM-DD

   ## Context
   Vấn đề thấy được hay đo được. Thay ADR cũ thì: "Thay ADR-NNNN vì ...".

   ## Decision
   1. Mỗi ý kiểm được - một test, một hook hay một reviewer giữ nó.

   ## Alternatives Considered
   ### Phương án
   - Ưu điểm / Nhược điểm / Từ chối vì

   ## Consequences
   - Hệ quả không hiển nhiên: cái gì giờ không còn được máy giữ, khi nào xem lại.
   ```

4. **Số đo thắng ý kiến.** Phương án bị loại vì chi phí thì ghi chi phí đo được (số project, số file một
   thay đổi chạm, số interface chỉ có một cài đặt), không ghi "phức tạp hơn".
5. **Mục lục:** thêm một dòng vào `README.md` của sổ.
6. **Status `Proposed`** tới khi chủ dự án duyệt; agent không tự đổi thành `Accepted` — kể cả ADR khởi đầu
   của setup, và kể cả khi chủ dự án vừa duyệt một plan nhắc tới nó. Duyệt plan không phải duyệt ADR: hỏi một
   câu riêng, kèm liên kết mở được tới ADR.
7. **Làm đúng phương án đã chọn.** Một ADR chọn "phương án X" thì code, `CLAUDE.md`, hook và test kiến trúc
   theo đúng hình của X (chọn "tầng là project" thì Presentation cũng là project). Muốn lệch — gộp hai tầng,
   hạ một tầng thành thư mục — thì **ghi ngoại lệ thành một dòng Decision riêng** có lý do đo được và điều kiện
   bỏ nó, và chủ dự án duyệt trước khi code lệch. Lệch mà không ghi là cách một ADR im lặng mất nghĩa.

## Không sửa — thay thế

- ADR đã `Accepted` **không sửa nội dung**, kể cả sửa lỗi chính tả làm đổi nghĩa. Đổi ý là **ADR mới**: nói
  trong `Context` nó thay ADR nào và vì sao, kèm số đo đã đổi.
- Trên ADR cũ chỉ đổi đúng dòng Status thành `Superseded by ADR-NNNN`, và cột Status trong mục lục.
- ADR còn `Proposed` thì sửa thoải mái.
- Hook hay test kiến trúc đang giữ một quyết định bị thay: đổi chúng **cùng lượt** với ADR mới, không để
  máy giữ một luật mà sổ đã bỏ.

## Trỏ từ CLAUDE.md

Luật mà agent phải theo nằm trong `CLAUDE.md`; lý do nằm trong ADR. Nối chúng bằng **một con trỏ**, không
chép nội dung:

```markdown
| Tầng | Thư mục / project | Được tham chiếu | ADR |
|---|---|---|---|
| UseCases | UseCases/Feature/ | Domain | 0001 |
```

- Mỗi hàng của bảng tầng trỏ tới số ADR đặt ra luật đó.
- ADR bị thay: sửa con trỏ sang ADR mới cùng lượt.
- Người đọc `CLAUDE.md` thấy luật; ai muốn "sửa" luật thì theo số mà đọc lý do trước.

## ADR khởi đầu

`docs/decisions/0001-clean-architecture.md` (khi setup tạo nó) để trống **một** chỗ: tầng là project
(layers as projects) hay thư mục trong module tính năng (layers as folders). Dự án chọn, ghi một câu vì sao,
rồi chủ dự án duyệt thành `Accepted`. Hình dạng code và test kiến trúc cho từng cách: skill
`clean-architecture`.
