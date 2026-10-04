# sheet-classify — cắt bộ PDF bản vẽ thành sheet, gắn bộ môn và loại sheet

Công thức thứ hai của skill. Mỗi trang PDF của một bộ bản vẽ được hỏi ba câu: thuộc bộ môn nào, là loại bản vẽ nào,
và có bắt đầu một sheet mới không.

## Khi nào dùng

Một bộ PDF bản vẽ hay một hồ sơ trình duyệt cần cắt thành từng sheet, để import vào Revit hay để đặt tên file theo
số và tên sheet. PDF gộp nhiều trang nên phải biết trang nào mở sheet mới, trang nào nối tiếp trang trước. Host:
revit, cad.

## State gửi Jev

Một dòng chữ cho mỗi trang, năm trường theo thứ tự cố định:

`sheet number: <số> | sheet name: <tên> | scale: <tỷ lệ> | previous page: <so sánh> | page text: <chữ>`

- Trường thiếu ghi `-`.
- `previous page` do code tính trước khi hỏi, từ số sheet của trang này và trang trước. Nó là một trong bốn giá trị
  `first page`, `same number`, `other number`, `no number on this page`. Gửi nguyên số sheet của trang trước thì luật
  từ khoá (khớp trên cả state) đọc nhầm tiền tố của trang trước, và chữ rời máy nhiều hơn.
- `page text` là tối đa 300 ký tự đầu sau OCR của vùng bản vẽ ngoài khung tên.
- Khung tên chỉ đọc ba ô: số sheet, tên sheet, tỷ lệ.

Không nằm trong state đã đo: tên dự án, chủ đầu tư, tên khách, địa chỉ, tên người vẽ hay người kiểm, đường dẫn và tên file PDF.

## Câu hỏi

`questions.json` là ba câu `choice`, gửi nguyên văn:

- `discipline`: 8 lựa chọn (Architecture, Structure, Mechanical, Plumbing, Electrical, Fire protection, General,
  Other), trùng tên với bộ phân loại sheet của một add-in Revit của Paper.
- `drawing_type`: 10 lựa chọn, chín tên của bộ phân loại đó cộng `schematic`. Bộ bản vẽ cơ điện đầy sơ đồ nguyên lý,
  sơ đồ đơn tuyến, sơ đồ trục đứng; không có `schematic` thì chúng rơi vào `other`.
- `starts_new_document`: 2 lựa chọn (`yes`, `no`). Một tài liệu là một sheet, hay một văn bản, bảng, chỉ dẫn kỹ thuật
  trong bộ hồ sơ. Số sheet mới luôn là sheet mới, kể cả khi tên ghi "continued". Trang không có số mà chữ nối tiếp
  bảng hay chữ của trang trước là `no`.

Mô tả lựa chọn nêu phần tử, mã tiền tố hay gặp (kể cả tiền tố Việt KT, KC, DH, TG, CTN, D, PCCC) và tên Việt. Ba chỗ
cố ý:

- Báo cháy thuộc Fire protection, như add-in kia, như hồ sơ PCCC ở Việt Nam và như `layer-map` (đổi 2026-10-01 theo
  lời chủ dự án). Dự án không có bộ môn Fire protection thì luật (f) sau Jev xếp trang báo cháy vào Electrical.
- Bản vẽ phối hợp cơ điện là Other.
- Bìa hay danh mục là General, kể cả khi liệt kê sheet của nhiều bộ môn.

Không có câu `level`: lựa chọn của nó là tên tầng của từng dự án, dựng lúc chạy, nên chữ gửi nguyên văn không chứa
được. Sản phẩm thêm câu `level` như add-in kia đã làm; công thức không đo nó.

## Luật trước và sau Jev

- **Trước Jev:** `rules.json`, bảng mặc định của bộ phân loại sheet kia chép sang khuôn kit. Luật khớp từ nguyên vẹn
  trên toàn bộ state đã bỏ dấu, viết thường; luật đầu tiên khớp thì thắng. Tiền tố số sheet viết thành cụm
  `sheet number <tiền tố>` và đứng trước luật tên sheet (tiền tố thắng), độ tin 0.8; luật tên 0.7; không manh mối thì
  `Other` / `other` độ tin 0. Ranh giới: `no` khi có `same number`, `cont`, `continued`, `tiep theo`; mặc định `yes`
  độ tin 0.6, tức là trang không có dấu hiệu nào vào "cần xem". Bộ đếm trang (`2/3`) không thành luật được: khớp từ
  nguyên vẹn không biểu diễn được "n trên m".
- **Sau Jev:**
  - (a) `previous page: first page` thì `starts_new_document` = `yes`, nguồn `rules`.
  - (b) Jev trả `no` mà `previous page: other number` thì đổi thành `yes`, nguồn `rules`: số sheet mới là sheet mới.
  - (c) Jev trả `yes` mà `previous page: same number` thì giữ, độ tin 0.30, tức là vào "cần xem".
  - (d) Jev trả `drawing_type` = `cover/index` thì `discipline` = `General`, nguồn `rules`.
  - (e) Trang `no` nhập vào tài liệu của trang trước. Bộ môn và loại của tài liệu là của trang đầu; nhãn của trang
    nối tiếp chỉ để người xem đối chiếu.
  - (f) Tài liệu có bộ môn `Fire protection` mà danh sách bộ môn của dự án không có Fire protection: nếu `sheet name`
    hay `page text` của trang đầu, đã về chữ thường bỏ dấu, có một trong các cụm `bao chay`, `bao khoi`, `bao nhiet`,
    `fire alarm`, `smoke detector`, `heat detector`, `call point` và không có cụm chữa cháy nào (`chua chay`,
    `sprinkler`, `dau phun`, `hydrant`, `hose reel`, `vach tuong`) thì bộ môn đổi thành `Electrical`, nguồn `rules`,
    độ tin giữ nguyên; còn lại (trang chữa cháy, hay trang gộp chữa cháy và báo cháy) giữ nhãn và vào "cần xem".
    Danh sách bộ môn là dữ kiện của máy, không gửi Jev, nên (f) đụng cả nhãn nguồn `rules` lẫn `jev`, chạy cả khi
    Jev tắt, và chạy sau (e). Chủ dự án chốt luật lùi này cho báo cháy ngày 2026-10-01. Chưa đo: fixture không mô
    phỏng danh sách bộ môn.
- Độ tin dưới 0.70 thì đưa vào "cần xem".

Không để Jev tự quyết ranh giới khi số sheet đổi: một câu hỏi độc lập có thể mâu thuẫn với sự thật code đã biết.

## Port

`ISheetPageJudge.JudgeAsync(SheetPageFacts) → SheetPageJudgement`. `SheetPageFacts(SheetNumber, SheetName, Scale,
PreviousPage, PageText)` chỉ chứa năm trường của mục State; `PreviousPage` do một policy thuần trong UseCases tính.
`JevSwitchPolicy.Decide(switch, typeSafeKey, openRouterKey)` chọn đường gọi như `layer-map`. Công tắc tắt là
`PAPER_SHEETCLASSIFY_JEV=0` (Jev bật mặc định). `SheetPageConsistencyPolicy` áp (a)–(e) sau khi đã gộp nhãn; sau nó `ProjectDisciplinePolicy` áp (f) với danh sách bộ
môn của dự án làm đầu vào.

Bộ phân loại sheet đã có trong một add-in Revit của Paper gửi Jev khi có khoá, nhưng chỉ đọc khoá TypeSafe và chưa có
công tắc tắt. Port công thức vào đó phải thêm đường OpenRouter (đường chính) và công tắc tắt theo luật 2; đó là việc
của kho add-in, không làm ở kit.

## Đo

33 dòng bịa: 20 dòng thường, 13 dòng khó (số không tiền tố, phối hợp cơ điện, bìa có chữ Electrical, báo cháy tiếng
Việt, trang nối tiếp của bảng không khung tên, chữ "continued" trên số sheet mới, trang 2/3 của chỉ dẫn kỹ thuật, chi
tiết chỉ nhận ra qua tỷ lệ, tiền tố Việt `MC`, "sơ đồ hệ thống", "trần" trong ghi chú của mặt bằng ống gió).

`rules: 20/33 rows, hard 0/13`
`jev (typesafe/jev-1.13, 2026-10-01): 32/33 rows, hard 13/13`

Một lần chạy qua OpenRouter trên fixture bịa; câu Jev không trả lời thì lấy của luật; chưa áp luật sau Jev (a)–(e).
Dòng khó Jev vẫn sai: không có. Cả 13 dòng khó đúng đủ ba câu, kể cả SC28 ("continued" trên số sheet mới), SC23 và
SC24 (phối hợp cơ điện là Other) và SC29 (trang 2/3 của chỉ dẫn kỹ thuật là `no`). Dòng sai duy nhất là một dòng
thường, ở câu `drawing_type` (theo từng câu: `discipline` 33/33, `drawing_type` 32/33, `starts_new_document` 33/33).
33 lời gọi, 0 hỏng, 56 692 token đầu vào, 16.8 s.

Số trên dữ liệu thật: chưa có.

Mô tả lựa chọn và dòng khó do cùng một người viết trong một lượt, nên số trên fixture là số dev, không phải holdout.

## Nguồn

- Lựa chọn và bảng từ khoá lấy từ bộ phân loại sheet của một add-in Revit của Paper (2026-09-26); nguồn cụ thể ở sổ
  nghiên cứu của kho kit.
- DocJev, cắt và phân loại tài liệu bằng Jev: https://x.com/jerryjliu0/status/2101738281046294552
- Proq phân loại 26 sheet trong 2.9 s với $0.0052: https://x.com/hari_trinay/status/2101118529936519453
