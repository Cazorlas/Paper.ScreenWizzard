---
name: api-lookup
description: Look up every host or framework API member in the project's docs source before the code that calls it, and record the lookup in the plan's API table - an MCP docs server, a URL, or the assembly itself when neither exists. Use before calling any member of a host application, framework or LLM API, and whenever an exception or a surprising result names one.
---

# Tra API trước khi gọi

Một member gọi sai năm, sai overload, hay sai điều kiện trong Remarks thì build vẫn xanh và chỉ vỡ khi
host chạy. Tra **trước** dòng code gọi nó, và ghi lại — một lần tra không ghi là một lần tra không ai
kiểm được.

## Nguồn tra: `docsSource` trong `.claude/paper.profile.json`

| Dạng | Ví dụ | Cách tra |
|---|---|---|
| `mcp:<server>` | `mcp:<host>-docs` | gọi tool của server đó (search rồi read); server phải có trong `.mcp.json` |
| URL | `https://learn.microsoft.com/aspnet/core` | tải trang của đúng member, đúng phiên bản |
| `skill:<name>` | `skill:claude-api` | đọc skill đó trước khi viết lời gọi |
| `xmldoc:<folder>` | `xmldoc:<thư mục XML doc của SDK, đúng năm>` | tìm `name="M:Namespace.Type.Member` (T: type, P: property, M: method) trong `*.xml` của đúng năm, đọc `<summary>` và `<param>` |
| `assembly:<path>` | `assembly:<đường dẫn tới .dll của đúng cấu hình build>` | đọc chữ ký trong DLL — **không** có Remarks, nói rõ điều đó |

Gói host có thể thêm skill tra riêng với mẹo của nguồn đó (`<host>-api-lookup`); khi có, đọc nó sau skill này.

## Gói bên thứ ba: `context7`, `microsoft-docs`

`docsSource` trả lời cho **host của dự án** và framework nó chạy trên. Còn một gói bên thứ ba — test
runner, thư viện UI, công cụ build — thì hỏi `context7` nếu máy có plugin đó: `resolve-library-id`
(tên gói + câu hỏi) rồi `query-docs` trên library ID nhận được.

- **Ghi vào bảng "API đã tra" như mọi lần tra khác**, cột "Trang đã đọc" ghi **library ID** (`/nunit/nunit`)
  chứ không ghi "context7" — một lần tra phải lặp lại được.
- **Không dùng nó cho API của host.** Index của nó mỏng ở đúng chỗ host dày; gói host có số đo của riêng nó
  trong `<host>-api-lookup`. Host luôn đi qua `docsSource`.
- **Nó là dịch vụ ngoài.** Gửi tên thư viện và câu hỏi chung; không gửi code, tên ký hiệu hay tên khách
  hàng của dự án.
- `microsoft-docs` là nguồn chính thức cho nền .NET/Windows; đứng trước `context7` khi member thuộc về đó,
  và cũng ghi trang đã đọc vào bảng như mọi lần tra.
- Không có plugin thì bỏ qua mục này — các dạng nguồn ở bảng trên vẫn đủ.

## Mỗi member

1. **Đúng phiên bản** mà lần build này nhắm tới (năm hay phiên bản của host, phiên bản framework).
2. Đọc **Remarks và Exceptions**, không chỉ chữ ký — đó là chỗ có luật cắn: gọi hai lần thì throw,
   chỉ chạy trong transaction, chỉ trên UI thread.
3. **Một dòng trong bảng "API đã tra"** của plan (`YYYY-MM-DD-<task>-plan.md`), ghi trước dòng code gọi nó:

| Member | Phiên bản | Trang đã đọc | Điều cần nhớ |
|---|---|---|---|
| `HttpClient.SendAsync` | .NET 8 | learn.microsoft.com … | một `HttpRequestMessage` đã gửi thì gửi lại sẽ throw |

4. Member xuất hiện trong một exception hay một kết quả bất ngờ → tra, thêm dòng, **rồi** mới sửa.
5. **Hành vi mà tài liệu không nói** (gọi được từ ngữ cảnh nào, thread nào, có cần lock không) thì **đo** —
   chạy headless hay trong host đang mở — và ghi số đo vào chính dòng đó; một điều đoán không phải một lần tra.

## Bước kiểm cuối

`/task-verify` chạy `.claude/paperflow/paperflow.ps1 api-check -Path <plan>`. File code mà nhánh thêm dòng
(so với nhánh gốc, cả phần chưa commit) dùng namespace của host — cả file nhắc tới nó (một `using` có từ
trước cũng tính), hoặc project của file có `global using` / `<Using Include>` của nó — mà bảng "API đã tra"
trống thì **đỏ**, nêu từng file; việc chưa xong. Namespace của host chỉ lấy từ `api.namespaces` trong
profile (setup ghi từ template host; dự án setup trước đó tự khai); thiếu khoá thì bước này báo không áp
dụng kèm lời nhắc khai. Không tìm được nhánh gốc thì bước này báo **không kiểm được** (exit 2).
Bảng có dòng thì những `Type.Member` chưa dòng nào nhắc chỉ được **liệt kê** để người kiểm xem, không chặn.
Việc không gọi API host nào thì bảng để trống và bước này báo không áp dụng.

## Hai bẫy của `xmldoc:`

Đo trên XML doc của một SDK host thật (2026-09-16): 8941 member, **0 thẻ `<remarks>`**.

- **Chỉ có summary.** Luật cắn nằm lẫn trong summary (một member đòi object phải mới tạo *và* đã nằm
  trong database, chỉ nói trong một mệnh đề phụ). Đọc hết câu, và ghi `xmldoc — không có Remarks` vào cột trang.
- **Không thấy trong XML ≠ API không có.** Member dùng nhiều nhất của cả SDK có thể không có trong
  file. Không thấy thì đọc chữ ký trong DLL của đúng cấu hình build, và ghi cả hai lần tra.

## Khi nguồn không có

- MCP server không chạy / không cấu hình: tra từ assembly của đúng cấu hình build, và ghi vào cột "Trang
  đã đọc" là `assembly — không có Remarks`. Đừng dừng flow vì nó; đừng im lặng bỏ qua.
- Host chưa có docs server hay XML doc nào: `docsSource` ghi `assembly:<path>`, và coi đó là một
  khoảng trống đã biết, không phải lý do bỏ bước tra.
