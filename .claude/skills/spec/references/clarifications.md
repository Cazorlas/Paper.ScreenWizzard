# Làm rõ theo phụ thuộc và questionnaire

Đọc khi xếp vòng hỏi hoặc người dùng cần lấy thông tin từ chuyên gia khác. Giữ giới hạn năm câu
trong phiên và cổng duyệt của `task-spec`; questionnaire không tự tạo câu trả lời cho yêu cầu.

## Vòng hỏi

Ghi quyết định còn thiếu và điều kiện của từng quyết định. Dữ kiện trong source hoặc tài liệu sẵn có
do agent tra; chỉ hỏi người dùng lựa chọn làm đổi việc xây hoặc cách kiểm.

Ví dụ: định dạng báo cáo và nơi lưu độc lập nhau, hỏi cùng vòng. Danh sách người nhận phụ thuộc
chọn gửi email, để vòng sau. Mỗi câu có số, lựa chọn và đề xuất. Sau câu trả lời, cập nhật
Clarifications và xét lại những câu vừa đủ điều kiện; câu chưa đủ giữ **Chưa trả lời** cùng phụ thuộc.

## Questionnaire cho người nắm thông tin

Khi người dùng không biết chính sách hay nghiệp vụ, xác định **vai người nhận** và **quyết định cần
lấy về** từ thông tin đã có; chỉ hỏi phần còn thiếu. Hỏi về việc chuyển câu hỏi, không bắt người dùng
tự trả lời phần chuyên môn họ vừa nói không biết.

Soạn một tài liệu cạnh plan theo nơi tài liệu dự án khai, ví dụ `<featureDocs>/<slug>/questions/<topic>.md`.
Mỗi quyết định thiếu phải tới được ít nhất một câu. Ưu tiên câu quan trọng trước; một câu một ý.
Không tự đặt thời hạn hay danh tính người nhận khi chưa biết; dùng vai đã biết và hỏi nếu thật sự cần.

```markdown
# <Chủ đề>

## Mục đích và người nhận

Quyết định cần lấy về: <danh sách>. Người trả lời: <vai, chuyên môn>.
Kết quả dùng cho: <phần yêu cầu hoặc quyết định thiết kế>.

## Bối cảnh

<Đủ để người không đọc hội thoại hiểu câu hỏi.>

## Cách trả lời

Có thể trả lời một phần hoặc ghi "chưa biết"; nêu điều còn chưa chắc.

## Câu hỏi

1. <Một câu hỏi>
   Trả lời:

## Có gì chúng tôi chưa hỏi?
```

Clarifications ghi link questionnaire, câu hỏi liên quan và **Chưa trả lời**. Khi câu trả lời về,
ghi nguồn/vai và ngày, cập nhật từng quyết định được giải đáp; phần chưa rõ giữ mở. Các đề xuất của
người trả lời đi qua cùng cổng duyệt yêu cầu. Soạn tài liệu không cấp quyền gửi, publish hay duyệt plan.
