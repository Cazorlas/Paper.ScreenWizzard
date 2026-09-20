# ADR-0001: Quyết định của một tính năng nằm trong tầng không dùng host; host đi qua cổng với dữ liệu thuần

## Status

Accepted. Setup của paper-kit tạo bản khởi đầu; dự án chọn phương án A ở mục **Decision** và người dùng duyệt
cùng plan đợt 1. Từ lúc này không sửa nội dung nữa: đổi ý là viết ADR mới thay nó.

## Date

2026-09-20

## Context

Phần **quyết định** của một lệnh — giữ hay bỏ, thứ tự, kích thước, báo gì, ghi log gì — hay bị viết lẫn với
code gọi **host**: ứng dụng chủ mà add-in chạy trong đó, framework web, framework UI, cơ sở dữ liệu, một
model ngôn ngữ. Viết lẫn thì quyết định chỉ kiểm được khi host đang chạy: chậm, khó lặp lại, và bug lộ ra
muộn, ở máy người dùng.

Clean architecture tách hai thứ đó theo **hướng tham chiếu một chiều**: phần quyết định không biết host,
host được gọi qua **port** (interface do phần quyết định khai), và thứ đi qua port là **dữ liệu thuần**.

### Các tầng

| Tầng | Chứa | Được tham chiếu | Không được tham chiếu |
| --- | --- | --- | --- |
| **Domain** | entity, value object, luật nghiệp vụ thuần | không gì ngoài thư viện chuẩn | host, UI, IO, mọi tầng khác |
| **UseCases** | interactor (lộ ra bằng interface `I<Feature>Interactor`), policy, port (`I<Something>Port`), record thuần đi qua port | Domain | host, UI, Infrastructure, Presentation |
| **Infrastructure** | adapter cài port: đọc host thành record, thực thi plan lên host | UseCases, Domain, host API | Presentation; không quyết định nghiệp vụ |
| **Presentation** | view, view model | UseCases, Domain | Infrastructure, host API |
| **entry host** | điểm vào: lệnh, đăng ký DI, đăng ký với host | mọi tầng | không quyết định; chỉ dựng adapter, gọi interactor, đưa kết quả lên view |

```text
entry host      -> Presentation, Infrastructure, UseCases, Domain
Presentation    -> UseCases, Domain
Infrastructure  -> UseCases, Domain
UseCases        -> Domain
Domain          -> (không gì)
```

**Dữ liệu qua port là dữ liệu thuần:** số, chuỗi, record; id của host đổi sang số hay chuỗi; đơn vị ghi
trong tên field (`LengthMm`, `AngleDeg`). Không bao giờ là một đối tượng của host. Adapter đọc host thành
record **một lần**, interactor nhận record và trả **plan** (record: làm gì, ở đâu, id nào), adapter thực thi
plan. Test của interactor vì vậy không cần mock host: đưa record vào, so plan ra.

### Câu hỏi còn mở: tầng là project hay thư mục

Bốn tầng trên có thể là bốn **project** (compiler giữ ranh giới), hoặc bốn **thư mục** bên trong mỗi
module tính năng (test kiến trúc giữ ranh giới). Hai cách đều đúng clean architecture; chúng khác nhau ở
chỗ **cái gì được ship**. Phép thử: *ta chờ điều gì thay đổi, và cách này có làm thay đổi đó rẻ đi không?*

## Decision

1. **Mọi quyết định của một tính năng nằm trong UseCases và Domain**, và chạy được trong unit test không cần
   host. Tính năng không có quyết định nào (một lệnh chỉ mở hộp thoại) không dựng interactor rỗng.
2. **Host chỉ đi qua port**, do một adapter trong Infrastructure cài. Dữ liệu qua port là record thuần.
3. **Hướng tham chiếu một chiều** như bảng trên. Hook của paper-kit (`layer-guard`, `no-static-host-state`)
   chặn lúc sửa, theo `architecture` trong `.claude/paper.profile.json`; test kiến trúc của dự án đỏ lúc
   build.
4. **Cách đặt tầng: A, tầng là project** — xem hai phương án ở **Alternatives Considered**. Paper.ScreenWizzard
   là một ứng dụng, mọi tính năng (chụp, sửa ảnh, quay, ghi chú trên màn hình) ship cùng nhau trong một
   file chạy, nên không có đơn vị ship theo tính năng để bảo vệ; ngược lại Domain và UseCases nhắm
   `net10.0` thuần (không `-windows`), nên compiler tự cấm chúng gọi WPF hay Win32, không cần test canh.
   Phương án B ở lại dưới đó làm phương án đã loại.
   Bốn project: `Paper.ScreenWizzard.Domain`, `.UseCases`, `.Infrastructure` (Win32, GDI, WinRT: chụp, cửa sổ,
   clipboard, phím tắt, file, cài đặt) và `.App` (WPF: Presentation cùng gốc ghép DI). Presentation là thư
   mục trong `.App`, không phải project riêng: tách nữa chỉ thêm project mà không thêm ranh giới nào compiler
   chưa giữ; test kiến trúc canh việc ViewModel không gọi Infrastructure.
5. **Áp dụng cho tính năng mới, và cho tính năng cũ khi được sửa tới** — không refactor một lượt.

### Điều kiện chuyển (switch condition)

- **Từ B sang A, cho đúng một module:** khi module đó có **host hay người dùng thứ hai** (second host or
  consumer) cần phần quyết định mà không có host thứ nhất — một ứng dụng chủ khác, một dịch vụ web, một CLI,
  một plugin khác ship bộ tính năng khác. Khi ấy tách Domain và UseCases của **module đó** thành project
  riêng; không tách cả solution.
- **Từ A sang B:** khi đo được rằng tầng cắt ngang tính năng còn việc ship cắt dọc — một tính năng nhỏ trải
  ba bốn project, phần lớn interface chỉ có một cài đặt, và chưa có host thứ hai nào dùng lại UseCases.
- Chuyển là một quyết định khó đảo ngược: viết ADR mới thay ADR này, kèm số đo.

## Alternatives Considered

### A. Tầng là project (layers as projects)

Mỗi tầng một project: `<App>.Domain`, `<App>.UseCases`, `<App>.Infrastructure`, `<App>.Presentation`, và
project entry host tham chiếu tất cả.

- Ưu điểm: **compiler giữ ranh giới** — UseCases không tham chiếu được host vì project không có package
  đó. Project test chỉ cần tham chiếu UseCases và Domain. Không cần test kiến trúc cho hướng tham chiếu.
- Nhược điểm: **project nhân lên theo module**. Nhiều module tính năng ship, bật tắt hay cấp phép riêng thì
  hoặc gom mọi tính năng vào bốn project chung (mất đơn vị ship theo tính năng), hoặc nhân bốn project cho
  mỗi module. Mỗi project mới vào ma trận build, đóng gói và cấu hình phát hành. Một thay đổi nhỏ chạm
  nhiều project.
- Hợp khi: một sản phẩm, một bộ tính năng ship cùng nhau; hoặc phần quyết định đã có host thứ hai.

### B. Tầng là thư mục trong mỗi module tính năng (layers as folders)

Ranh giới project đi theo **tính năng**: mỗi module là đơn vị ship và không tham chiếu module khác. Bên
trong module, các tầng là thư mục:

```text
<Module>/
  UseCases/<Feature>/Ports/       I<Feature>Interactor, I<Something>Port  (hợp đồng)
  UseCases/<Feature>/Implements/  <Feature>Interactor, <Rule>Policy
  UseCases/<Feature>/Models/      record thuần: DTO vào, plan ra
  Adapters/                       <Something>Adapter : I<Something>Port
  Commands/                       entry: dựng adapter, gọi interactor
  ViewModels/, Views/             Presentation
```

Phần không dùng host mà hai module cùng cần thì lên một project dùng chung, không nằm trong module nào.

- Ưu điểm: một tính năng nằm trọn một chỗ; số project theo số module, không nhân bốn. Bên trong module tự
  chọn độ sâu theo kích thước.
- Nhược điểm: **compiler không giữ ranh giới** trong module — một `using` host trong `UseCases/` vẫn build
  xanh. **Test kiến trúc giữ** thay: mỗi project có tầng và chỉ tham chiếu xuống; không module nào tham
  chiếu module khác; thư mục không dùng host không nhắc namespace nào của host; interactor lộ ra bằng
  interface. Kiểu host lọt qua `var` hay tên đầy đủ trong biểu thức thì test đọc `using` không bắt được —
  agent review kiến trúc là lớp kiểm thứ hai.
- Hợp khi: nhiều module tính năng ship riêng, chưa module nào có host thứ hai.

### Không tách

- Từ chối: quyết định lẫn với lệnh gọi host thì chỉ kiểm được khi host chạy, và mỗi bug quyết định phải
  tái hiện trong host.

## Consequences

- Test của interactor viết trước, với port giả bằng dữ liệu thuần. Fake mà chữ ký phải nhắc kiểu host là
  port đã rò host — sửa port, không sửa fake.
- Mỗi dự án đặt tên một **cặp port mẫu** (một port đọc, một port thực thi) của một tính năng đã chạy thật,
  bằng một ADR sau; tính năng mới chép hình của cặp đó.
- Hook chỉ thấy file sửa qua Edit/Write, không thấy file ghi bằng lệnh shell; test kiến trúc lúc build và
  agent review lúc kiểm việc bù chỗ đó.
- Với phương án B, bảng tầng trong `CLAUDE.md` của dự án và bảng tầng trong test kiến trúc là **cùng một
  danh sách**: thêm project là phải xếp nó vào tầng, nếu không test đỏ.
