# ADR-0003: Trong mỗi tầng, thư mục cấp một là domain; vai (Ports, UseCases, Models) là cấp hai

## Status

Proposed, chờ chủ dự án duyệt. Duyệt plan refactor không phải duyệt ADR này.

## Date

Dự thảo 2026-09-26.

## Context

ADR-0001 chọn phương án A (mỗi tầng một project) và vẽ bên trong UseCases hình
`UseCases/<Feature>/{Ports, Implements, Models}`. Bộ kit paper-kit 1.32.0 (vendor 2026-09-26, commit `1c9d5c8`) đổi
hình bên trong mọi tầng theo ADR-0019 của kit, và agent `architecture-reviewer` nay báo hình cũ là lỗi (mục 10,
"Folder shape"). Hùng yêu cầu 2026-09-26: "refactor lại theo adr paperflow mới".

Đo trên cây hiện tại (commit `1c9d5c8`):

- `Domain/` có `Common/` và `Geometry/` đứng cạnh ba domain `Capture/`, `Editor/`, `Shell/`: hai thư mục không phải
  domain, một cái dùng chung, một cái theo kỹ thuật.
- `UseCases/<Feature>/Implements/` là tên cũ của `UseCases/`; bốn file port gom nhiều interface và cả record
  (`CapturePorts.cs` có `ScreenCaptureResult`, `IImageDelivery.cs` có `DeliveryResult`); port mang hậu tố `Port`;
  `IMonitorCatalogPort` nằm ở `Shell/Ports` trong khi chỉ lõi của Capture gọi nó.
- `Infrastructure/Common/` gom adapter theo "dùng chung", không theo domain.
- `Presentation/` chia theo vai trước (`ViewModels/<D>`, `Views/<D>`, `Rendering/`), có hai thư mục vai `Services/`
  lồng trong `Views/`.
- `App/Startup/` là thư mục vai trong entry host.

## Decision

1. **Trong mỗi project tầng, thư mục cấp một là domain**: `Capture`, `Editor`, `Shell`, cộng `Shared` cho cái hai
   domain trở lên cùng cần. Tầng ngoài dùng lại đúng tên đó. Presentation giữ thêm hai thư mục không phải domain,
   mỗi cái có lý do ghi trong test: `Mvvm/` (bộ MVVM tự giữ, `CLAUDE.md` gọi tên nó) và `Resources/` (từ điển XAML
   được gọi bằng pack URI).
2. **Vai là cấp hai.** UseCases: `<D>/Ports/` (chỉ interface, mỗi file một interface, tên file bằng tên interface),
   `<D>/UseCases/` (interactor, session, policy), `<D>/Models/` (record, enum). Presentation: `<D>/ViewModels/`,
   `<D>/Commands/`, `<D>/Views/`, và riêng Editor thêm `Editor/Rendering/` (vẽ hình ghi chú bằng WPF: không phải cửa sổ, không
   phải view model). Domain và Infrastructure không cần thư mục vai: file nằm thẳng trong domain.
2a. **Domain chỉ nhìn chính nó và `Shared/`.** `Shell` là domain điều phối: phím tắt, khay và thanh chụp bắt đầu một lần chụp và
   mở trình sửa, nên `Shell` được nhìn mọi domain. `Shared/` không nhìn domain nào. Nợ có tên: `AppSettings` nằm ở
   `Domain/Shell` và dựng từ kiểu của Shell lẫn Capture, nên lõi và màn hình của Capture và Editor đọc `Domain.Shell` để lấy
   lựa chọn của người dùng; chuyển nó là một việc riêng (cần tách cài đặt theo domain).
3. **Port tên theo cái cần, không hậu tố `Port`**, và nằm trong domain dùng nó (lõi hay màn hình); port hai domain trở
   lên dùng nằm ở `Shared/Ports/`, và bên cài nằm ở `Shared/` của tầng mình.
4. **Entry host mỏng, không thư mục vai**: file của `App` nằm ở gốc project.
5. **Test kiến trúc giữ hình này** (`tests/Paper.ScreenWizzard.UnitTests/Architecture/`), theo
   `clean-architecture/references/architecture-tests-csharp.md` mục 5 và 6. Hướng giữa các thư mục domain (Decision
   2a) có test cho dòng `using`; tên đầy đủ trong biểu thức và `clr-namespace` trong XAML thì agent `architecture-reviewer` giữ.
6. Phương án A của ADR-0001 không đổi: vẫn năm project, cùng hướng tham chiếu. ADR này thay **hình bên trong**
   project mà ADR-0001 vẽ, và thay Decision 5 của nó ("không refactor một lượt") cho riêng lần chuyển này.

## Alternatives Considered

- **Giữ hình cũ, ghi mọi module là nợ có tên** (`DomainDebt` của test). Loại vì dự án chỉ có ba domain và đợt 2
  (quay màn hình) sắp thêm domain thứ tư: chuyển bây giờ tốn ít nhất, và đợt 2 viết thẳng theo hình mới.
- **Chuyển dần từng domain khi được sửa tới** (Decision 5 của ADR-0001). Loại vì đổi tên port và `Common` → `Shared`
  cắt ngang cả ba domain; chuyển nửa chừng thì hai hình sống cùng lúc lâu dài.
- **Chuyển `Mvvm/` vào `Shared/Mvvm/`**. Loại: `CLAUDE.md` và skill `paper-wpf-style` gọi nó là `Presentation/Mvvm/`;
  đổi chỗ không mua được gì.

## Consequences

- Namespace đi theo thư mục, nên mọi `using` trong `src/` và `tests/` đổi; tên kiểu không đổi, trừ port (bỏ hậu tố
  `Port`) và fake của port.
- Một namespace lặp chữ: `Paper.ScreenWizzard.UseCases.Capture.UseCases`. Đó là giá của tên vai chung của kit.
- `CLAUDE.md` (bảng tầng: `I<X>Port` thành `I<X>`, đường dẫn `App/Startup/NativeMethods`) và `CODEMAP.md` của mỗi
  project đổi theo.
