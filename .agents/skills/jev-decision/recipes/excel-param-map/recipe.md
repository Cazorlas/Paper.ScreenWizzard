# excel-param-map — ghép cột Excel của tư vấn vào parameter Revit

Công thức thứ hai, chép hình của `layer-map`. Chưa chạy trong add-in nào: số dưới đây chỉ đo trên fixture bịa.

## Khi nào dùng

Bảng thiết bị của tư vấn mỗi nơi một kiểu tiêu đề (`Lưu lượng (m³/h)`, `Q`, `Airflow` với đơn vị ở hàng dưới, hai cột
cùng tên `Size`), nên bảng ghép cột viết tay cho từng dự án hỏng ngay ở dự án sau. Khi import một bảng Excel vào
Revit, công thức này đọc từng cột và gắn hai nhãn kèm nguồn và độ tin: cột ghi vào parameter nào của category đang
import (fixture: Mechanical Equipment), và giá trị của cột viết theo đơn vị nào. Import chỉ ghi cột đã có nhãn đủ tin;
phần còn lại vào "cần xem".

## State gửi Jev

Một dòng cho mỗi cột: `header: <tiêu đề> | samples: <v1>; <v2>; <v3>`.

- Tiêu đề có chữ Việt có dấu thì thêm `| header plain: <tiêu đề bỏ dấu>` ngay sau `header`. Bỏ dấu bằng FormD và
  `đ` → `d`, nên `³` vẫn là `³`.
- Ô gộp phía trên cột (tiêu đề nhóm) đứng trước: `header: <nhóm> / <tiêu đề>`.
- Mẫu là 3 ô khác rỗng đầu tiên dưới hàng tiêu đề. Ô là số thì giữ nguyên như viết; ô còn lại cắt còn 20 ký tự.
- Vì thế hàng đơn vị dưới tiêu đề hiện ra như mẫu đầu tiên (`samples: L/s; 950; 1400`). Sản phẩm không phải đoán
  hàng nào là hàng đơn vị; câu hỏi bảo Jev đọc mẫu đầu.

Không nằm trong state đã đo: tên file, tên sheet, cột khác, số dòng, ô thứ tư trở đi, đường dẫn, tên dự án, tên người dùng.
Category không gửi: nó chỉ dùng để chọn bộ câu hỏi.

## Câu hỏi

`questions.json` là hai câu `choice`, bộ câu hỏi của category Mechanical Equipment:

- `target_parameter`: 19 lựa chọn. Bảy parameter dựng sẵn của Revit, tên tiếng Anh đúng như giao diện (`Mark`,
  `Type Mark`, `Model`, `Manufacturer`, `Description`, `Comments`, `Cost`). Mười một parameter hiệu năng và kích thước
  đặt tên như shared parameter hay family parameter thường gặp của thiết bị cơ điện (`Airflow`, `Water Flow`,
  `Cooling Capacity`, `Heating Capacity`, `Power`, `Voltage`, `External Static Pressure`, `Width`, `Height`, `Length`,
  `Weight`). Và `(do not import)`: cột số thứ tự, kích thước gộp `1200x600x450`, cột áp bơm, dòng điện không có
  parameter nào, ép vào một parameter là ghi sai dữ liệu. Parameter chỉ đọc (`System Name`, `Panel`…) không có trong
  danh sách vì import không ghi được vào chúng.
- `unit`: 8 lựa chọn: `mm`, `m`, `m3/h`, `L/s`, `kW`, `none`, cộng hai lựa chọn riêng. `other` là có ghi đơn vị nhưng
  ngoài danh sách (V, Pa, kg, HP, BTU/h, CFM…). `not stated` là đại lượng vật lý mà không ghi đơn vị. Cả hai đều nghĩa
  là import hỏi người dùng, không đổi đơn vị.

**Câu hỏi đã đo theo category.** Danh sách parameter đổi theo category, nhưng câu hỏi gửi Jev không sinh lúc chạy.
Mỗi category có một bộ câu hỏi đã đo riêng, gửi nguyên văn từng byte (≤ 255 lựa chọn mỗi câu); `questions.json` này
là bộ của Mechanical Equipment. Danh sách parameter sinh lúc chạy (parameter ghi được của category, instance và type,
gộp theo tên) chỉ dùng ở máy, ở hai chỗ: luật khớp đúng tên trước Jev, và luật (a) sau Jev. Category chưa có bộ câu
hỏi đã đo thì `target_parameter` chỉ chạy luật, không gọi Jev. Thêm, bớt hay sửa chữ một lựa chọn là một câu hỏi mới:
đo lại trên fixture của category đó. Hai cách bị loại:

- Sinh `criteria` từ tên parameter lúc chạy: chữ gửi đi chưa từng được đo, và test không có file nào để so thân yêu cầu.
- Gửi phần của danh sách đã đo mà dự án có: bộ lựa chọn nhiễu đổi theo từng dự án, nên con số đã đo không còn tả câu
  hỏi gửi đi, và thân yêu cầu lệch file.

## Luật trước và sau Jev

- **Trước Jev:**
  - (0) Tiêu đề (bỏ dấu, chữ thường) trùng đúng tên một parameter của danh sách lúc chạy thì lấy parameter đó, nguồn
    `rules`, độ tin 0.95. Luật này không có trong `rules.json` vì danh sách chỉ có lúc chạy, nên fixture không đo nó.
  - (1) `rules.json`. Luật khớp từ khoá trên cả dòng state đã về chữ thường, bỏ dấu; luật đầu tiên khớp thì thắng.
    Mặc định `(do not import)` và `none`, độ tin 0.30.
- **Sau Jev (luật nhất quán, chỉ đụng nhãn có nguồn `jev`):**
  - (a) Jev chọn parameter mà danh sách lúc chạy của dự án không có thì coi như Jev không trả lời; luật cũng không có
    thì `(do not import)` và "cần xem".
  - (b) Hai cột cùng ra một parameter thì cả hai vào "cần xem".
  - (c) Parameter chữ (`Mark`, `Type Mark`, `Model`, `Manufacturer`, `Description`, `Comments`) mà `unit` khác `none`
    thì `unit` thành `none`. Parameter đại lượng mà `unit` là `none` thì thành `not stated`, độ tin 0.30. Đơn vị không
    hợp parameter thì "cần xem", độ tin 0.30: lưu lượng chỉ nhận `m3/h`, `L/s`, `other`, `not stated`; công suất `kW`,
    `other`, `not stated`; kích thước `mm`, `m`, `other`, `not stated`.
  - (d) `other` và `not stated` không bao giờ được đổi đơn vị: import hỏi người dùng.
- Độ tin dưới 0.70 thì đưa vào "cần xem".

## Port

`IColumnMapJudge.JudgeAsync(ColumnFacts) → ColumnJudgement`. `ColumnFacts` chỉ chứa `Header`, `HeaderPlain` và
`Samples` (≤ 3, đã che), tức là các trường ở mục State; category đi riêng, chỉ để chọn bộ câu hỏi.
`ColumnQuestionCatalog.For(category)` trả bộ câu hỏi đã đo, hoặc không có gì (khi đó chỉ chạy luật).
`SampleMaskPolicy` che mẫu. `JevSwitchPolicy.Decide(switch, typeSafeKey, openRouterKey)` chọn đường gọi, như
`layer-map`. Công tắc tắt là `PAPER_EXCELMAP_JEV=0` (Jev bật mặc định). `ColumnMapConsistencyPolicy` (a)–(d) chạy trong interactor, sau khi
đã gộp nhãn. Test so thân yêu cầu với file câu hỏi của đúng category.

## Đo

Fixture có 34 dòng bịa: 22 dòng tiêu đề rõ nghĩa (đơn vị nếu có nằm trong tiêu đề, viết ASCII), và 12 dòng khó theo
mẫu lỗi của bảng thiết bị thật: `Lưu lượng (m³/h)` tiếng Việt có `³`; `Q` một chữ không đơn vị; `Q` dưới nhóm
`Làm lạnh` là nhiệt; đơn vị ở hàng dưới tiêu đề; hai cột cùng tên `Size` (công suất động cơ và kích thước gộp); cột
ghi chú có số và đơn vị, tiếng Việt và tiếng Anh; cột `STT`; `H` là cột áp bơm; công suất lạnh theo BTU/h;
`Model No.` mà chữ `No.` kéo về `Mark`.

`rules: 29/34 rows, hard 7/12`
`jev (typesafe/jev-1.13, 2026-10-01): 33/34 rows, hard 11/12`

Một lần chạy qua OpenRouter trên fixture bịa; câu Jev không trả lời thì lấy của luật; chưa áp luật sau Jev (a)–(d).
Dòng khó Jev vẫn sai: EP32. Đó là cột `H` với hàng đơn vị `m`: cột áp bơm theo mét, không phải chiều cao của thiết bị.

Luật và fixture cùng người viết: số dev, không phải holdout.

## Nguồn

- Spreadsheets that read intent: https://x.com/dabit3/status/2100780008193020049
- Semantic spreadsheet formatting: https://x.com/GuangyuRobert/status/2100601420395282695
