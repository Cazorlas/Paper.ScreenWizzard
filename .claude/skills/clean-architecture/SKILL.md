---
name: clean-architecture
description: Place a feature's code in the use case / port shape - the decision logic in a host-free interactor, the host behind a port with plain data crossing it, a thin command and a translating adapter - and keep it there with the profile's platform-free rules. Use when adding or refactoring a feature, when a decision is only testable with the host running, or when layer-guard blocks an edit.
---

# Use case, port, adapter

**Mục tiêu một câu:** mọi *quyết định* của một tính năng chạy được trong unit test **không cần host** —
không ứng dụng host đang chạy, không web server, không LLM thật.

Hình dạng này đã được viết ba lần ở ba dự án, mỗi lần gắn tên project riêng (một ADR, một hook
`platform-free-layers`, một agent reviewer). Ở đây nó viết một lần; cái gì bị cấm ở đâu thì
`.claude/paper.profile.json` → `architecture` khai.

## Từ vựng — bốn từ, dùng đúng nghĩa

| Từ | Nghĩa |
|---|---|
| **decision core** | interactor và policy: mọi quyết định (giữ/bỏ, thứ tự, kích thước, báo gì). Không một kiểu host nào |
| **mechanism** | adapter đứng sau một port: đọc host ra dữ liệu thuần, và thực thi plan lên host. Không quyết định |
| **plan** | thứ interactor **trả về** cho adapter thực thi — record thuần: tạo gì, ở đâu, id nào (chuỗi/số, không phải đối tượng host). Khác plan của task trong `<featureDocs>` |
| **DTO in, plan out** | ranh giới: adapter đọc host thành DTO **một lần**, decision core nhận DTO và trả plan, adapter đổi id về đối tượng host rồi thực thi |

Test của decision core vì vậy không cần mock: đưa DTO vào, so plan ra.

## Cặp port mẫu (reference port pair)

Mỗi dự án **đặt tên một reference port pair** trong ADR của chính nó (`docs/adr/` hay `docs/decisions/`):
một port đọc (host → DTO) và một port thực thi (plan → host) của một tính năng đã chạy thật — ví dụ
`I<Feature>Reader` / `I<Feature>PlanExecutor`. Tính năng mới chép hình của cặp đó; `architecture-reviewer`
so port mới với nó. Dự án chưa có ADR nào đặt tên cặp này thì tính năng đầu tiên đi theo hình dạng dưới
đây sẽ đề xuất nó — một dòng ADR, do chủ dự án duyệt.

## Hình dạng

```text
UseCases/<Feature>/
  Ports/        I<Feature>Interactor     interface của use case
                I<Something>Port         interface mà use case cần từ thế giới ngoài (đọc DTO / thực thi plan)
                                         port là hợp đồng; bên cài nó là một *service*, nằm ngoài UseCases/
  Implements/   <Feature>Interactor      quyết định: giữ/bỏ, thứ tự, báo gì, log gì
                <Rule>Policy             luật tách riêng khi interactor dài ra
  Models/       record thuần             DTO vào, plan ra: số, chuỗi, record — không đối tượng host

Adapters/ (ngoài UseCases)   <Something>Adapter : I<Something>Port   dịch host <-> record, KHÔNG quyết định
Commands/                    dựng adapter, gọi interactor, đưa kết quả lên view model — hết
ViewModels/                  phụ thuộc I<Feature>Interactor + Models, không phụ thuộc adapter
```

| Tầng | Được gọi | Không được gọi |
|---|---|---|
| Use case / Domain | BCL, record của chính nó, port interface | host API, UI framework, HTTP/DB client |
| Adapter | host API, port interface, Models | quyết định nghiệp vụ |
| Command | adapter, interactor, view model | logic |
| View model | interactor interface, Models | adapter, host API |

**Dữ liệu qua port là dữ liệu thuần.** Đơn vị tường minh (mm, độ) trong tên field. Không `Element`,
`ElementId`, `Entity`, `ObjectId`, `DbContext`, `HttpContext` — adapter đổi chúng sang record trước.

## Làm theo thứ tự

1. **Test của interactor trước** (lane `unit`, `[red]`), với port giả bằng dữ liệu thuần. Fake mà chữ ký
   phải nhắc kiểu host nghĩa là port đã rò host — sửa port, đừng sửa fake.
2. **Interactor** tới khi xanh.
3. **Adapter** cài port — đây là chỗ duy nhất gọi host; tra API theo skill `api-lookup` trước.
4. **Command** mỏng: dựng adapter → gọi interactor → đưa kết quả lên view model.
5. **Lane `live`**: chạy thật trên host, đọc lại kết quả.

## Profile giữ ranh giới

```json
"architecture": {
  "platformFree": [
    { "path": "**/UseCases/**", "forbid": ["Vendor.HostApi", "System.Windows"], "forbidPackages": ["Vendor.HostApi"] },
    { "path": "src/*.Domain/**", "forbid": ["Vendor."], "forbidPackages": ["Vendor"] }
  ],
  "hostStateTypes": ["Document", "Element"]
}
```

`Vendor.HostApi` đứng chỗ namespace và package của host; `profile.json` của gói host (`hosts/<host>`) ghi
danh sách thật, và setup chép nó vào profile của dự án lần đầu.

- Hook `layer-guard` chặn một Edit/Write đưa namespace hay package bị cấm vào đường dẫn khớp glob.
- Hook `no-static-host-state` chặn field static giữ `hostStateTypes` — nó bắt tài liệu đầu tiên của
  process, và thành rác sau khi đóng rồi mở lại file.
- Cả hai không thấy file ghi qua lệnh shell. Trước khi báo xong, chạy agent `architecture-reviewer`.

Luật sai cho dự án này thì **sửa profile**, không sửa hook.

## Tầng là project hay thư mục — ADR giữ quyết định, test giữ ranh giới

ADR kiến trúc của dự án (setup tạo `docs/decisions/0001-clean-architecture.md` khi dự án chưa có ADR nào)
chọn một trong hai:

- **Tầng là project** — compiler giữ ranh giới; giá là số project nhân lên theo module.
- **Tầng là thư mục trong module tính năng** (hình dạng ở trên) — test kiến trúc giữ ranh giới.

Chuyển khi một module có host hay người dùng thứ hai — bằng ADR mới, theo skill `adr`. Test kiến trúc C#
cho cả hai cách (mỗi project có tầng và chỉ tham chiếu xuống, module không tham chiếu module, thư mục không
dùng host không nhắc namespace host, interactor lộ ra bằng interface):
[references/architecture-tests-csharp.md](references/architecture-tests-csharp.md).

## Sâu hay mỏng — ba dấu hiệu tách sai

Đúng tầng mà vẫn sai thiết kế là chuyện thường. Ba câu hỏi dưới đây bắt được thứ luật tầng không bắt được,
và cả ba đều đo được chứ không phải cảm tính.

**Một module sâu là module có giao diện đơn giản hơn nhiều so với phần nó che.** Giao diện là **cái giá**
module bắt cả hệ thống trả; phần cài đặt là **cái lợi**. Một lớp mà đọc hiểu cách gọi còn lâu hơn tự viết
lại thì nó đang lỗ. Đây chính là lý do tồn tại của seam: seam sâu vì nó che cả host lẫn UI sau vài cái port.

**Rò rỉ tri thức: một quyết định mà nằm ở N chỗ.** Đổi nó là đổi N chỗ, và không trình biên dịch nào nhắc.
Chỉ số đo được sẵn: **số file cần cả namespace của host lẫn của UI** — mỗi file như vậy là một chỗ hai quyết
định đang dính vào nhau, và là lý do file đó không có tầng nào để về ngoài seam. Chỉ số đó giảm thì thiết
kế tốt lên; nó tăng sau một đợt refactor thì đợt đó đi sai hướng.

**Đẻ lớp mỏng (classitis).** Mỗi interface là một thứ người đọc phải nạp vào đầu. Ba lớp `Parser`,
`Validator`, `Processor` mà cái nào cũng gọi thẳng cái sau thì đó là **cắt theo thời điểm chạy**, không phải
cắt theo tri thức — gộp lại. Luật thực dụng: **một người đọc phải mở năm file mới hiểu một cái nút thì thiết
kế sai**, kể cả khi mọi luật tầng đều xanh.

Ba dấu hiệu này dùng khi **xét một thiết kế**, không dùng để chặn commit: chúng không có test, và không nên
có — một thiết kế sâu hay mỏng là câu người đọc trả lời, không phải máy.

## Khi nào không tách

Tính năng không có quyết định nào (một lệnh chỉ mở một hộp thoại, một adapter chỉ đọc một giá trị): đừng
dựng interactor rỗng. Áp dụng cho tính năng mới, và cho tính năng cũ **khi được sửa tới** — không
refactor một lượt; refactor thì theo skill `parity-refactor`.
