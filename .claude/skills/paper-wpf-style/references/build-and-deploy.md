# Build, CI và phát hành một app WPF độc lập

Luật cho app desktop **không nằm trong host nào** (không phải add-in). Add-in có đường phát hành của host nó
(bundle, `.addin`, MSI); app độc lập thì dùng phần này. `CLAUDE.md` của dự án ghi lại các lựa chọn cụ thể bằng
một mục **Deploy**, và trỏ tới ADR nếu có quyết định khó đảo ngược.

## Ba việc, ba chỗ

| Việc | Chạy ở đâu | Chạy gì |
| --- | --- | --- |
| **Build và unit test** | mỗi push và pull request, runner `windows-latest` | `.github/workflows/ci.yml`: `dotnet build <slnx> -c Release`, rồi `dotnet test` project unit |
| **ui và e2e** | **máy người làm**, không bao giờ trên runner | verb `ui` / `e2e` của profile; kết quả là dòng bằng chứng của plan |
| **Phát hành** | khi có tag `v<x.y.z>` | `.github/workflows/release.yml`: publish, nén, đính vào GitHub release |

- **Vì sao ui và e2e không lên CI:** chúng lái chuột, bàn phím và chụp màn hình thật, cần một desktop tương tác;
  runner được host không cho. Đừng "sửa" việc đó bằng cách thêm chúng vào CI hay chạy chúng bằng mock cho xanh.
- **CI đỏ khi có cảnh báo:** dự án đặt `TreatWarningsAsErrors`, CI giữ nguyên, không thêm `-p:TreatWarningsAsErrors=false`.
- Đọc **số test đã chạy**, không chỉ exit code: một bước test chạy 0 test vẫn có thể exit 0.

## Phát hành

1. **Số phiên bản là tag.** `v1.2.0` → `-p:Version=1.2.0`. Không sửa số trong csproj bằng tay.
2. **Bản mặc định là gói di động (portable zip):** `dotnet publish <App.csproj> -c Release -r win-x64
   --self-contained -p:PublishSingleFile=true`, nén thư mục ra một file `<Tên>-<phiên bản>-win-x64.zip`. Không cần
   cài .NET, không cần quyền quản trị (manifest `asInvoker`). WPF không trim được: đừng bật `PublishTrimmed`.
3. **Verb `package` trong profile** chạy đúng lệnh publish đó ở máy, ra `artifacts/`, để người làm thử gói trước
   khi gắn tag. Workflow phát hành gọi lại cùng lệnh, không có một bản thứ hai của công thức.
4. **Mở gói ra và chạy thử** trước khi báo phát hành xong: giải nén vào thư mục trống, chạy exe, thấy cửa sổ
   đầu tiên (hay biểu tượng khay) — `build` xanh không chứng minh gói chạy được.
5. **Phát hành là việc gửi ra ngoài.** Agent không tự đẩy tag hay tạo release: chỉ khi chủ dự án nói trong phiên
   này. Workflow phát hành tồn tại để chủ dự án bấm, không phải để agent chạy sau mỗi task.

## Bộ cài, ký số, cập nhật tự động

Ba việc này **khó đảo ngược** (người dùng đã cài, chứng chỉ đã mua, kênh cập nhật đã hứa): mỗi việc một ADR,
không làm ngầm trong workflow.

| Chọn | Hợp khi | Cái giá |
| --- | --- | --- |
| **Zip di động** (mặc định) | công cụ cho vài người, cài không cần quyền | không có gỡ cài đặt, không tự cập nhật |
| **MSIX** | muốn gỡ sạch và cập nhật qua kênh của Windows | cần ký số; một số API file bị ảo hoá |
| **MSI / Inno Setup** | cài vào Program Files, mục khởi động, gỡ trong Settings | tự dựng và tự bảo trì script cài |

Chưa có ADR chọn thì **không có bộ cài**: gói là zip di động và `CLAUDE.md` ghi rõ "chưa có bộ cài".

## Mục Deploy trong CLAUDE.md của dự án

Ghi đủ bốn dòng, để agent sau không phải đoán:

- **CI:** tên workflow, chạy gì, và câu "ui và e2e chạy ở máy người làm".
- **Gói:** target (`win-x64`), self-contained hay không, verb `package`.
- **Phát hành:** ai bấm, bằng cách nào (tag), và rằng agent không tự phát hành.
- **Chưa có:** bộ cài, ký số, cập nhật tự động — nếu chưa quyết.
