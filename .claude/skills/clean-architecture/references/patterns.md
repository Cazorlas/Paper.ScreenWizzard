# Design pattern và SOLID: biết tên, cân nhắc, không dựng sẵn

Trang này trả lời **một câu, lúc đang code**: *thứ mình sắp viết đã có tên pattern chưa, và bài toán của
nó đã có thật chưa?*

Biết tên thì có một từ để nói trong plan và review, và biết luôn những bẫy người khác đã gặp. Còn dựng
pattern khi chưa có bài toán thì đẻ ra lớp mỏng (*classitis*, xem mục "Sâu hay mỏng" của `SKILL.md`):
thêm interface, thêm file, người đọc phải nhớ thêm, mà không được gì. **Pattern là lời giải cho một bài
toán, không phải thứ đi tìm bài toán.**

## Thứ tự cân nhắc — đừng đảo

1. **Viết thẳng trước.** Một `if`, một `switch`, một hàm. Đa số chỗ trông "cần Strategy" chỉ cần một
   `switch` expression.
2. **Có một trong ba dấu hiệu dưới đây thì mới gọi tên pattern:**
   - **hôm nay** đã có ít nhất hai biến thể thật (không tính "sau này có thể");
   - đó là một **port**, và test cần fake nó bằng dữ liệu thuần;
   - host hoặc framework **bắt buộc** một interface (callback, `ICommand`, `INotifyPropertyChanged`).
3. **Thử dạng có sẵn trong C# trước** (bảng tiếp theo). Nhiều pattern GoF đã là tính năng của ngôn ngữ.
4. **Dựng rồi thì ghi lại.** Viết một dòng trong `Decisions` của plan: *pattern gì, bài toán gì, vì sao
   viết thẳng không đủ*. Không viết được câu đó thì đừng dựng.

## SOLID vừa đủ

SOLID là **hướng đi**, không phải chỉ tiêu phải đạt. Hình dạng use case / port đã cho sẵn phần lớn SOLID
mà không cần thêm lớp nào. Đi quá thì mỗi chữ cái lại thành một cách đẻ lớp mỏng.

| Nguyên tắc | Kit đã có ở | Đủ là | Quá tay là |
|---|---|---|---|
| **S** — một lý do để đổi | interactor quyết định, adapter dịch, command mỏng | mỗi lớp trả lời được *"đổi vì ai?"* bằng một câu | cắt theo từng bước chạy (`Parser` → `Validator` → `Processor` gọi thẳng nhau) |
| **O** — thêm mà không sửa | policy tách riêng, `switch` gom ở một chỗ | thêm một biến thể là sửa **một** chỗ | dựng điểm mở rộng cho biến thể chưa ai yêu cầu |
| **L** — thay được cho nhau | fake của port trong unit test | fake và adapter thật trả lời cùng một hợp đồng | cây kế thừa sâu để "dùng lại" code |
| **I** — interface nhỏ | port tách **đọc** và **thực thi** (reference port pair) | port chỉ có những gì interactor gọi | một interface cho mỗi method |
| **D** — phụ thuộc vào trừu tượng | interactor biết port, không biết host | có interface **ở ranh giới**: host, IO, LLM, thời gian | interface cho mọi lớp bên trong, kể cả lớp chỉ có một cài đặt |

**Luật gọn:** trừu tượng hoá ở **ranh giới** (port), còn bên trong viết thẳng. Test decision core bằng
dữ liệu thuần, đưa DTO vào và so plan ra, thì không cần mock. Nếu cần mock thì ranh giới đang đặt sai chỗ.

## C# đã có sẵn — dùng cái này trước khi dựng lớp

| Pattern | Dạng gọn trong C# | Chỉ dựng lớp khi |
|---|---|---|
| Strategy | `Func<TIn, TOut>` hoặc `switch` expression | mỗi biến thể có trạng thái riêng, hoặc có nhiều method |
| Iterator | `IEnumerable<T>` + `yield return` | không bao giờ tự viết `IIterator` |
| Observer | `event`, `INotifyPropertyChanged`, `IObservable<T>` | — |
| Command | record của plan, delegate, `ICommand` | cần undo/redo, hoặc cần xếp hàng rồi chạy sau |
| Prototype | `record` + biểu thức `with` | — |
| Visitor | `switch` expression có pattern matching trên kiểu | có nhiều thao tác trên một cây kiểu **đóng** và ổn định |
| Singleton | lifetime `Singleton` của container DI | không bao giờ dùng `static Instance` cho thứ có trạng thái |
| Decorator | đăng ký DI bọc quanh một port (log, cache, retry) | — |
| Template Method | truyền delegate/port vào, không kế thừa | framework bắt kế thừa một base class |

## Kit đã dùng những pattern này dưới tên khác

Đọc bảng này để **nói cùng một thứ tiếng** với plan và `architecture-reviewer`, không phải để dựng thêm.

| Từ của kit | Tên GoF | Bẫy |
|---|---|---|
| adapter đứng sau port | **Adapter** | adapter bắt đầu tự quyết định: giữ/bỏ, thứ tự. Việc đó thuộc interactor |
| plan do interactor trả về, adapter thực thi | **Command** (dạng dữ liệu) | plan mang đối tượng host thay vì id, nên không test được nếu không có host |
| `<Rule>Policy` | **Strategy** | chỉ có một policy mà vẫn dựng `IPolicy`. Tách lớp là được, bỏ interface đi |
| command mỏng: dựng adapter, gọi interactor | **Facade** mỏng | command lớn dần thành nơi chứa logic |
| host state để trong field static | **Singleton**, ở đây là anti-pattern | hook `no-static-host-state` chặn. Tài liệu đóng rồi mở lại thì đối tượng thành rác |
| DTO in, plan out | tách **Query** và **Command** | adapter gọi ngược vào host giữa lúc core đang quyết định |

## Từng pattern: dấu hiệu nên cân nhắc, và lúc không nên

### Tạo đối tượng

| Pattern | Cân nhắc khi | Đừng dựng khi |
|---|---|---|
| **Simple Factory** | cùng một logic chọn kiểu (theo loại, theo cấu hình) bị lặp ở nhiều chỗ | chỉ có một chỗ gọi: để `switch` tại chỗ |
| **Factory Method** | framework tạo đối tượng, còn lớp con quyết định tạo kiểu nào | DI đã tạo được: đăng ký là xong |
| **Abstract Factory** | hai **họ** đối tượng phải đổi cùng nhau, ví dụ bộ adapter của hai host | chỉ có một họ. Đây là pattern hay bị dựng thừa nhất |
| **Builder** | đối tượng nhiều tham số tuỳ chọn, cần kiểm tra khi dựng | `record` với `init` và giá trị mặc định là đủ |
| **Prototype** | cần bản sao để sửa một chút | dùng `with` trên record, không tự viết `Clone()` |
| **Singleton** | thứ không trạng thái, hoặc trạng thái sống đúng bằng process | giữ đối tượng host, hoặc giữ thứ test cần thay. Dùng lifetime DI |

### Cấu trúc

| Pattern | Cân nhắc khi | Đừng dựng khi |
|---|---|---|
| **Adapter** | đổi API ngoài (host, SDK, HTTP) sang record của mình. Là hình dạng mặc định của port | hai phía đã cùng kiểu: adapter chỉ chuyển tiếp là lớp thừa |
| **Bridge** | hai trục biến thể độc lập nhân lên với nhau (ví dụ loại hình × cách xuất) | mới có một trục |
| **Composite** | dữ liệu là cây: nhóm chứa phần tử hoặc nhóm, và cần đối xử như nhau | cây chỉ sâu một cấp: dùng `List<T>` |
| **Decorator** | thêm log, cache, retry, đo thời gian quanh một port mà không sửa port | chỉ bọc một chỗ gọi: viết thẳng ở đó |
| **Facade** | một thao tác cần gọi năm API host theo đúng thứ tự | facade bắt đầu quyết định nghiệp vụ, tức là thành interactor giấu mặt |
| **Flyweight** | hàng nghìn đối tượng dùng chung phần bất biến (hình học, kiểu, style) | chưa đo được bộ nhớ là vấn đề |
| **Proxy** | nạp chậm, gọi từ xa, hoặc kiểm quyền trước khi gọi thật | `Lazy<T>` là đủ |

### Hành vi

| Pattern | Cân nhắc khi | Đừng dựng khi |
|---|---|---|
| **Chain of Responsibility** | chuỗi luật kiểm tra hay xử lý lỗi, mỗi mắt được bỏ qua hoặc dừng chuỗi | thứ tự cố định và ngắn: một chuỗi `if` dễ đọc hơn |
| **Command** | xếp hàng việc để chạy sau (ví dụ chờ tới lượt thread của host), undo/redo, ghi log thao tác | chỉ gọi một lần ngay tại chỗ |
| **Mediator** | nhiều view model phải báo nhau mà không biết nhau | nó giấu luồng: đọc code không biết ai nhận. Thử truyền tham số trước |
| **Memento** | cần chụp rồi khôi phục trạng thái của **chính mình** | host đã có transaction/undo: đừng làm lại |
| **Observer** | nhiều bên cần biết một thay đổi | chỉ một bên nghe: gọi thẳng. Nhớ gỡ đăng ký, vì event bị quên gỡ là rò bộ nhớ |
| **State** | mỗi trạng thái có **nhiều** hành vi khác nhau, và bảng chuyển trạng thái rắc rối | `enum` + `switch` vẫn còn đọc được |
| **Strategy** | có ít nhất hai thuật toán hoặc luật thật, được chọn lúc chạy | một thuật toán: viết hàm. Hai thuật toán một method: dùng `Func<>` |
| **Template Method** | framework bắt kế thừa | tự thiết kế: ưu tiên truyền port hoặc delegate |
| **Visitor** | nhiều thao tác trên một cây kiểu đóng, ổn định | cây kiểu còn đổi: mỗi kiểu mới bắt sửa mọi visitor |

## Pattern AI hay dựng thừa — tự hỏi lại trước khi viết

| Dấu hiệu trong diff | Hỏi |
|---|---|
| `IXxx` mới chỉ có **một** cài đặt, không phải port, không có fake trong test | interface này che cái gì? Không trả lời được thì bỏ |
| `XxxFactory` chỉ `new` một kiểu | factory này chọn giữa những kiểu nào? |
| `AbstractXxx` hoặc `BaseXxx` có một lớp con | kế thừa này cho ai dùng lại? |
| `XxxManager`, `XxxHelper`, `XxxService` bọc đúng một lời gọi | nó thêm gì ngoài một bước nhảy? |
| Visitor hoặc Mediator trong một tính năng dưới 5 file | `switch` hoặc truyền tham số có đủ không? |
| `static Instance` | nó giữ trạng thái gì, và ai reset nó khi tài liệu đóng? |

Luật thực dụng giống ở `SKILL.md`: **người đọc phải mở năm file mới hiểu một cái nút thì thiết kế sai**,
dù pattern nào cũng có tên đẹp.

## Nguồn

Cách gọi tên và cách chia ba nhóm (tạo, cấu trúc, hành vi) theo GoF, tham khảo *design-patterns-for-humans*
của Kamran Ahmed (CC BY 4.0, commit `7023f30`). Code mẫu gốc viết bằng PHP. Trang này **viết lại từ đầu**
cho C# và cho hình dạng use case / port: không chép đoạn nào.
