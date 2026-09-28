# Bốn hình dạng của một test không chứng minh gì

Mở từ skill `task-do`, mục "Một test xanh ngay lần đầu chưa phải là một test".

**Bốn hình dạng của một test không chứng minh gì** (đặt tên theo `wondelai/skills`, và xoá thẳng chứ đừng
sửa — nó không mang theo độ phủ nào để giữ):

| Hình dạng | Vì sao nó vô nghĩa |
|---|---|
| Khẳng định một mock trả về thứ chính test vừa bảo nó trả | Nó kiểm cái mock, không kiểm code |
| Tính giá trị mong đợi **bằng chính biểu thức** code đang dùng | Sai cùng nhau thì vẫn xanh |
| `Assert(HẰNG, Is.EqualTo(HẰNG))` | Xanh với mọi code biên dịch được |
| Snapshot một output chưa ai đọc mà đã duyệt | Đóng băng cái sai thành "đúng" |

Một test đặc tả ghim một giá trị **đã quan sát được** thì không nằm trong danh sách này; ghim thứ code tự
tính lại lúc assert thì có.
