# Luật 10 đủ: effort cao để nghĩ, effort đang set để làm — model không đổi

Phần *nghĩ* — suy luận, viết `SPEC.md`, viết plan, chia task, thảo luận, phân tích, rà soát — chạy ở effort
**ít nhất `high`**. Phần *làm* — viết code, chạy task, build, test, lái host — chạy ở effort người dùng đang
set. **Model không đổi ở cả hai phần**: model người dùng chọn là model chạy từ đầu tới cuối (ADR-0026). Luật áp
cho mọi dự án, mọi host, mọi loại việc (`code` và `model`).

## 1. Effort của phần nghĩ

Thang effort: `low` < `medium` < `high` < `xhigh` < `max`.

| Effort đang set | Phần nghĩ chạy ở |
| --- | --- |
| `low`, `medium`, hay không biết | `high` |
| `high` | `xhigh` — cao hơn một nấc |
| `xhigh`, `max` | giữ nguyên — đã cao hơn `high` |

Không bao giờ hạ effort cho phần nghĩ, và không bao giờ đổi model để thay cho việc nâng effort. Lý do đổi từ
"model mạnh nhất" sang effort: model mạnh đốt token gấp nhiều lần cho cùng một spec, còn effort cao trên model
đang chạy đủ cho phần nghĩ với giá thấp hơn nhiều.

## 2. Session chính không tự đổi effort của chính nó

Không có công cụ nào cho nó làm việc đó. Nó **xin một câu**, đúng hai chỗ chuyển tiếp:

| Chỗ | Xin gì |
| --- | --- |
| trước chặng 1 | bắt đầu phần nghĩ — đề nghị `/effort <mức ở bảng mục 1>` |
| trước chặng 4 | bắt đầu phần làm — đề nghị `/effort` về lại mức người dùng đã set |

Xin rồi thì đi tiếp theo câu trả lời. **Không dừng lượt vì chuyện này**: nó không có dòng nào trong bảng
"Khi nào dừng", và một lượt dừng lại để chờ đổi effort là một lần bỏ việc giữa chừng. Effort đang set đã là
mức của bảng thì không xin gì.

Trên Opus 5.5 và Fable 5.1, qua API key hay gói Claude, đổi effort **giữ cache** (tài liệu prompt caching của
Claude Code, tra 2026-09-28), nên đổi giữa phiên không tốn gì thêm. Trên model và đường khác, đổi effort vỡ
cache như đổi model: khi đó **chỗ đổi tốt nhất là ranh giới giữa hai phiên** (ADR-0023) — phần nghĩ là phiên
viết spec, phần làm là phiên `/task-do` mới, xin đổi ngay đầu phiên mới lúc context còn nhỏ. Người dùng đã bảo
"làm luôn" trong phiên cũ trên một model như thế thì giữ effort đang chạy và ghi `giữ — phiên lớn` vào báo cáo.

## 3. Subagent

- **`architecture-reviewer`** (rà soát là phần nghĩ) khai `effort: high` trong frontmatter — sàn của mục 1.
- **Lane agent giữ `model: inherit` và không khai `effort`**: phần thực thi chạy đúng model và effort người
  dùng đã chọn. Đó là toàn bộ ý nghĩa của nửa sau luật này.
- Không agent nào khai cứng một model khác `inherit`.

## Xong khi

Báo cáo nói effort nào đã dùng cho phần nghĩ và phần làm, trên model nào, hoặc nói rõ lý do giữ nguyên:
`giữ — đã ≥ high`, hay `giữ — phiên lớn`.
