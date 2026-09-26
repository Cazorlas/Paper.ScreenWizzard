# Sổ quyết định kiến trúc (ADR)

Mỗi file là **một** quyết định kiến trúc tại một thời điểm, kèm các phương án đã cân nhắc và lý do chọn.
Hành vi hiện tại của một tính năng không nằm ở đây — nó nằm trong `SPEC.md` của tính năng, và được viết
lại mỗi lần tính năng đổi. ADR thì không bao giờ viết lại: trộn hai thứ vào một file thì lần refactor đầu
tiên xoá mất lý do.

Skill `adr` viết ADR mới theo đúng các luật dưới đây.

## Mục lục

| # | Quyết định | Status |
| --- | --- | --- |
| [0001](0001-clean-architecture.md) | Quyết định của tính năng nằm trong tầng không dùng host; host đi qua cổng với dữ liệu thuần | Proposed |
| [0002](0002-file-cai-setup-exe-inno.md) | File cài là Setup.exe theo người dùng, dựng bằng Inno Setup | Proposed |
| [0003](0003-thu-muc-cap-mot-la-domain.md) | Trong mỗi tầng, thư mục cấp một là domain; vai là cấp hai | Proposed |

Thêm một dòng mỗi khi có ADR mới; khi một ADR bị thay thế, sửa cột Status của nó.

## Khi nào viết

Phải đủ **cả ba** điều kiện. Hai trên ba là chưa đủ.

1. **Khó đảo ngược** — đổi ý về sau tốn công thật (dời code giữa project, đổi hướng tham chiếu, đổi định
   dạng dữ liệu đã lưu).
2. **Gây bất ngờ nếu thiếu ngữ cảnh** — người đọc code sau này sẽ hỏi "sao lại làm thế này?" và muốn "sửa".
3. **Có đánh đổi thật** — có phương án khác đáng cân nhắc, và phương án được chọn vì lý do cụ thể.

Dễ đảo ngược thì đừng viết — cứ đảo. Không gây bất ngờ thì không ai hỏi. Không có phương án nào khác thì
chẳng có gì để ghi ngoài "làm điều hiển nhiên".

## Đánh số và tên file

- `NNNN-ten-quyet-dinh.md`: bốn chữ số, tăng dần, không dùng lại số của ADR đã bị thay thế.
- Tên file và tiêu đề nói **quyết định**, như một mệnh đề — không phải một chủ đề. "Tầng tách theo thư mục
  trong module" chứ không phải "Về tầng".

## Khuôn

Một đoạn văn là một ADR hợp lệ khi quyết định nhỏ. Khi cần đủ phần:

```markdown
# ADR-NNNN: <quyết định, viết thành một mệnh đề>

## Status

Proposed | Accepted | Superseded by ADR-NNNN

## Date

YYYY-MM-DD

## Context

## Decision

## Alternatives Considered

## Consequences
```

`Alternatives Considered` chỉ ghi phương án đáng nhớ; `Consequences` chỉ ghi hệ quả không hiển nhiên.

## Không sửa — thay thế

- Một ADR đã `Accepted` thì **không sửa nội dung**. Đổi ý là viết ADR mới, trong `Context` nói nó thay
  ADR nào và vì sao.
- Thay đổi duy nhất được phép trên ADR cũ là dòng Status: `Superseded by ADR-NNNN`, và cột Status trong
  mục lục.
- ADR còn `Proposed` thì sửa thoải mái — nó chưa là quyết định.
- `CLAUDE.md` của dự án trỏ tới số ADR ở chỗ luật đó áp dụng (ví dụ bảng tầng), không chép nội dung ADR.
