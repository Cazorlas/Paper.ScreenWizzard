# Prototype trả lời một câu hỏi thiết kế

Chỉ dùng khi đọc source, ví dụ hoặc sơ đồ chưa đủ để quyết định. Trước duyệt plan, đề xuất câu hỏi,
artifact nhỏ nhất và kịch bản quyết định câu hỏi; tạo/chạy prototype theo task sau khi được duyệt.
Không mặc định mỗi việc đều có prototype.

- Logic/trạng thái không cần host: một HTML tự chứa, mở trực tiếp, dữ liệu bịa, hiện trạng thái sau
  mỗi thao tác, nút thử tự do và các kịch bản có tên/hướng dẫn. Tránh server hay dependency chỉ để demo.
- Giao diện: dùng cách chạy sẵn có của dự án; nêu rõ biến thể và câu hỏi đang so. Demo HTML không chứng
  minh bố cục hay tương tác của UI desktop hoặc host thật.
- Prototype mang nhãn thử nghiệm, tách khỏi đường chạy sản phẩm. Trạng thái ở bộ nhớ; khi câu hỏi cần
  lưu trữ, dùng dữ liệu tạm trong phạm vi plan cho phép, ghi cách dọn và giữ evidence trước khi dọn.

## Giữ lại để người sau kiểm được

Artifact theo nơi tài liệu dự án khai, ví dụ `<featureDocs>/<slug>/prototypes/<topic>/`. Plan phần
Decisions trỏ tới artifact và một dòng bảng evidence thiết kế:

| Câu hỏi | Artifact / nguồn | Kịch bản đã thử | Kết luận và giới hạn |
|---|---|---|---|
| <quyết định cần giải đáp> | <link, revision hoặc SHA-256 hash; chưa commit: hash và trạng thái cây> | <input bịa, thao tác, trạng thái đọc được> | <chọn gì, vì sao, chưa chứng minh gì> |

Đọc lại artifact và thử kịch bản trước khi ghi kết luận. Prototype chưa chạy thì ghi chưa kiểm được,
không kết luận theo code nhìn thấy. Giữ artifact hoặc bản sao có hash truy cập được; một link tới
nhánh đã xoá không đủ. Câu hỏi và kết luận nằm trong Decisions/ADR theo quy ước dự án.

Đây là **bằng chứng thiết kế**, không thay test hồi quy hay lane `live` của phần triển khai. Demo đạt
trên dữ liệu bịa mà chưa chạy host -> lane `live` là `not verifiable` (hay `không áp dụng` nếu profile
không khai), không phải `pass`. Không tự commit, tạo nhánh lưu trữ, gửi artifact hay publish vì mục này.
