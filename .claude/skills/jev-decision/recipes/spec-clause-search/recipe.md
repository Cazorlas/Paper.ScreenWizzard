# spec-clause-search — chấm từng đoạn spec/tiêu chuẩn theo câu hỏi

Công thức cho việc tìm theo câu hỏi trong một bộ spec hay tiêu chuẩn. Chưa chạy trong sản phẩm nào: mục Port mô tả việc
cần làm ở phía sản phẩm.

## Khi nào dùng

Kỹ sư hỏi một câu về một bộ spec hay tiêu chuẩn PDF ("gió tươi tối thiểu cho văn phòng là bao nhiêu?"). Tìm theo từ
khoá hỏng theo hai hướng: bỏ sót đoạn trả lời bằng chữ khác ("phòng làm việc", "không khí ngoài trời") hay bằng ngôn
ngữ khác (câu hỏi Việt, tiêu chuẩn Anh), và đẩy lên đoạn trùng gần hết chữ mà không trả lời (cùng đại lượng nhưng cho
gara, định nghĩa lặp đúng thuật ngữ, giới hạn ngược).

Công thức chấm **từng cặp** (câu hỏi, một đoạn): đoạn này có trả lời câu hỏi không. Sản phẩm xếp các đoạn theo câu
trả lời đó; việc xếp không nằm trong số đo của công thức (xem "Xếp hạng" ở mục Đo).

## State gửi Jev

Một dòng cho mỗi cặp câu hỏi–đoạn:
`question: <câu hỏi như người dùng gõ> | question plain: <câu hỏi đã bỏ dấu> | passage: <đoạn>`

- `question plain` chỉ có khi câu hỏi có chữ Việt có dấu (gửi cả bản gốc lẫn bản bỏ dấu, như SKILL.md nói). Đoạn
  không có bản bỏ dấu: đoạn lấy từ tài liệu in, luôn đủ dấu; bản bỏ dấu sẽ gấp đôi token mỗi lời gọi.
- Đoạn ≤ 800 ký tự, cắt theo ranh giới điều khoản, giữ số điều và tiêu đề ở đầu đoạn như trong tài liệu. Ký tự `|`
  trong đoạn đổi thành `/` trước khi gửi, để tách trường không vỡ.
- Mọi tài liệu được gửi khi công tắc bật, kể cả spec riêng của khách: chủ dự án đồng ý gửi dữ liệu thật ngày
  2026-10-04.

Không nằm trong state đã đo: tên file, đường dẫn, tên tài liệu, tên dự án, tên khách, tên người dùng, đoạn kề bên, ảnh trang, số
trang. Tên tiêu chuẩn và số điều riêng cũng không gửi: chúng không giúp xét nghĩa và có thể làm Jev đoán theo tên tài
liệu.

## Câu hỏi

`questions.json` có một câu `answers_question`, ba lựa chọn:

- `yes`: đoạn tự nó cho câu trả lời của đúng trường hợp được hỏi — giá trị, giới hạn, cách làm, luật, hay rằng việc đó
  không bắt buộc / không được phép; bằng tên trường hợp đó hay bằng một luật chung bao trùm nó; chữ, đơn vị, ngôn ngữ
  có thể khác câu hỏi. Định nghĩa chỉ là câu trả lời khi câu hỏi hỏi nghĩa của thuật ngữ.
- `partly`: chỉ một phần — một số trường hợp hay đại lượng được hỏi, hay chỉ dưới một điều kiện câu hỏi không nêu; một
  yêu cầu, điều kiện hay ngoại lệ liên quan mà không có câu trả lời; hay chỉ tới bảng, điều, phụ lục chứa câu trả lời.
- `no`: không giúp trả lời — chỗ, phần tử, thiết bị, loại nhà hay đại lượng khác dù trùng gần hết chữ, hay giới hạn
  ngược (tối đa khi hỏi tối thiểu); chỉ định nghĩa hay nói cách đo một thuật ngữ câu hỏi dùng; chỉ nêu phạm vi hay mục
  đích.

`instructions` nói: xét theo nghĩa, không theo chữ trùng; đoạn trả lời "không" (not required, need not, shall not) vẫn
là trả lời. Chữ câu hỏi nêu **lớp** lỗi (chữ khác, ngôn ngữ khác, trùng chữ mà không trả lời, giới hạn ngược, định
nghĩa, chỉ tới bảng, trả lời "không"), không chép dòng khó. Cùng một người viết câu hỏi và dòng khó, nên số Jev là số
dev.

Không có câu thứ hai về loại đoạn (yêu cầu, định nghĩa, ghi chú, phạm vi): sản phẩm không cần nhãn đó để xếp, và lỗi
"đoạn định nghĩa" đã được đo bằng chính `answers_question`. Không chỉ hai lựa chọn: mất "chỉ tới Bảng 4", thứ người đọc
spec cần thấy.

## Luật trước và sau Jev

- **Trước Jev:**
  - (0) Tài liệu không được đánh dấu công khai thì không gửi; chỉ luật.
  - (1) `rules.json`: luật **overlap** — tỉ lệ từ của câu hỏi (trường `question`) có trong đoạn (trường `passage`),
    trên chữ thường đã bỏ dấu, mỗi từ một lần, sau khi bỏ các từ khung câu hỏi của `overlap_ignore` (từ hỏi, giới từ,
    trợ từ, động từ khuyết thiếu, required/need; không phải từ chủ đề). Độ trùng ≥ 0.6 → `yes` độ tin 0.6; ≥ 0.3 →
    `partly` độ tin 0.5; còn lại `no` độ tin 0.3. Độ trùng từ không bao giờ tự nhận "chắc" (dưới 0.70). Ngưỡng 0.6 và
    0.3 chốt cùng lúc với dòng khó, không chọn sau khi đo.
  - Điểm yếu đã biết, không vá: bỏ dấu làm vài từ Việt trùng nhau, nên `của` và `cửa` cùng thành `cua` và cùng bị bỏ;
    `cần` và `can` cũng vậy; `không` bị bỏ nên `không khí` mất một chữ.
- **Sau Jev**, chỉ đụng câu trả lời có nguồn `jev`:
  - (a) `yes` hay `partly` với độ tin < 0.70 → danh sách "có thể liên quan", không vào "trả lời" (luật 6).
  - (b) Jev `no` với độ tin < 0.70 mà luật nói `yes` → "có thể liên quan": không giấu một đoạn trùng nhiều chữ chỉ vì
    Jev không chắc.
  - (c) Xếp: `yes` rồi `partly`, độ tin giảm dần, bằng nhau thì theo thứ tự trong tài liệu; `no` ẩn sau một nút.
- Eval không áp (0), (a), (b), (c): nó chấm luật overlap và câu trả lời của Jev như chúng là.

## Port

IClauseSearchJudge.JudgeAsync(ClauseFacts) → ClauseJudgement. ClauseFacts(Question, QuestionPlain, Passage) chỉ chứa
các trường ở mục State. Interactor: cắt tài liệu thành đoạn (≤ 800 ký tự, theo điều khoản) → luật overlap cho mọi đoạn
→ nếu công tắc bật, Jev cho **mọi** đoạn, gọi song song → (a), (b), (c). Không lọc trước bằng độ
trùng: lọc thì mất đúng những đoạn trả lời mà không trùng chữ. Tối đa 300 đoạn mỗi câu hỏi; quá 300 thì bảo người dùng
thu hẹp tài liệu, không cắt im lặng. Công tắc tắt là `PAPER_SPECSEARCH_JEV=0` (Jev bật mặc định); JevSwitchPolicy chọn đường gọi như ở
`layer-map`. Chi phí: 300 đoạn × ~650 token ≈ 195k token ≈ $0.008 mỗi câu hỏi.

## Đo

Fixture có 43 dòng bịa, mỗi dòng một cặp câu hỏi–đoạn; đoạn viết theo giọng tiêu chuẩn công khai, số điều và giá trị
đều bịa. 26 dòng thường: bảy câu hỏi (bốn Việt, ba Anh), đoạn cùng ngôn ngữ với câu hỏi, đúng theo nghĩa và đúng theo
độ trùng (`yes` ≥ 0.6, `partly` trong [0.3, 0.6), `no` < 0.3). 17 dòng khó trên sáu câu hỏi khác, viết trước luật theo
các mẫu lỗi: đoạn trả lời bằng chữ khác; đoạn trùng gần hết chữ mà nói về chỗ hay thiết bị khác; định nghĩa lặp đúng
thuật ngữ; câu hỏi và đoạn khác ngôn ngữ (cả chiều Việt–Anh lẫn Anh–Việt, và một đoạn khác ngôn ngữ mà không liên
quan); giới hạn ngược; chỉ tới bảng; chỉ một trường hợp; trả lời "không bắt buộc" hay "shall not"; câu hỏi gõ không
dấu. Nhãn của dòng khó: `yes` 7, `partly` 3, `no` 7.

`rules: 28/43 rows, hard 2/17`
`jev (typesafe/jev-1.13, 2026-10-01): 42/43 rows, hard 16/17`

Một lần chạy qua OpenRouter trên fixture bịa; câu Jev không trả lời thì lấy của luật; chưa áp luật (a)–(c). Dòng khó
Jev vẫn sai: SC42 — câu hỏi Anh về van chặn lửa cho ống hút bếp qua tường ngăn cháy, đoạn Việt nêu một yêu cầu liên
quan (bọc ống) mà không có câu trả lời, mong đợi `partly`. Runner không in lựa chọn của từng dòng, nên lựa chọn Jev đã
chọn cho SC42 chưa được ghi. Lần chạy: 43 lời gọi, 0 hỏng, 31,145 token đầu vào, 21.4 s.

Số `hard` của luật thấp **theo cách dựng**: các mẫu lỗi của dòng khó chính là chỗ độ trùng từ hỏng, nên luật chỉ đúng
hai dòng khó (một đoạn "shall not" trùng nhiều chữ, một đoạn khác ngôn ngữ không liên quan).

**Xếp hạng.** Chấm từng cặp `yes` / `partly` / `no` là cách đo tối thiểu: nó cho biết Jev tách được đoạn đúng hay
không, nhưng chưa đo thứ người dùng thấy (đoạn đúng có lên đầu danh sách không). Đề xuất, chưa làm: một chế độ
`-Rank <k>` của runner nhóm các dòng theo trường `question` của state, xếp mỗi nhóm theo (`yes` > `partly` > `no`, rồi
độ tin giảm dần), và chấm hit@k = tỉ lệ câu hỏi có ít nhất một dòng `yes` trong k dòng đầu; cần fixture mỗi câu hỏi ≥ 5
đoạn. Fixture này đã nhóm sẵn: các dòng chung đúng chữ câu hỏi (hai đến bốn đoạn mỗi câu).

## Nguồn

- Demo Question search over a contract PDF: Jev chấm từng đoạn của một hợp đồng PDF theo câu hỏi của người dùng.
  https://x.com/kourin_crypto/status/2102078920187363787
- Đoạn của fixture bịa theo giọng TCVN, BS, ASHRAE; không chép văn bản tiêu chuẩn nào.
