# layer-map — gắn bộ môn, loại phần tử và hệ thống MEP cho mọi lớp CAD

Đây là công thức mẫu: các công thức khác chép hình của nó. Đã chạy thật trong một add-in AutoCAD của Paper
(merge 2026-09-29).

## Khi nào dùng

Bản vẽ CAD của tư vấn đặt tên lớp mỗi nơi một kiểu (`M-SEA-DUCT`, `B&D-ME-M-HVAC`, `B&D-M-Duct Fittings`), nên
bảng tên lớp viết tay cho từng dự án hỏng ngay ở dự án sau. Công thức này đọc mọi lớp, gắn ba nhãn kèm nguồn và độ
tin, rồi lưu thành một bản đồ lớp mà phía Revit đọc được (lớp → category và system type khi dựng model).

## State gửi Jev

Một dòng cho mỗi lớp: `layer: <tên lớp> | colour <n> | linetype <tên> | <số đối tượng theo loại> | blocks: <tối đa 5 tên block>`.

Không nằm trong state đã đo: hình học, toạ độ, đường dẫn file bản vẽ, tên dự án, tên người dùng, tên xref.

## Câu hỏi

`questions.json` là ba câu `choice`, lấy từ bộ câu hỏi đã chỉnh (lượt 2) của một add-in AutoCAD của Paper, rồi đổi
2026-10-01 sau lần đo đầu theo lời chủ dự án: mô tả `discipline` chuyển báo cháy từ Electrical sang Fire protection,
và mô tả `mep_system` nói rõ `none` là lớp không có hệ ống hay ống gió, kể cả báo cháy. Add-in kia chép thay đổi này
ngày 2026-10-01: bộ câu hỏi nó gửi Jev giờ bằng từng byte `questions.json` (so 2026-10-03).

- `discipline`: 8 lựa chọn.
- `element_kind`: 13 lựa chọn.
- `mep_system`: 14 lựa chọn, có hai lựa chọn riêng. `none` dùng cho lớp không thuộc hệ cơ điện. `not known` dùng
  cho lớp cơ điện mà không nói được hệ thống.
- Báo cháy (đầu báo khói, đầu báo nhiệt, nút ấn, chuông còi, tủ báo cháy) là Fire protection, kể cả khi người vẽ
  điện đặt nó trên lớp `E-`; hệ thống của nó là `none`. Dự án không có bộ môn Fire protection thì luật (d) sau Jev xếp
  nó vào Electrical.

## Luật trước và sau Jev

- **Trước Jev:** `rules.json`. Luật khớp từ khoá trên chữ thường đã bỏ dấu, luật đầu tiên khớp thì thắng. Lớp `0`
  và `Defpoints` cố định trong sản phẩm, nên không có trong fixture.
- **Sau Jev:** (a)–(c) là luật nhất quán, chỉ đụng nhãn có nguồn `jev`; (d) áp dữ kiện của dự án.
  - (a) Bộ môn không thuộc cơ điện thì hệ thống là `none`.
  - (b) Bộ môn Architecture, Structure, Electrical hoặc General mà loại phần tử là ống gió, ống, phụ kiện hoặc
    miệng gió thì loại đổi thành `other`. Luật này sửa mẫu lỗi `S-BEAM`.
  - (c) Bộ môn Mechanical/HVAC hay Plumbing/Drainage mà Jev trả `none` thì đổi thành `not known` với độ tin 0.30,
    tức là đưa vào "cần xem". Bộ môn Fire protection cũng vậy khi loại phần tử là `pipe` hay `fitting/accessory`.
    Lớp Fire protection khác mà hệ thống là `none` là báo cháy, theo mô tả lựa chọn.
  - (d) Bộ môn Fire protection với hệ thống `none` (báo cháy, sau (c)), mà danh sách bộ môn của dự án không có Fire
    protection, thì bộ môn đổi thành Electrical, nguồn `rules`, độ tin giữ nguyên. Danh sách bộ môn là dữ kiện của
    máy (cấu hình dự án), không gửi Jev, nên luật này đụng cả nhãn nguồn `rules` lẫn `jev` và chạy cả khi Jev tắt.
    Lớp chữa cháy có ống (sprinkler, họng nước) khi dự án không có Fire protection thì giữ nhãn và vào "cần xem":
    chủ dự án chỉ chốt luật lùi cho báo cháy (2026-10-01). Trên fixture bịa luật này chưa đo: fixture và
    `scripts/jev-eval.ps1` không mô phỏng danh sách bộ môn. Trong add-in nó đã được chấm lại offline trên dữ liệu thật
    (2026-10-03, mục Đo).
- Độ tin dưới 0.70 thì đưa vào "cần xem".

## Port

`ILayerJudge.JudgeAsync(LayerFacts) → LayerJudgement`. `LayerFacts` chỉ chứa các trường ở mục State.
`JevSwitchPolicy.Decide(switch, typeSafeKey, openRouterKey)` chọn đường gọi. Công tắc tắt là `PAPER_LAYERMAP_JEV=0` (Jev bật mặc định).
`JevAnswerConsistencyPolicy` áp (a)–(c) trong interactor, sau khi đã gộp nhãn; sau nó `ProjectDisciplinePolicy` áp (d)
với danh sách bộ môn của dự án làm đầu vào.

## Đo

Fixture có 45 dòng bịa: 30 dòng tên theo chuẩn AIA hoặc ISO, và 15 dòng khó lấy từ các mẫu lỗi đã gặp trên bản vẽ
thật (ống gió trong shaft, cable duct, fire alarm, tên lớp tiếng Việt và tiếng Trung, refrigerant, LPG).

`rules: 31/45 rows, hard 1/15`
`jev (typesafe/jev-1.13, 2026-10-01): 33/45 rows, hard 11/15`

Một lần chạy qua OpenRouter trên fixture bịa, sau khi đổi chữ báo cháy; câu Jev không trả lời thì lấy của luật; chưa
áp luật sau Jev (a)–(d). Dòng khó Jev vẫn sai: LM31, LM34, LM36, LM37 (theo từng câu: `discipline` 43/45,
`element_kind` 38/45, `mep_system` 37/45). 45 lời gọi, 0 hỏng, 100121 token đầu vào, 25.6 s. LM33 (báo cháy, nhãn
mới Fire protection / `fixture/device` / `none`) đúng cả ba câu.

Lần đo đầu (2026-10-01, báo cháy còn là Electrical trong câu hỏi, trong luật từ khoá và trong nhãn LM20, LM33): jev
33/45 rows, hard 11/15; dòng khó còn sai LM31, LM34, LM36, LM37. Mô tả `discipline` và `mep_system`, nhãn LM20 và
LM33, và luật `fa`, `alarm`, `detector` đổi 2026-10-01 sau lần đo đầu, theo lời chủ dự án; số đo lại không sạch như
số đầu, vì chữ mới được viết khi đã thấy kết quả lần đầu (câu "also … on an E- layer" đúng vào LM20).

Số trên dữ liệu thật trong add-in đó, holdout 58 dòng, đúng cả ba nhãn. Chấm lại offline ngày 2026-10-03 trên nhãn mới
(báo cháy là Fire protection ở dự án có bộ môn đó, Electrical ở dự án không có), từ các lượt Jev đã lưu ngày
2026-09-29; không gọi Jev lần nào mới:

| Cách | Đúng | Trên nhãn cũ (2026-09-29) |
| --- | --- | --- |
| luật | 43 % (25/58) | 43 % |
| Jev, câu hỏi lượt 1 | 43 % (25/58) | 43 % |
| Jev, câu hỏi lượt 2 | 57 % (33/58) | 60 % |
| Jev lượt 2 + luật `none` | 60 % (35/58) | 66 % |
| Jev lượt 2 + luật (a)–(d), như add-in áp hôm nay | 64 % (37/58) | 69 % |

Hai lượt Jev đó chạy với chữ cũ, khi báo cháy còn là Electrical, và trả Electrical cho mọi lớp báo cháy, nên số Jev
giảm khi nhãn đổi; luật (d) không đổi số nào trên các lượt đó. Chữ hiện tại của `questions.json`, giờ cũng là chữ
add-in gửi, chưa đo trên tên lớp thật: tên lớp là của khách và không được gửi đi. Số Jev với chữ hiện tại chỉ có trên
fixture bịa ở trên.

Luật (b) và (c) được chọn sau khi đã thấy holdout.

## Nguồn

- Việc của chủ dự án, 2026-09-26, trong một add-in AutoCAD của Paper; nguồn cụ thể ghi ở sổ nghiên cứu của kho kit.
- Demo truyền cảm hứng, Proq phân loại 26 sheet trong 2.9 s với $0.0052:
  https://x.com/hari_trinay/status/2101118529936519453
