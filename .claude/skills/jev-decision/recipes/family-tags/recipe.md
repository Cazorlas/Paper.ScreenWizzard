# family-tags — gắn danh mục thư viện, bộ môn và cờ 2D cho family .rfa

Một công thức của skill. Mỗi file .rfa của một thư viện family được hỏi ba câu: thuộc danh mục thư viện nào, bộ môn
nào dùng nó, và có phải family 2D chỉ vẽ trong view không.

## Khi nào dùng

Một thư viện hàng trăm, hàng nghìn file .rfa gộp từ nhiều nguồn: dự án cũ, nhà cung cấp, đồng nghiệp. Category Revit
của family hay sai (thiết bị, thiết bị chữa cháy, biển báo vẽ bằng Generic Models hay category khác), tên file là bản
copy (`Copy of`, `(2)`), viết tắt tiếng Việt, hay tên của family cha mà phần đó từng lồng trong. Thư viện cần lọc theo
danh mục và bộ môn để người dựng mô hình tìm được family. Host: revit.

Không có bước LLM tóm tắt trước Jev. Code đọc file ra state đã ngắn (bốn trường), nên state chính là bản tóm tắt. Cho
một LLM tóm tắt family trước thì thêm tiền, thêm chữ rời máy, và chữ gửi Jev đổi theo từng lần tóm tắt nên không đo
được.

## State gửi Jev

Một dòng chữ cho mỗi file, bốn trường theo thứ tự cố định:

`file: <tên>.rfa | category: <category Revit gốc> | types: <t1>; <t2>; … | shared parameters: <p1>; <p2>; …`

- Trường rỗng ghi `-`.
- `file` chỉ là tên file, không đường dẫn, không tên thư mục (thư mục thường mang tên dự án hay khách).
- `category` là tên tiếng Anh của category Revit, lấy theo category dựng sẵn, không theo ngôn ngữ giao diện.
- `types`: tối đa 6 tên type đầu tiên theo thứ tự trong family; `shared parameters`: tối đa 8 tên shared parameter.
  Còn nữa thì bỏ, không ghi số đếm.
- Có ký tự ngoài ASCII trong ba trường tên thì thêm `| ascii: <file> / <types> / <shared parameters>`: cùng chữ, bỏ
  dấu (`đ` thành `d`), giữ hoa thường và dấu câu. Gửi cả bản gốc lẫn bản bỏ dấu.

Không nằm trong state đã đo: đường dẫn ổ đĩa hay thư mục, tên người tạo hay người sửa, tên dự án, tên khách, giá trị parameter,
hình học, ảnh thumbnail, family lồng bên trong.

## Câu hỏi

`questions.json` là ba câu `choice`, gửi nguyên văn:

- `library_category`: 26 lựa chọn, danh mục thư viện do công thức này đặt. Mỗi lựa chọn gom một nhóm category Revit;
  mô tả kết thúc bằng `Revit: <các category>`. Một lựa chọn mỗi category Revit thì gần 60 lựa chọn, người duyệt thư
  viện không cần tách Duct Fittings với Duct Accessories. Rà 2026-10-01 sau lần đo đầu, theo thư viện family kiến
  trúc, kết cấu và cơ điện thông thường: thêm `MEP hangers and supports` (giá đỡ, ty treo; Revit không có category
  cho nó nên trước đó rơi vào Generic model); `Structural columns and framing` đổi thành `Columns and framing` để nhận
  cả cột kiến trúc (category Columns); xếp mười category Revit mới (Food Service Equipment, Medical Equipment,
  Vertical Circulation, Plumbing Equipment, Fire Protection, Audio Visual Devices, Mechanical Control Devices,
  Hardscape, Temporary Structures, Spot Elevation Symbols) vào lựa chọn có sẵn. Không bỏ, không gộp lựa chọn nào.
- `discipline`: 8 lựa chọn (Architecture, Structure, Mechanical, Plumbing, Electrical, Fire protection, General,
  Other), trùng tên với `sheet-classify`; mô tả viết lại cho family.
- `is_annotation`: 2 lựa chọn (`yes`, `no`), độc lập với `library_category` dù gần như suy được từ nó: đó là chỗ đặt
  luật nhất quán sau Jev.

Ranh giới cố ý:

- Bơm của mọi hệ là Mechanical equipment; bộ môn mới tách hệ. Van chữa cháy là Pipe fittings and accessories.
- Thiết bị chữa cháy và báo cháy làm ở category nào cũng là Fire protection devices.
- Biển báo có đèn (đèn exit) là Lighting fixtures, bộ môn Electrical. Biển không đèn là Signage: biển thoát hiểm,
  biển lối ra, biển chỉ hướng thoát hiểm và biển thiết bị chữa cháy thuộc bộ môn Fire protection; biển phòng, biển WC
  và biển chỉ đường tới phòng hay khu vực thuộc Architecture. Mô tả bộ môn viết lại 2026-10-01 sau lần đo đầu: chữ cũ
  để "wayfinding" ở Architecture mà không nói biển chỉ hướng thoát hiểm thuộc bên nào.
- Hai loại 2D tách theo category Revit: family ở Generic Annotations (vẽ theo khổ giấy, giữ cỡ in ở mọi tỷ lệ) là
  Annotation symbols, family ở Detail Items (vẽ theo cỡ thật, to nhỏ theo tỷ lệ view) là Detail items, dù tên file nói
  vật gì. Viết lại 2026-10-01 sau lần đo đầu: chữ cũ để "plan symbols" ở Annotation symbols và "2D plan or elevation
  drawings of objects" ở Detail items, hai mô tả chồng nhau.
- Cột kiến trúc và cột kết cấu cùng danh mục Columns and framing; bộ môn tách chúng. Giá đỡ và ty treo là MEP hangers
  and supports, mang bộ môn của thứ nó đỡ.
- Báo cháy là Fire protection. Luật lùi về Electrical của `layer-map` và `sheet-classify` (dự án không có bộ môn Fire
  protection) không áp cho thư viện: thư viện không thuộc một dự án.
- Quạt hút khói và quạt tăng áp là Mechanical; báo cháy là Fire protection.
- Tag, ký hiệu và chi tiết mang bộ môn của thứ nó thể hiện. General chỉ cho family mọi bộ môn dùng: khung tên, mũi
  tên bắc, đầu mặt cắt, đầu cao độ, đầu trục, multi-category tag.
- Profile không là annotation (`no`): phác thảo 2D để quét hình 3D, không vẽ trong view.

`instructions` nêu lớp lỗi (category sai, tên copy, viết tắt, tên family cha), không chép dòng khó.

## Luật trước và sau Jev

- **Trước Jev:** `rules.json` là đúng điều thư viện làm khi chỉ lọc theo category Revit: category gốc → nhãn, độ tin
  0.8. Luật khớp từ nguyên vẹn trên toàn bộ state đã bỏ dấu, viết thường; luật đầu tiên khớp thì thắng. Nhãn trường
  thành chữ, nên cụm `category <tên category>` chỉ khớp ở trường `category`.
  - Luật Tags khớp `tags types`: mọi category tag có tên kết thúc bằng Tags, nên cụm này phủ hết mà không liệt kê
    khoảng 40 category. Nó đứng đầu vì `category mechanical equipment` cũng khớp trong
    `category mechanical equipment tags`.
  - Category Revit mới (từ bản 2022–2023) có luật category như category cũ. `MEP hangers and supports` không có
    luật: Revit không có category cho nó, nên luật luôn ra danh mục của category gốc.
  - `discipline`: category một bộ môn → bộ môn đó, độ tin 0.8. Category đa bộ môn (Mechanical Equipment, Plumbing Equipment, Pipe
    Fittings, Pipe Accessories, Generic Models, Mass, Generic Annotations, Detail Items, Profiles) không có luật
    category; với chúng mới đến từ khoá bộ môn, độ tin 0.7, Fire protection xếp trước để bơm chữa cháy không rơi vào
    hệ khác. Không đủ manh mối thì `Other` độ tin 0.
  - `is_annotation`: `yes` cho category tag, Title Blocks, Generic Annotations, các đầu ký hiệu, View Titles, Detail
    Items; mặc định `no` độ tin 0.8.
  - Luật không đọc tên file để đổi `library_category`: đó là việc Jev được đo.
- **Sau Jev:**
  - (a) `library_category` thuộc Tags, Annotation symbols, Title blocks, Detail items mà `is_annotation` = `no`, hay
    một lựa chọn 3D mà `yes`: cả hai vào "cần xem" với độ tin 0.30.
  - (b) Jev đổi `library_category` khỏi nhóm của category Revit gốc (luật trả 0.8): giữ nhãn Jev nhưng đánh dấu
    "category gốc sai?" để người duyệt thư viện sửa family; không tự sửa file.
  - (c) Tên file bắt đầu `Copy of` hay kết thúc `(n)`: code tìm file cùng tên gốc trong thư viện để gộp trùng. Việc
    của code, không hỏi Jev.
  - (d) Độ tin dưới 0.70 thì đưa vào "cần xem".
  - Chỉ đụng nhãn nguồn `jev`.

Công thức chỉ gắn nhãn thư viện; không để Jev đổi category trong file .rfa.

## Port

`IFamilyTagJudge.JudgeAsync(FamilyFacts) → FamilyTagJudgement`. `FamilyFacts(FileName, Category, TypeNames,
SharedParameterNames)` chỉ chứa bốn trường của mục State; `FamilyStatePolicy` dựng chuỗi state (cắt 6 type, 8 tham
số, bỏ đường dẫn, thêm `ascii`). `JevSwitchPolicy.Decide(switch, typeSafeKey, openRouterKey)` chọn đường gọi như
`layer-map`. Công tắc tắt là `PAPER_FAMILYTAGS_JEV=0` (Jev bật mặc định). `FamilyTagConsistencyPolicy` áp (a)–(d) sau khi đã gộp nhãn.

Đọc type và shared parameter trong cùng lần mở file mà thư viện đã làm để lấy category. Kết quả cache theo tên file,
kích thước và ngày sửa; không hỏi lại file không đổi.

## Đo

46 dòng bịa: 30 dòng thường, 16 dòng khó (bản `Copy of` tên vô nghĩa hay đã sửa thành vật khác, family lồng nhau mang
tên family cha, viết tắt tiếng Việt QHK, BCC, BCN, Generic Models dùng làm biển báo, category gốc sai cho dàn nóng, tủ
điện, tủ chữa cháy và sprinkler, một đèn exit có đèn, cùng một ty treo ống gió vẽ bằng Generic Models). FT45 (cột
kiến trúc, thường) và FT46 (ty treo, khó) thêm 2026-10-01 sau lần đo đầu, cùng hai lựa chọn mới; nhãn FT20 chỉ đổi
tên lựa chọn.

`rules: 34/46 rows, hard 4/16`
`jev (typesafe/jev-1.13, 2026-10-01): 43/46 rows, hard 13/16`

Một lần chạy qua OpenRouter trên fixture bịa, sau khi đổi ranh giới và danh mục; câu Jev không trả lời thì lấy của
luật; chưa áp luật sau Jev (a)–(d). Dòng khó Jev vẫn sai: FT32, FT33, FT39 (theo từng câu: `library_category` 43/46,
`discipline` 45/46, `is_annotation` 46/46). 46 lời gọi, 0 hỏng, 140046 token đầu vào, 26.6 s. Cả ba dòng sai đều là
dòng khó và đều sai `library_category`; một trong ba sai cả `discipline`, không rõ dòng nào. FT32 và FT33 vẫn sai
`library_category` dù ranh giới Annotation symbols với Detail items đã viết lại. FT38 (biển chỉ dẫn thoát hiểm) nay
đúng. FT46 (ty treo ống gió, MEP hangers and supports / Mechanical) đúng. FT39 (biển phòng, biển WC vẽ bằng Generic
Models), đúng ở lần đầu, nay sai `library_category`.

Lần đo đầu (2026-10-01, 25 lựa chọn, 44 dòng): jev 41/44 rows, hard 12/15; dòng khó còn sai FT32 (ký hiệu 2D bồn cầu
mang tên family cha), FT33 (chi tiết 2D cánh cửa ở Detail Items) và FT38 (biển chỉ dẫn thoát hiểm vẽ bằng Generic
Models). Mô tả lựa chọn, nhãn FT20 và hai dòng FT45, FT46 đổi 2026-10-01 sau lần đo đầu; số đo lại không sạch như số
đầu, nhất là ở FT32, FT33, FT38: ranh giới mới được viết khi đã biết Jev sai ba dòng đó.

Luật và fixture cùng một người viết: đây là số dev. Số trên thư viện thật: chưa có.

## Nguồn

- Gắn nhãn 1,018 bài báo thành 24 chủ đề bằng Jev: https://x.com/nutlope/status/2100426999546184123
- Việc của chủ dự án 2026-10-01; nguồn cụ thể ở sổ nghiên cứu của kho kit.
