# titleblock-fill — điền title block hoặc parameter từ dữ liệu bẩn

Công thức chép hình của `layer-map` và `excel-param-map`. Chưa chạy trong add-in nào: số dưới đây chỉ đo trên fixture
bịa.

## Khi nào dùng

Khi điền khung tên của sheet Revit (parameter của sheet) hay block khung tên CAD (attribute) từ một nguồn bẩn: bảng
danh mục bản vẽ, khung tên của bộ bản vẽ cũ, hay chữ trích từ PDF. Dữ liệu nguồn hay sai theo vài mẫu lặp lại: ngày
đọc được hai cách, giá trị nằm sai cột, nhãn dính trong giá trị, hai người một ô, chữ mặc định của template còn
nguyên, chữ vỡ mã. Với mỗi cặp "ô đích + một giá trị", công thức hỏi hai câu: giá trị thuộc ô nào, và có ghi nguyên
được vào ô đích không.

Mọi giá trị chỉ là **đề xuất** cho người duyệt. Jev không bao giờ ghi, và không có đường nào tới lệnh ghi. Host:
`revit`, `cad`.

## State gửi Jev

Một dòng cho mỗi cặp ô: `target: <tên ô đích> | value: <giá trị đã che>`.

- Tên ô đích viết như trong khung tên: tên parameter của sheet (`Drawn By`, `Sheet Issue Date`) hay tag/prompt
  attribute CAD (`DRN`, `REV`, `NGÀY`), không đổi sang tên chuẩn.
- Trường nào đổi khi bỏ dấu (FormD, bỏ dấu thanh, `đ` → `d`, giữ hoa thường) thì thêm `| target plain: …` hay
  `| value plain: …` ngay sau nó. Chữ vỡ mã cũng theo luật này.
- Ô nguồn rỗng gửi `value: (empty)`; ô chỉ có `-` gửi nguyên `-`.

**Che tên (`NameMaskPolicy`, ở máy, trước khi gửi):**

1. Tên trong danh sách ở máy (Project Information: Author, Client Name, Organization Name; danh sách người của dự án
   người dùng giữ) thành `[PERSON-n]` hay `[COMPANY-n]`, đánh số theo thứ tự xuất hiện trong giá trị.
2. Khi ô đích là ô người (theo bản đồ khung tên: drawn, checked, designed, approved), **mọi** cụm chữ cái không nằm
   trong danh sách giữ lại cũng thành `[PERSON-n]`. Danh sách giữ lại: chữ mặc định của template (`Author`, `Checker`,
   `Designer`, `Approver`), chữ giữ chỗ (`TBC`, `TBA`, `N/A`, `xx`), nhãn ô (`DRAWN`, `CHECKED`, `DESIGNED`,
   `APPROVED`, `BY`, `VẼ`, `KIỂM TRA`, `THIẾT KẾ`, `DUYỆT`) và từ nối (`and`, `và`, `&`). Chữ số, dấu và ngày giữ
   nguyên. Tên viết tắt (`NVA`) cũng là cụm chữ cái nên bị che.

Che tên là hình state mà fixture đã đo, không còn là luật riêng tư: gửi tên thật được (chủ dự án đồng ý 2026-10-04)
nhưng là đổi state và phải đo lại. Gửi "hình" của giá trị thay cho chữ thì Jev không còn thấy nhãn như CHECKED hay hai
tên trong một ô, đúng mẫu lỗi cần bắt, nên cách đó bị loại.

**Bản đồ khung tên (`TitleBlockMap`, ở máy):** ô đích → một trong chín ô hay "không biết". Revit: parameter dựng sẵn
của sheet đã biết nghĩa. CAD: bản đồ tag → ô theo từng block, người dùng xác nhận một lần rồi lưu. Bản đồ dùng cho che
tên (2) và luật (a) sau Jev; nó không gửi đi.

Không nằm trong state đã đo: bảng nguồn, ô khác của cùng dòng, tiêu đề cột nguồn, tên file, tên sheet nguồn, đường dẫn, tên
người dùng, tên người hay công ty thật. Các ô thông tin dự án (Project Name, Client Name, Project Address, Project
Number) **không bao giờ** được hỏi: chúng lấy từ Project Information, không từ import.

## Câu hỏi

`questions.json` là hai câu `choice`:

- `field`: 10 lựa chọn: `sheet number`, `sheet name`, `drawn by`, `checked by`, `designed by`, `approved by`, `date`,
  `revision`, `scale`, `none`. `designed by` và `approved by` có vì sheet Revit có sẵn bốn parameter người (Drawn By,
  Checked By, Designed By, Approved By) và khung tên Việt có "Thiết kế", "Chủ nhiệm/Chủ trì"; với tám lựa chọn, ô
  "Thiết kế" bị ép vào `drawn by` hay `none`, đúng lỗi công thức muốn bắt. `approved by` gồm cả Chủ nhiệm, Chủ trì
  (người ký cuối). `none`: cả giá trị lẫn đích không thuộc chín ô (khổ giấy, zone, trạng thái hay suitability).
  - **Đọc giá trị trước, ô đích sau.** Nội dung rõ ràng thuộc ô khác (mã revision trong ô ngày, giá trị có nhãn
    CHECKED trong ô Drawn) thì chọn ô của nội dung. Nhãn đứng **trước** giá trị nói giá trị thuộc ô nào; chữ hay giá
    trị thứ hai đứng **sau** (chữ trạng thái sau mã revision, revision sau số sheet) thì không: ô là ô của giá trị mở
    đầu, và `value_ok` là `needs fixing`. Giá trị rỗng hay chữ giữ chỗ thì chọn ô mà đích đại diện. Chữ này viết lại
    2026-10-01 sau lần đo đầu: bản cũ lấy "số sheet kèm revision" làm ví dụ nội dung thuộc ô khác, mâu thuẫn với TF40,
    nơi ô đích chính là số sheet. Nhãn TF40 giữ nguyên (`sheet number` / `needs fixing`); chữ của `value_ok` không đổi.
- `value_ok`: 3 lựa chọn: `yes`, `needs fixing`, `missing`, chấm giá trị **so với ô đích** ("ghi nguyên vào ô đích
  được không"). Nội dung thuộc ô khác thì `needs fixing`. Hai câu độc lập: Jev không thấy câu trả lời kia.
  - **Ngày:** `yes` chỉ khi ngày đọc được một cách: `yyyy-mm-dd`, năm bốn chữ số với ngày > 12, hay tháng viết bằng
    chữ hoặc có nhãn (`5 Jun 2026`, `ngày 05 tháng 06 năm 2026`). Ngày và tháng đều ≤ 12, năm hai chữ số, hay số
    serial của bảng tính → `needs fixing`. Tư vấn Việt viết dd/mm, tư vấn nước ngoài viết mm/dd: máy không biết nguồn
    theo kiểu nào nên không được đoán.
  - **`1:100 @A1` là `yes`:** tỷ lệ kèm khổ giấy là một giá trị tỷ lệ trọn vẹn, không có gì cho người sửa.
  - **`missing`** gồm chữ mặc định của template còn nguyên (`Author`, `Checker`, `Designer`, `Approver`, `Unnamed`,
    `MM/DD/YY`, tên của chính ô đích) cùng `(empty)`, `-`, `TBC`, `TBA`, `N/A`, `xx`.

Ví dụ trong `criteria` cố ý khác giá trị của dòng khó (`05/06/2026`, `P02 - FOR TENDER`, `46178` trong criteria;
`10/01/2026`, `C01 - FOR CONSTRUCTION`, `46296` trong fixture), để số Jev không đo việc chép lại chuỗi.

## Luật trước và sau Jev

- **Trước Jev:**
  - (0) Ô nguồn rỗng → `value_ok` = `missing`, `field` theo bản đồ khung tên, nguồn `rules`, không gọi Jev. Ô đích
    thuộc thông tin dự án → không hỏi.
  - (1) `rules.json`: khớp từ khoá trên cả dòng state đã về chữ thường, bỏ dấu; luật đầu tiên khớp thì thắng. `field`
    theo tên ô (số và tên sheet trước, rồi bốn ô người, rồi ngày, revision, tỷ lệ), mặc định `none` 0.30. `value_ok`
    chỉ có luật `missing` (0.80), mặc định `yes` 0.50, dưới ngưỡng nên vào "cần xem". Không có luật `needs fixing`:
    khớp từ khoá không đọc được hình của một ngày hay của chữ vỡ mã, đó chính là chỗ Jev phải giúp.
- **Sau Jev (chỉ đụng nhãn có nguồn `jev`):**
  - (a) `field` khác ô mà bản đồ khung tên gán cho đích → "cần xem" (giá trị nằm sai ô), không ghi vào ô nào cho tới
    khi người chuyển nó.
  - (b) `value_ok` = `yes` cho một ngày mà bộ đọc ngày ở máy thấy đọc được hai cách → `needs fixing`, nguồn `rules`.
  - (c) Revit: `revision` và `scale` không bao giờ được import ghi (Current Revision lấy từ các revision gắn vào sheet,
    Scale từ các view); plan hiện giá trị nguồn cạnh giá trị của model cho người xem. CAD: attribute là chữ, ghi được
    sau khi duyệt.
  - (d) Mọi dòng, kể cả `yes` độ tin 0.99, vào **plan duyệt**. `needs fixing` mở ô cho người sửa; `missing` giữ nguyên
    ô đích.
- Độ tin dưới 0.70 thì đưa vào "cần xem".
- Ghi là một lệnh riêng sau khi người duyệt: chỉ những dòng đã duyệt, Revit trong một transaction, CAD trong một nhóm
  undo. Jev không có đường nào tới lệnh ghi.

## Port

`ITitleBlockFieldJudge.JudgeAsync(TitleBlockFieldFacts) → TitleBlockFieldJudgement`.
`TitleBlockFieldFacts(Target, TargetPlain, Value, ValuePlain)` chỉ chứa các trường ở mục State; `Value` đã qua
`NameMaskPolicy`. `TitleBlockMap` ở máy. Công tắc tắt là `PAPER_TITLEBLOCK_JEV=0` (Jev bật mặc định); `JevSwitchPolicy.Decide(switch,
typeSafeKey, openRouterKey)` chọn đường gọi, như `layer-map`. `TitleBlockFillConsistencyPolicy` áp (a)–(d) sau khi đã
gộp nhãn; interactor trả `TitleBlockFillPlan`; `ApplyTitleBlockFillPlan` chỉ ghi dòng đã duyệt. Test so thân yêu cầu
với `questions.json`.

## Đo

Fixture có 41 dòng bịa: 26 dòng thường (chín ô với tên đích tiếng Anh và tiếng Việt, giá trị sạch, bốn ô chữ mặc định hay giữ
chỗ, hai ô `none`), và 15 dòng khó theo mẫu lỗi của khung tên thật: ngày `01/10/26` và `10/01/2026` (ngày và tháng đều
≤ 12); `1:100 @A1`; `P03` dưới `REV` và dưới `Date`; `C01 - FOR CONSTRUCTION`; hai người trong ô Drawn; `CHECKED:`
trong ô Drawn By; tên sheet tiếng Việt có dấu chứa "KIỂM TRA"; tên sheet tiếng Việt vỡ mã (UTF-8 đọc như
Windows-1252); `Checker` còn nguyên trong Checked By; ngày là số serial của bảng tính; người dưới `THIẾT KẾ`; số sheet
kèm `REV C02`; `ngày 01 tháng 10 năm 2026`.

`rules: 32/41 rows, hard 6/15`
`jev (typesafe/jev-1.13, 2026-10-01): 40/41 rows, hard 14/15`

Một lần chạy qua OpenRouter trên fixture bịa, sau khi đổi chữ của `field`; câu Jev không trả lời thì lấy của luật;
chưa áp luật sau Jev (a)–(d). Dòng khó Jev vẫn sai: TF40 (theo từng câu: `field` 40/41, `value_ok` 40/41). 41 lời
gọi, 0 hỏng, 68869 token đầu vào, 23.7 s. TF40 (`M-101 REV C02` dưới `DWG NO`) vẫn sai, và là dòng sai duy nhất nên
sai cả `field` lẫn `value_ok`: câu mới không đổi được câu trả lời của Jev.

Lần đo đầu (2026-10-01, chữ cũ của `field`): jev 40/41 rows, hard 14/15; dòng khó còn sai TF40 (`M-101 REV C02`
dưới `DWG NO`: chữ REV kéo về revision, sai cả `field` lẫn `value_ok`). `instructions` của `field` đổi 2026-10-01 sau
lần đo đầu; số đo lại không sạch như số đầu ở TF40, vì câu mới chép đúng mẫu của dòng đó.

Luật và fixture cùng người viết: số dev, không phải holdout.

## Nguồn

- Dirty-field PDF form fill: https://x.com/bendersej/status/2100960073853935630
