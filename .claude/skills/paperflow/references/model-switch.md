# Luật 10 đủ: model mạnh để nghĩ, model đang set để làm

Phần *nghĩ* — suy luận, viết `SPEC.md`, viết plan, chia task, thảo luận, phân tích, rà soát — chạy trên
model mạnh nhất. Phần *làm* — viết code, chạy task, build, test, lái host — chạy trên model người dùng
đang set. Bốn điều dưới đây là thứ làm nó thành luật chứ không phải lời khuyên.

## 1. Cổng đầu tiên là tài khoản, không phải công việc

Chỉ đổi model khi tài khoản đang chạy khớp `models.strongOnAccount` của `.claude/paper.profile.json`.

Profile không khai khoá đó, hay không biết chắc đang ở tài khoản nào → **không đổi**. Mặc định nghiêng về
"không đổi" là có chủ ý và là nửa quan trọng hơn của luật: một máy có thể đăng nhập tài khoản công ty, và
đốt hạn mức của công ty cho phần suy luận của mình là **cái giá người khác trả**. Một luật tối ưu cho mình
bằng túi tiền của người khác thì không phải luật.

Khoá này sống trong profile chứ không nằm trong kit là vì cùng lý do mọi giá trị cụ thể khác nằm ở đó:
kit đi tới ba dự án và không biết ai đang đăng nhập.

## 2. Cổng thứ hai là hạn mức

Model mạnh nhất có hạn mức riêng, tách khỏi hạn mức thường. Hết thì dùng mặc định cho **cả hai** phần và
**nói ra một dòng** — không chờ, không hỏi lại, và tuyệt đối không nói như thể vẫn đang ở model mạnh.

## 3. Session chính không tự đổi model của chính nó

Không có công cụ nào cho nó làm việc đó. Nó **xin một câu**, đúng hai chỗ chuyển tiếp:

| Chỗ | Xin gì |
| --- | --- |
| trước chặng 1 | bắt đầu phần nghĩ — đề nghị lên model mạnh |
| trước chặng 4 | bắt đầu phần làm — đề nghị về model thường |

Xin rồi thì đi tiếp theo câu trả lời. **Không dừng lượt vì chuyện này**: nó không có dòng nào trong bảng
"Khi nào dừng", và một lượt dừng lại để chờ đổi model là một lần bỏ việc giữa chừng.

## 4. Subagent thì đổi được, nên phải đổi đúng

- Agent **suy luận và rà soát** được gọi kèm `model` mạnh — khi hai cổng trên cho phép.
- **Lane agent giữ `model: inherit`**, vì phần thực thi phải chạy đúng model người dùng đã chọn. Đó là
  toàn bộ ý nghĩa của nửa sau luật này.

Đổi **ở lúc gọi**, không ghi cứng vào frontmatter. Ghi cứng thì mất luôn cả hai nhánh "sai tài khoản" và
"hết hạn mức" — một agent khai cứng model mạnh sẽ đòi nó kể cả trên tài khoản công ty, đúng cái luật này
sinh ra để chặn.

## Xong khi

Báo cáo nói model nào đã dùng cho phần nghĩ và phần làm, hoặc nói rõ lý do không đổi:
`mặc định — sai tài khoản`, hay `mặc định — model mạnh không khả dụng`.
