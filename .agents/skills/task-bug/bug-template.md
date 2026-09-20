# <PREFIX>-NNN — <điều người dùng thấy sai, một câu>

<!--
Khuôn này do skill `task-bug` sở hữu. Chép thành `<docs>/bugs/<PREFIX>-NNN-<slug>.md` lúc nhận báo lỗi,
thêm một dòng vào sổ `<docs>/bugs/README.md`, rồi xoá khối chú thích này.

  - NNN là số kế tiếp của prefix đó, ba chữ số; không dùng lại số của file nào, kể cả file `duplicate`
  - Status đổi theo /task-bug (triaging → open / not-a-bug / duplicate) và /task-verify (fixed); dòng
    Status ở đây và dòng sổ luôn khớp nhau
  - Hướng fix chỉ là đề xuất: sửa thật đi qua brief + plan được duyệt
  - file bug không bao giờ bị xoá
-->

| | |
|---|---|
| **Status** | triaging |
| **Feature** | [<feature>](../features/<slug>/SPEC.md) — dòng `F<n>` hay mục "What it does not do yet" link về đây |
| **Phát hiện** | YYYY-MM-DD, <ai báo / lúc làm việc gì> |

## Hiện tượng

<Người dùng làm gì, thấy gì, và đáng lẽ phải thấy gì — lời người dùng, số đo nếu có. Tài liệu họ đưa
nằm ở `<featureDocs>/<slug>/input/`.>

## Tái hiện

<Các bước ngắn nhất cho thấy kết quả sai, và bằng chứng đã chạy: test đỏ ở assertion, hoặc lệnh + id +
giá trị đọc lại từ host. Chưa dựng lại được thì ghi đã thử gì và đo được gì — Status giữ `triaging`.>

## Bản chất

<Vì sao sai, tới `file:line`. Code bug (phạm dòng luật nào của SPEC.md) hay spec gap (SPEC.md chưa nói).
`not-a-bug` / `duplicate` thì lý do, hoặc mã bug gốc.>

## Hướng fix (chưa làm)

<Đề xuất sửa gì, test tái hiện nào viết đỏ trước. Khi có plan sửa: link plan. Khi `fixed`: ngày và plan.>
