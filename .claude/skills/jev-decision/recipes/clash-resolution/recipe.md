# clash-resolution — chọn cách gỡ một va chạm MEP

Với một va chạm MEP mà code đã tính hình học thành chữ, công thức này chấm hai nhãn: `which_moves` (phần tử nào dời)
và `action` (nâng, hạ, đổi cỡ, đi vòng, hay đưa người xử lý). Code tính hình học và kiểm move; Jev chỉ chọn ý định.
Một sàn cứng cho ống tự chảy chạy trước cả luật lẫn Jev, và Jev không hạ được sàn đó.

## Khi nào dùng

Sau khi dò va chạm MEP trên model Revit, mỗi va chạm là một cặp phần tử A, B (thứ tự do bộ dò báo ra, tuỳ ý). Code
tính hình học thành chữ: loại, hệ thống, cỡ, kiểu dòng chảy, cao độ đáy, khoảng hở trên và dưới, và những move vừa
kèm độ dời. Jev chọn ý định trước (cái nào dời, dời kiểu gì), như cánh tay robot chọn ý định rồi mới chuyển động; code
kiểm move đó có trong danh sách vừa rồi đề xuất.

Kết quả là một đề xuất ("dời A: hạ 160 mm" hay "người xử lý: <lý do>"), không tự sửa model. Áp vào model đi qua
luồng sửa model có dry run của host revit (skill revit-model-task). Hình học tính bằng code (skill cad-geometry hay
code Revit), không bao giờ bằng Jev.

## State gửi Jev

Sản phẩm và fixture dùng cùng một dạng, năm dòng, mọi giá trị do code tính:

```text
A: <kind> | <system> | <size> | <flow> | bottom <n> mm above floor
B: <kind> | <system> | <size> | <flow> | bottom <n> mm above floor
slope required: <A | B | A and B | none>
free space: A <n> mm above, <n> mm below; B <n> mm above, <n> mm below
moves that fit: A <moves | none>; B <moves | none>
```

- `kind`: `pipe`, `duct`, `cable tray`, `conduit` (từ category).
- `system`: nhãn tiếng Anh cố định code ánh xạ từ phân loại hệ thống — `sanitary drain`, `storm drain`,
  `condensate drain`, `domestic cold water`, `domestic hot water`, `chilled water supply`, `chilled water return`,
  `heating water`, `sprinkler main`, `sprinkler branch`, `supply air`, `supply air main`, `return air`, `exhaust air`,
  `fresh air`, `toilet exhaust`, `kitchen exhaust`, `power`, `data`, `lighting`, `fire alarm`; thêm `, pumped` khi ống
  thoát nước là ống có áp.
- `size`: `DN<n>` (ống), `<W>x<H> mm` (ống gió chữ nhật, khay cáp), `<d> mm` (conduit).
- `flow`: `gravity, slope <x>% required` hay `pressure` (ống), `air` (ống gió), `cables` (khay, conduit).
- `bottom`: cao độ đáy tính từ mặt sàn tầng dưới.
- `slope required`: phần tử nào là ống tự chảy cần giữ độ dốc.
- `free space`: khoảng hở trên tới sàn tầng trên (trừ khe 25 mm, nhỏ hơn khi dầm hay ống khác chặn) và dưới tới trần
  (trừ khe 25 mm).
- `moves that fit`: theo thứ tự cố định `raise <n> mm`, `lower <n> mm`, `resize to <W>x<H> mm`,
  `route around (+<L> m, <k> bends)`; không move nào vừa thì `none`.

Code tính "moves that fit" như sau. Khe hở 25 mm. Chiều cao của ống là đường kính ngoài theo DN, của conduit là đường
kính, của ống gió và khay là cạnh H; đỉnh bằng đáy cộng chiều cao. Để A nâng qua B, A phải lên tới đỉnh B cộng khe;
để A hạ xuống dưới B, đỉnh A phải xuống tới đáy B trừ khe; độ dời của B là hai số đó đổi chỗ. Nâng vừa khi độ dời nâng
không quá khoảng hở trên; hạ vừa khi độ dời hạ không quá khoảng hở dưới. Đổi cỡ chỉ cho ống gió: cỡ mới cùng diện tích
(±3 %), tỉ lệ cạnh không quá 4, vừa khi cạnh H giảm ít nhất bằng độ dời nâng hay hạ. Đi vòng là dữ kiện code đưa (có chỗ
bên cạnh), chỉ ghi khi vừa. Ống tự chảy vẫn được liệt kê move vừa về hình học: hình học là việc của code, luật dốc là
chính sách, và fixture nhờ vậy đo được Jev có giữ luật dốc không.

Không nằm trong state đã đo: toạ độ, id element, tên tầng, tên system type hay family của model, tên file, tên dự án, tên
khách, tên người dùng. Toàn bộ state là chữ tiếng Anh do code sinh, nên luật "gửi cả bản bỏ dấu" của `SKILL.md` không
áp dụng.

## Câu hỏi

`questions.json` là hai câu `choice`, chữ đã đo là chữ gửi đi:

- `which_moves`: `element A`, `element B`, `neither`. Thứ tự quyết định:
  1. Ống có độ dốc bắt buộc (ống thoát tự chảy) không dời — không nâng, không hạ, không đi vòng — kể cả khi nó nhỏ hơn
     và có move vừa. Xét theo `flow` và dòng `slope required`, không theo tên hệ thống: ống thoát `pumped` là ống có áp
     và được dời.
  2. Trong các phần tử được dời, cái nhỏ hơn dời, đo bằng cỡ ngang: DN của ống, đường kính conduit, cạnh lớn của ống
     gió hay khay. Chỉ khi cỡ nhỏ lớn hơn 75 % cỡ lớn (chênh dưới 25 % của cỡ lớn) thì ống, khay hay conduit dời, ống
     gió đứng yên; đúng 75 % hay ít hơn thì cái nhỏ dời. Biên này chốt 2026-10-01 sau lần đo đầu: chữ cũ "within 25%"
     không nói có tính biên không, cũng không nói tính trên cỡ nào, và CL30 (150 và DN200, đúng 75 %) nằm trên biên.
     Nhãn CL30 giữ nguyên (`element B` / `raise`).
  3. Cái đó không có move nào vừa thì cái kia dời, nếu được dời và có move vừa.
  4. Không thì `neither`.
- `action`: `raise`, `lower`, `resize`, `route around`, `send to a person`. Với phần tử dời: nâng hay hạ, cái nào độ
  dời nhỏ hơn (bằng nhau thì nâng, giữ khoảng trần); rồi đổi cỡ; rồi đi vòng. Đổi cỡ đứng trước đi vòng vì làm dẹt ống
  gió là việc cục bộ có hai côn, còn đi vòng thêm chiều dài, bốn co và cần chỗ bên cạnh. Move liệt kê cho phần tử đứng
  yên không tính. Không phần tử nào được dời có move vừa thì `send to a person`.
- Hai câu chứ không phải sáu lựa chọn trong một: lựa chọn "move the other element" của brief bị bỏ vì trùng việc của
  `which_moves`, và A/B là thứ tự bộ dò báo ra nên "cái kia" không có nghĩa cố định. `neither` có vì khi đưa người xử
  lý thì không phần tử nào dời.
- Thứ bậc cố định theo loại (ống gió > khay > ống có áp > conduit) bị bỏ: nó sai khi ống chính chữa cháy hay ống nước
  lạnh to hơn ống gió nhỏ. Chọn move rẻ nhất của cả hai phần tử cũng bị bỏ: nó bắt một ống gió 1400 mm né một khay cáp
  chỉ vì ống gió cần dời ít hơn, ngược thói quen phối hợp giữ yên phần tử to.

## Luật trước và sau Jev

- **Sàn cứng cho ống tự chảy (Jev không hạ được):** ống có `slope required` không bao giờ dời — không nâng, không hạ
  cục bộ (tạo võng đọng cặn), không đi vòng (đoạn dài thêm ăn mất cao độ dốc), không đổi cỡ. Cao độ hạ lưu là việc của
  người thiết kế.
  - (0) Trước Jev: `slope required: A and B`, hoặc `moves that fit: A none; B none` → `neither` / `send to a person`,
    nguồn `rules`, **không gọi Jev**.
  - (a) Sau Jev: phần tử Jev chọn có `slope required` → `send to a person`, nguồn `rules`.
- **Luật từ khoá (`rules.json`):** khớp cả từ trên chữ thường đã bỏ dấu, luật đầu tiên khớp thì thắng. Mỗi câu mở đầu
  bằng sàn (0) (`slope required a and b`, `a none b none`); `which_moves` tiếp theo là sàn (a) (`slope required a` →
  `element B`, `slope required b` → `element A`), bước (3) (`b none` → `element A`, `a none` → `element B`), rồi câu
  "ống, khay, conduit dời, ống gió đứng yên" của bước (2) theo loại của A rồi của B. Luật `action` theo thứ tự move cố
  định: `raise`, `lower`, `resize`, `route around`. Luật không so được số (cỡ, độ dời); đó là chỗ của Jev.
  Không luật nào khớp: `which_moves` = `neither`, `action` = `send to a person` (0.30) — không biết thì đưa người.
- **Sau Jev (luật nhất quán, sản phẩm làm theo):**
  - (a) như trên.
  - (b) `action` Jev chọn không có trong `moves that fit` của phần tử Jev chọn → `send to a person`, nguồn `rules`:
    code kiểm hình học, Jev chỉ chọn ý định.
  - (c) `which_moves` = `neither` mà `action` khác `send to a person`, hay ngược lại → `send to a person`.
  - (d) Độ tin của một trong hai câu dưới 0.70 → `send to a person` ("cần xem").

## Port

`IClashResolutionJudge.JudgeAsync(ClashFacts) → ClashJudgement`. `ClashFacts(A, B, SlopeRequired)`, mỗi phần tử
`ClashElementFacts(Kind, System, Size, Flow, BottomMm, FreeAboveMm, FreeBelowMm, Moves)`, mỗi move
`ClashMove(Kind, ShiftMm, NewSize, ExtraLengthM, Bends)`. Công tắc tắt `PAPER_CLASH_JEV=0`, riêng cho tính năng này (Jev bật mặc định);
đường gọi chọn qua `JevSwitchPolicy` chung. Interactor: hình học (code) → facts → sàn (0) → luật → judge (nếu công tắc
bật) → `ClashResolutionPolicy` (a)–(d) → plan đề xuất. Code sản phẩm nằm ở kho add-in Revit, không ở kit.

## Đo

Fixture có 40 dòng bịa: 24 dòng thường (phần tử dời nhỏ hơn rõ hay phần tử kia là ống dốc, phần tử đứng yên không có
move, move đầu tiên theo thứ tự là đáp án) và 16 dòng khó theo mẫu lỗi thật — ống thoát tự chảy có move nâng hay hạ vừa
mà không được dời, hai ống cùng dốc, ống thoát `pumped`, ống gió lớn và khay cáp nhỏ, sprinkler main to hơn ống gió,
khoảng hở trần trông rộng mà thiếu, ống quen "nhường ống gió" nhưng không có move, nâng liệt kê trước mà hạ ngắn hơn,
hai cỡ chênh trong 25 %, phần tử to có move ngắn hơn phần tử nhỏ, hai ống gió. Mọi số tính bằng công thức hình học ở
trên.

`rules: 29/40 rows, hard 5/16`
`jev (typesafe/jev-1.13, 2026-10-01): 36/40 rows, hard 13/16`

Một lần chạy qua OpenRouter trên fixture bịa, sau khi đổi biên 25 %; câu Jev không trả lời thì lấy của luật; chưa áp
luật sau Jev (a)–(d). Dòng khó Jev vẫn sai: CL34, CL35, CL39 (theo từng câu: `which_moves` 39/40, `action` 36/40).
40 lời gọi, 0 hỏng, 45101 token đầu vào, 28.5 s. Bốn dòng sai (ba dòng khó và một dòng thường) đều sai `action`, một
trong số đó sai cả `which_moves`, không rõ dòng nào; số giống lần đầu. CL30 (đúng biên 75 %) đúng cả hai câu.

Lần đo đầu (2026-10-01, chữ cũ "within 25%"): jev 36/40 rows, hard 13/16; dòng khó còn sai CL34, CL35, CL39, cả ba
cùng mẫu (phần tử dời có cả nâng lẫn hạ, hạ ngắn hơn, Jev chọn nâng); CL30 đúng. Chữ biên 25 % của hai câu đổi
2026-10-01 sau lần đo đầu; số đo lại không sạch như số đầu, và biên được chốt theo hướng giữ nhãn CL30 mà Jev đã
đúng ở lần đầu (tính biên thì CL30 thành `element A` / `lower`).

Luật, câu hỏi và fixture cùng một người viết: số dev. Dòng khó được viết trước luật và luật không chỉnh theo chúng.

## Nguồn

- MuJoCo robot arm: Jev nhận hình học đơn giản hoá bằng chữ, chọn ý định trước rồi mới chuyển động:
  https://x.com/dimentary/status/2101018760371171420
