> Đợt 1 đã làm xong 2026-09-21 (SPEC được duyệt 2026-09-20). Việc làm và bằng chứng: [2026-09-20-m1-chup-va-sua-anh-plan.md](../shell/2026-09-20-m1-chup-va-sua-anh-plan.md)

# Chụp màn hình — SPEC

Bấm một phím tắt, chọn phần màn hình mình cần (vùng chữ nhật, vùng vẽ tay, một cửa sổ, hoặc cả màn hình),
và ảnh đi thẳng tới nơi mình muốn: trình sửa ảnh, clipboard, hoặc file.

## User story

Là người hay gửi ảnh chụp màn hình cho đồng nghiệp, tôi muốn chụp đúng phần cần chụp bằng một phím tắt và một
lần kéo chuột, để khỏi mở ứng dụng, khỏi cắt lại ảnh, và ảnh ra đúng nét dù màn hình 150% hay có hai màn hình.

## What the user does

1. Ứng dụng đang chạy ở khay hệ thống. Người dùng bấm phím tắt của một kiểu chụp, hoặc bấm nút cùng kiểu trên
   thanh chụp, hoặc chọn trong menu khay.
2. Nếu đã đặt độ trễ (ví dụ 5 giây), một số đếm ngược hiện lên; người dùng mở sẵn menu hay tooltip cần chụp.
3. Màn hình mờ đi một chút và **đứng yên**: mọi thứ người dùng thấy từ lúc này là ảnh chụp tại thời điểm bấm
   (hay khi hết đếm ngược), không phải màn hình đang chạy tiếp.
4. Người dùng chọn theo kiểu đã bấm:
   - **Vùng chữ nhật:** kéo chuột từ góc này sang góc kia, theo hướng nào cũng được. Cạnh vùng đang kéo hiện
     kích thước dạng rộng × cao (pixel). Thả chuột là chụp.
   - **Vùng tự do:** giữ chuột và vẽ đường bao quanh thứ cần chụp. Thả chuột là khép đường bao (nối điểm cuối
     về điểm đầu) và chụp.
   - **Cửa sổ:** rê chuột; cửa sổ nằm dưới con trỏ sáng viền và hiện tên. Bấm để chụp cửa sổ đó.
   - **Toàn màn hình:** không phải chọn gì, chụp ngay màn hình đang có con trỏ chuột (hoặc tất cả màn hình, tuỳ
     người dùng đặt).
5. Bấm Esc hoặc chuột phải ở bất kỳ bước nào là huỷ, không có gì được chụp, màn hình trở lại bình thường.
6. Một **hộp thoại "Đã chụp"** hiện ra (mặc định), cho xem ảnh thu nhỏ, kích thước rộng × cao, và bốn việc để chọn:
   **Lưu** (ghi thẳng vào thư mục lưu với tên tự đặt), **Lưu thành…** (chọn nơi và tên), **Sao chép** (vào clipboard),
   **Sửa** (mở trong trình sửa), và **Bỏ** (không giữ ảnh). Chọn xong thì hộp thoại đóng; **Lưu** hay **Sao chép** thì hiện
   một dòng báo nhỏ vài giây: ảnh đã đi đâu. Người dùng có thể đặt trong Cài đặt để bỏ hộp thoại và cho ảnh tự đi thẳng
   tới một nơi, không hỏi.

## Inputs

| Người dùng chọn | Mặc định | Quyết định điều gì |
| --- | --- | --- |
| Kiểu chụp (bấm phím, nút, hay menu) | — | vùng chữ nhật, vùng tự do, cửa sổ, toàn màn hình |
| Độ trễ | 0 giây (chọn 0, 3, 5, 10) | bao lâu sau khi bấm thì màn hình được chụp |
| Toàn màn hình lấy | màn hình có con trỏ chuột | chỉ màn hình đó, hay tất cả màn hình ghép lại |
| Kèm con trỏ chuột | tắt | ảnh có vẽ con trỏ hay không |
| Sau khi chụp | hiện hộp thoại "Đã chụp" | hộp thoại; hoặc bỏ hộp thoại và cho ảnh tự đi thẳng tới trình sửa, clipboard, file, hoặc tổ hợp clipboard và file |
| Thư mục lưu | thư mục Ảnh của người dùng, trong mục Paper.ScreenWizzard | file chụp được ghi ở đâu |
| Định dạng file | PNG (chọn thêm JPG, chất lượng mặc định 90) | file chụp lưu dạng nào |

## Outputs

- Một ảnh: đúng những pixel người dùng thấy trên màn hình lúc chụp, không co giãn.
- Ở nơi người dùng chọn trong hộp thoại (hoặc nơi đã đặt sẵn): cửa sổ trình sửa mở với ảnh; ảnh nằm trong clipboard để
  dán vào ứng dụng khác; hoặc một file mới trong thư mục lưu. Chọn Bỏ thì không có gì được giữ.
- Dòng báo sau khi chụp, và thông báo lỗi khi có bước không làm được (xem bảng "When it does not do the job").

## Key entities

Màn hình (một hay nhiều, mỗi cái có toạ độ trên một desktop chung), vùng chọn, đường bao tự do, cửa sổ, ảnh chụp,
nơi nhận ảnh (trình sửa, clipboard, file).

## Vùng chữ nhật

**Kéo theo hướng nào cũng ra cùng một vùng.** Người dùng hay kéo từ dưới lên hay từ phải sang trái.

- Cho kéo từ điểm (500, 400) tới điểm (300, 250) → vùng có góc trên trái (300, 250), **rộng 200, cao 150**
- Cho kéo từ (300, 250) tới (500, 400) → **cùng vùng đó**

**Vùng bị cắt theo mép desktop.** Kéo ra ngoài màn hình thì ảnh chỉ lấy phần có thật.

- Cho desktop một màn hình 1920 × 1080 (x từ 0 tới 1920) và kéo từ (1800, 500) tới (2100, 700) → vùng **rộng 120,
  cao 200**, không phải rộng 300
- Cho cùng desktop và kéo từ (100, 100) tới (-50, 300) → vùng bắt đầu ở **x = 0**, **rộng 100, cao 200**
- Cho desktop hai màn hình 1920 × 1080 mà màn hình trái nằm ở x từ -1920 tới 0, và kéo từ (-100, 100) tới (100, 300)
  → vùng **rộng 200**: toạ độ âm là hợp lệ, không bị kẹp về 0

**Vùng chụp hai màn hình liền nhau ra một ảnh liền.**

- Cho hai màn hình 1920 × 1080 đặt cạnh nhau, kéo từ (1800, 400) tới (2100, 600) → một ảnh **rộng 300, cao 200**,
  nửa trái là màn hình thứ nhất, nửa phải là màn hình thứ hai, không có khe

**Ảnh ra bằng số pixel thật của màn hình, không phải đơn vị hiển thị.** Màn hình đặt 150% vẫn cho ảnh nét.

- Cho màn hình đặt 150%, người dùng kéo vùng rộng 300 × cao 200 đơn vị nhìn thấy → ảnh **rộng 450, cao 300** pixel
- Cho màn hình đặt 100% và vùng rộng 300 × cao 200 → ảnh **rộng 300, cao 200**

## Vùng tự do

**Ảnh là hình bao chữ nhật của đường bao; ngoài đường bao thì trong suốt.**

- Cho đường bao là tam giác (100, 100), (300, 100), (200, 300) → ảnh **rộng 200, cao 200**
- Cho tam giác đó → điểm ảnh ứng với (200, 150) **đặc**, điểm ứng với (110, 290) **trong suốt**
- Cho đường bao chưa khép (chỉ có hai cạnh của tam giác) → thả chuột thì nối về điểm đầu, kết quả **như tam giác
  đủ ba cạnh**

## Cửa sổ

**Cửa sổ trên cùng dưới con trỏ được chọn.**

- Cho hai cửa sổ chồng nhau và con trỏ nằm trong phần chồng → **cửa sổ nằm trên** sáng viền
- Cho cửa sổ đang thu nhỏ, đang ẩn, hoặc chính lớp phủ chọn vùng của ứng dụng → **không bao giờ được chọn**
- Cho con trỏ đang ở trên nền desktop, không có cửa sổ nào → chọn **cả màn hình** chứa con trỏ

**Ảnh cửa sổ không dính viền bóng trong suốt của Windows.**

- Cho cửa sổ có khung nhìn thấy 1000 × 700 và Windows chừa thêm một viền bóng vô hình ngoài khung → ảnh **rộng 1000,
  cao 700**
- Cho cửa sổ phóng to tràn màn hình 1920 × 1080 (khung thật nhô ra ngoài màn hình vài pixel) → ảnh **rộng 1920,
  cao 1080**

## Toàn màn hình

- Cho hai màn hình 1920 × 1080 và con trỏ ở màn hình thứ hai, chọn "màn hình có con trỏ" → ảnh **1920 × 1080** của
  màn hình thứ hai
- Cho cùng cấu hình, chọn "tất cả màn hình" → **một ảnh 3840 × 1080** ghép hai màn hình

## Độ trễ và con trỏ

- Cho độ trễ 5 giây → **không** có gì được chụp trong 5 giây đầu, và ảnh là màn hình **ở giây thứ 5**; số đếm ngược
  hiện 5, 4, 3, 2, 1
- Cho "kèm con trỏ chuột" tắt → ảnh **không** có con trỏ; bật → ảnh có con trỏ đúng chỗ nó đang đứng
- Ảnh **không bao giờ** chứa lớp mờ, khung chọn, kích thước đang hiện hay số đếm ngược của chính ứng dụng

## Sau khi chụp

**Hộp thoại "Đã chụp" là nơi người dùng quyết định.** Ảnh chưa đi đâu cho tới khi chọn.

- Cho vừa chụp xong, cài đặt mặc định → **hiện hộp thoại** với ảnh thu nhỏ, kích thước, và năm nút: Lưu, Lưu thành…,
  Sao chép, Sửa, Bỏ; **chưa có file nào được ghi, clipboard chưa đổi, trình sửa chưa mở**
- Cho bấm **Sao chép** → clipboard có ảnh, hộp thoại đóng, **không** có file nào được ghi
- Cho bấm **Lưu** → một file ghi vào thư mục lưu theo quy tắc đặt tên bên dưới, hộp thoại đóng, clipboard **không đổi**
- Cho bấm **Lưu thành…** → hộp chọn nơi và tên mở với thư mục lưu và tên tự đặt điền sẵn; huỷ hộp chọn đó thì **quay về
  hộp thoại "Đã chụp"**, ảnh vẫn còn
- Cho bấm **Sửa** → trình sửa mở với ảnh, hộp thoại đóng
- Cho bấm **Bỏ**, hoặc Esc, hoặc nút đóng của hộp thoại → **không giữ gì**: không file, clipboard không đổi, không trình sửa
- Cho hộp thoại đang mở và người dùng bấm phím tắt chụp lần nữa → lần chụp mới chạy, hộp thoại cũ **vẫn ở đó** với ảnh cũ
  của nó; mỗi hộp thoại giữ ảnh của riêng nó
- Cho cài đặt "sau khi chụp" là tự đi thẳng tới clipboard → **không có hộp thoại**, clipboard có ảnh ngay và hiện dòng báo
- Cho cài đặt là tự đi thẳng tới trình sửa → **không có hộp thoại**, trình sửa mở ngay

**Ảnh đi ra đúng như đã chụp, không phụ thuộc nơi nhận.**

- Cho ảnh sao chép vào clipboard → dán vào ứng dụng khác ra **đúng ảnh đó, đúng kích thước**
- Cho ảnh lưu file, PNG, ngày 2026-09-20 lúc 14:03:05 → file tên **Screenshot 2026-09-20 14.03.05.png**
- Cho file cùng tên đã có trong thư mục → file mới tên **Screenshot 2026-09-20 14.03.05 (2).png**, lần nữa thì
  **(3)**; file cũ **không bị ghi đè**
- Cho vùng tự do lưu thành JPG (không có trong suốt) → phần ngoài đường bao ra **màu trắng**, không phải đen
- Cho vùng tự do lưu thành PNG → phần ngoài đường bao **giữ trong suốt**

## Edge cases

- Một lần chỉ có một lượt chụp. Bấm phím tắt của kiểu khác khi đang chọn thì bỏ lượt hiện tại và bắt đầu kiểu mới.
- Toạ độ, kích thước và vùng cắt đều tính trên pixel thật của desktop, không đổi theo mức phóng to của màn hình.
- Con trỏ chuột đứng ở đâu lúc bấm phím tắt cũng không ảnh hưởng vùng, trừ kiểu toàn màn hình và cửa sổ.

## When it does not do the job

| # | Case | What the user sees |
| --- | --- | --- |
| F1 | phím tắt đã bị ứng dụng khác giữ | dòng báo nêu phím nào không đăng ký được; kiểu chụp đó vẫn dùng được bằng thanh chụp và menu khay |
| F2 | vùng kéo nhỏ hơn 3 × 3 pixel | không chụp, hiện "vùng quá nhỏ", vẫn ở màn chọn để kéo lại |
| F3 | đường bao tự do có dưới 3 điểm khác nhau, hoặc diện tích nhỏ hơn 9 pixel vuông | không chụp, hiện "đường bao quá nhỏ", vẫn ở màn chọn |
| F4 | bấm Lưu mà thư mục lưu không tồn tại hoặc không ghi được | hiện lỗi nêu tên thư mục và lý do; **hộp thoại "Đã chụp" vẫn mở** với ảnh còn nguyên để chọn Lưu thành…, Sao chép hay Sửa; không có file nào bị mất im lặng |
| F5 | bấm Sao chép mà clipboard đang bị ứng dụng khác giữ | thử lại vài lần; vẫn không được thì báo "không chép được vào clipboard", **hộp thoại vẫn mở** với ảnh còn nguyên |
| F6 | màn hình đổi giữa lúc đang chọn (rút, cắm, đổi độ phân giải) | huỷ lượt chụp, báo "màn hình đã thay đổi, hãy chụp lại" |
| F7 | cửa sổ có nội dung bảo vệ bản quyền hay game toàn màn hình độc quyền | **silent**: vùng đó ra **đen** và ứng dụng không báo được, vì Windows trả về màu đen; đây là giới hạn của Windows, không phải lỗi ứng dụng |
| F8 | bấm phím tắt kiểu khác khi đang chọn hay đang đếm ngược | lượt cũ bị bỏ, lượt mới bắt đầu ngay; không có hai lớp phủ chồng nhau |
| F9 | ảnh cần ghi lớn tới mức không đủ bộ nhớ | huỷ, báo "ảnh quá lớn, hãy chọn vùng nhỏ hơn"; ứng dụng không đóng đột ngột |
| F10 | đóng hộp thoại "Đã chụp" bằng Bỏ, Esc hay nút đóng | **chủ ý**: ảnh không được giữ ở đâu cả và không có báo gì thêm, vì người dùng vừa chọn bỏ; đây là lý do hộp thoại luôn có nút Sửa và Lưu ngay cạnh |

## Assumptions

- Chọn trên ảnh đóng băng (chụp trước, chọn sau) chứ không chọn trên màn hình đang chạy, để nội dung không đổi
  giữa lúc kéo. Hệ quả: menu hay tooltip muốn chụp thì phải mở sẵn trước khi bấm, hoặc dùng độ trễ.
- Chụp cửa sổ là cắt phần **đang thấy trên màn hình** của cửa sổ đó, nên phần bị cửa sổ khác che sẽ có nội dung của
  cửa sổ che. Chụp được cả cửa sổ bị che là việc khác, chưa làm.
- Phím tắt mặc định lấy theo FastStone: PrintScreen vùng chữ nhật; Shift+PrintScreen vùng tự do; Alt+PrintScreen
  cửa sổ; Ctrl+PrintScreen toàn màn hình. Đổi được trong cài đặt (SPEC của khung ứng dụng).
- Định dạng file chỉ PNG và JPG.

## Clarifications

### Session 2026-09-20

- Q: chọn trên ảnh đóng băng hay trên màn hình đang chạy? -> A: ảnh đóng băng (mặc định của mình, chờ người dùng duyệt).
- Q: toàn màn hình lấy màn hình nào? -> A: mặc định màn hình có con trỏ, có tuỳ chọn tất cả màn hình.
- Q: chụp xong mở thẳng trình sửa hay hỏi? -> A: hỏi, bằng hộp thoại có Lưu, Lưu thành…, Sao chép, Sửa, Bỏ như FastStone
  (Hùng nói 2026-09-20); có cài đặt để bỏ hộp thoại và tự đi thẳng tới một nơi.

## What it does not do yet

- Esc lúc đang đếm ngược: cửa sổ đếm không lấy bàn phím (để Esc của người dùng còn đóng được menu ở chương trình khác); huỷ bằng cách bấm vào con số. Chờ Hùng quyết có đổi không.
- Ảnh không vào được file khi cài đặt là "tự vào file" (không có hộp thoại): hiện hộp lỗi và ảnh mất. Đề xuất: mở hộp "Đã chụp" để ảnh không mất. Chờ Hùng quyết.
- PNG của vùng tự do giữ màu gốc dưới điểm trong suốt (alpha 0). Đề xuất: đặt luôn màu bằng 0. Chờ Hùng quyết.
- Chụp cửa sổ dài phải cuộn (chụp cuộn).
- Kính lúp và toạ độ điểm ảnh khi kéo vùng.
- Chỉnh lại vùng (kéo cạnh, kéo góc) trước khi xác nhận: thả chuột là chụp luôn.
- Chụp cửa sổ bị che, và chụp cửa sổ không có viền (menu, tooltip) như một cửa sổ riêng.
- Lặp lại đúng vùng của lần chụp trước, và vùng có kích thước cố định (FastStone có cả hai).
- Quay màn hình và ghi chú lên màn hình: đợt sau, xem lộ trình.
