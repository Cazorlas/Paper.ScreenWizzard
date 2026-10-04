# classification-code — gán mã Uniclass 2015 cho family type Revit bằng cách đi cây phân loại

Công thức thứ hai của skill, chép hình của `layer-map`. Chưa chạy trong sản phẩm nào: mục Port mô tả việc cần làm ở
phía Revit.

## Khi nào dùng

Model cần mã Uniclass 2015 Products (Pr) trên từng family type để bàn giao (COBie, bảng khối lượng theo mã), nhưng
family đến từ nhiều nguồn: tên tiếng Việt, Generic Model dùng làm thiết bị, type chỉ có mã nhà sản xuất, family làm
từ khuôn sai category. Bảng tra theo category hỏng ngay ở những family đó.

Cây Pr bản v1.43 (July 2026) có 17 nhóm tầng 1 và hàng nghìn mã ở các tầng dưới, còn một câu `choice` có tối đa 255
lựa chọn. Vì vậy mỗi tầng là một câu riêng: chọn nhóm, rồi chọn con của nhóm vừa chọn, rồi con của con, cho tới khi
độ tin xuống dưới ngưỡng hoặc tới lá.

Phạm vi: một family type là một sản phẩm, nên chỉ đi cây Pr. Type nhiều lớp (Walls, Floors, Roofs, Ceilings) mang mã
Ss (systems); đó là một lần đi cây khác cùng hình, ngoài công thức này.

## State gửi Jev

Một dòng cho mỗi family type:
`category: <category> | family: <family> | type: <type> | params: <tên>=<giá trị>; …`

- `params`: 5 parameter type đầu tiên **có giá trị**, theo thứ tự của hộp Type Properties. Bỏ cả nhóm Identity Data
  (Mark, Type Mark, Comments, Type Comments, Description, Manufacturer, Model, URL, Keynote, Assembly Code, OmniClass
  và mọi parameter phân loại): bỏ theo nhóm dễ kiểm hơn một danh sách trường.
- Khi family hay type có chữ Việt có dấu, thêm `| ascii: <family> / <type>` đã bỏ dấu (gửi cả bản gốc lẫn bản bỏ
  dấu, như SKILL.md nói).
- Gửi **cùng một state** cho mọi tầng, không thêm câu trả lời của tầng trên, để số đo của câu tầng dưới trong eval là
  số của lời gọi tầng dưới thật.

Không nằm trong state đã đo: tên dự án, đường dẫn file, Mark, Comments, tên người dùng, hình học, vị trí, level.

## Câu hỏi

`questions.json` có ba câu `choice`. Tên mỗi lựa chọn là `<mã> <tên chính thức>`, nguyên văn bảng v1.43 (ví dụ
`Pr_65_65 Ductwork products`); sản phẩm lấy mã là chữ đầu tiên. Chỉ mã thì Jev không đọc được nghĩa; chỉ tên thì mất
mã, và tên trùng được giữa các nhánh ("Deaerators" có ở cả Pr_65_52 và Pr_65_67).

- `group` (tầng 1): 17 lựa chọn, mọi nhóm Pr. Câu duy nhất viết tay: mỗi nhóm một câu nói nhóm chứa gì (lấy từ nhóm
  con của bảng) và category Revit hay rơi vào đó.
- `subgroup_pr_65` (tầng 2 của một nhóm mẫu, Pr_65 Services and process distribution products): 12 lựa chọn.
- `section_pr_65_65` (tầng 3 của một nút mẫu, Pr_65_65 Ductwork products): 3 lựa chọn. Tầng này có vì mẫu lỗi
  "family Duct Fitting nhưng là damper" chỉ tách ở đây: damper là Pr_65_65_24, phụ kiện ống gió là Pr_65_65_25.

Câu tầng 2 trở xuống **sinh máy** từ bảng tải về của NBS theo một khuôn cố định, nên chữ đã đo cho Pr_65 và Pr_65_65
là đúng chữ khuôn sinh ra; không thêm ghi chú tay cho nhánh mẫu, nếu không nhánh mẫu sẽ tốt hơn mọi nhánh khác. Khuôn:

- `instructions`: `Which part of Uniclass 2015 {mã} {tên} is the Revit family type described in the state? Decide by what the product is, read from the family name, the type name and the parameters; the Revit category is only a hint, and names may be Vietnamese or only a manufacturer's code.`
- một lựa chọn cho mỗi con của nút, theo thứ tự mã: khoá `{mã con} {tên con}`, criteria
  `{tên con}. Includes: {tên các cháu, nối bằng "; "}`, hay chỉ `{tên con}` khi nút con không có con.

Câu sai hay mã ngoài bảng không thể xuất hiện ở tầng dưới, vì lựa chọn chính là con của nút vừa chọn. Không có lựa
chọn thoát ("không thuộc nhóm này"): không dòng nào của fixture mong đợi nó, nên nó sẽ là chữ gửi đi mà chưa đo; tầng
trên sai thì ngưỡng ở tầng dưới đưa vào "cần xem".

Vì sao không một câu 255 lựa chọn: cây Pr có hàng nghìn mã, cắt còn 255 là bỏ phần lớn cây; và một danh sách dài như
vậy làm mỗi lời gọi nặng hơn nhiều lần mà không cho Jev biết nhánh nào đáng xét. Vì sao chỉ Pr: một câu tầng 1 trộn
nhóm của Pr, Ss và EF có hai đáp án đúng theo thiết kế (một ống nước vừa là Pr_65_52 vừa thuộc một hệ Ss_55).

**Cách eval chấm cây bằng câu độc lập.** Runner gửi cả ba câu trong mỗi lời gọi nhưng chỉ chấm câu mà dòng có trong
`expect`: dòng thuộc Pr_65 mong đợi `group` + `subgroup_pr_65`, dòng thuộc Pr_65_65 mong đợi thêm `section_pr_65_65`,
dòng khác chỉ mong đợi `group`. Với các dòng trên nhánh mẫu, "dòng đúng" (mọi câu mong đợi đều đúng) bằng đúng kết quả
của lần đi cây: tầng 1 sai thì cả hai cách đều sai; tầng 1 đúng thì lần đi cây hỏi đúng câu tầng 2 đã chấm. Hai chỗ
hở biết trước:

- Dòng ngoài Pr_65 chỉ đo tầng 1.
- Sản phẩm hỏi mỗi tầng một lời gọi riêng, còn eval hỏi ba câu trong một lời gọi. State giống nhau, và luật 5 của
  skill coi các câu trong một lời gọi là độc lập.

## Luật trước và sau Jev

- **Trước Jev:**
  - (b) Type đã có mã Pr hợp lệ (có trong bảng) ở parameter phân loại thì giữ: nguồn `rules`, độ tin 1.0, không gọi
    Jev.
  - (c) Category không phải sản phẩm (annotation, Rooms, Grids, Levels, và Walls/Floors/Roofs/Ceilings) không được
    gửi.
  - Rồi `rules.json`: từ khoá chỉ tiếng Anh, khớp trên chữ thường đã bỏ dấu, luật đầu tiên khớp thì thắng. Luật theo
    tên vật (pump, fan, damper, drain, switch…) đứng trước luật theo category. Từ khoá chỉ lấy từ tên category Revit
    và chữ của dòng thường: chữ Việt duy nhất của fixture nằm ở dòng khó, nên một từ khoá tiếng Việt là từ chọn khi
    đã thấy dòng khó. Mặc định của mỗi câu là lựa chọn đầu tiên theo thứ tự bảng, độ tin 0.3: trung lập theo cách
    dựng và luôn dưới ngưỡng.
- **Sau Jev:**
  - (a) Chỉ đi xuống tầng dưới khi tầng này có độ tin ≥ 0.70. Mã ghi lại là tầng sâu nhất đạt ngưỡng; phần còn lại
    vào "cần xem".
- Độ tin dưới 0.70 thì đưa vào "cần xem".
- Eval không áp (a), (b), (c): nó chấm luật từ khoá và câu trả lời của Jev như chúng là.

## Port

`IClassificationJudge.JudgeAsync(ClassificationFacts, ClassificationQuestion) → ClassificationAnswer`: một lời gọi
cho một tầng. ClassificationFacts chỉ chứa các trường ở mục State. Interactor đi cây: luật → judge nếu công tắc bật →
ngưỡng → con của nút → … Bảng Uniclass đọc từ bản tải về của NBS, nằm trong sản phẩm. Một factory sinh câu tầng dưới
theo khuôn ở mục Câu hỏi, và có test so câu nó sinh cho Pr_65 và Pr_65_65 với `questions.json` (luật 4: câu gửi đi là
câu đã đo). Công tắc tắt là `PAPER_CLASSCODE_JEV=0` (Jev bật mặc định); JevSwitchPolicy chọn đường gọi như ở `layer-map`.

## Đo

Fixture có 45 dòng bịa: 29 dòng thường (một family type tiêu biểu cho mỗi nhóm hay gặp, tên tiếng Anh) và 16 dòng khó
viết trước luật theo năm mẫu lỗi: tên tiếng Việt; Generic Model dùng làm thiết bị; type chỉ có mã nhà sản xuất;
family "Duct Fitting" nhưng là damper; ống gió tròn và ống nước cùng chữ "pipe" hay "ống". 26 dòng đo tầng 2, 9 dòng
đo tầng 3. Hộp VAV không có trong fixture: v1.43 đặt nó ở hai chỗ (Pr_65_65_24_93 và Pr_70_65_04_94).

`rules: 35/45 rows, hard 6/16`
`jev (typesafe/jev-1.13, 2026-10-01): 44/45 rows, hard 15/16`

Một lần chạy qua OpenRouter trên fixture bịa; câu Jev không trả lời thì lấy của luật; chưa áp luật sau Jev. Dòng khó
Jev vẫn sai: CC44. Theo từng câu Jev đúng `group` 45/45, `subgroup_pr_65` 25/26, `section_pr_65_65` 9/9, nên CC44 sai
ở tầng 2: van xả khí tự động trên ống nước (Pr_65_54_93_05, một phụ kiện van) không được xếp vào Pr_65_54 Valve
products (giả thuyết, chưa đo: chữ "Air Vent" và category Pipe Accessories kéo về phía phân phối khí hay phụ kiện
ống). Runner không in lựa
chọn của từng dòng, nên lựa chọn Jev đã chọn cho CC44 chưa được ghi. Lần chạy: 45 lời gọi, 131,823 token đầu vào,
22.5 s.

## Nguồn

- Demo IAB category tree walk: Jev đi từng tầng của cây phân loại, mỗi tầng một câu choice.
  https://x.com/pilcloud/status/2102045675496366469
- Bảng Uniclass 2015 Products v1.43, July 2026, NBS: https://uniclass.thenbs.com/taxon/pr và các trang con của Pr_65,
  tra ngày 2026-10-01; mã và tên trong `questions.json` chép nguyên văn từ đó.
- Năm mẫu lỗi của dòng khó do chủ dự án nêu ngày 2026-10-01.
