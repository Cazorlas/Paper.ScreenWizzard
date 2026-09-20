> Đợt 1 đã làm xong 2026-09-21 (SPEC được duyệt 2026-09-20). Việc làm và bằng chứng: [2026-09-20-m1-chup-va-sua-anh-plan.md](../shell/2026-09-20-m1-chup-va-sua-anh-plan.md)

# Sửa ảnh — SPEC

Ảnh vừa chụp mở ra trong một cửa sổ sửa: vẽ mũi tên, khung, chữ, đánh số bước, làm mờ chỗ nhạy cảm, cắt ảnh, rồi
lưu hoặc chép. Mọi hình vẽ thêm là vật thể riêng cho tới lúc lưu, nên chọn lại, kéo đi, đổi màu hay xoá được.

## User story

Là người viết hướng dẫn và báo lỗi bằng ảnh chụp, tôi muốn chỉ ra chỗ cần xem bằng mũi tên, khung và số thứ tự,
che thông tin nhạy cảm, và cắt bớt phần thừa ngay sau khi chụp, để ảnh gửi đi tự nói được điều tôi muốn nói mà không
phải mở phần mềm vẽ khác.

## What the user does

1. Ảnh mở trong trình sửa ngay sau khi chụp; hoặc người dùng mở một file ảnh (PNG, JPG, BMP), kéo file vào cửa sổ,
   hay dán ảnh từ clipboard.
2. Chọn một công cụ trên thanh công cụ: **Chọn**, **Bút vẽ tay**, **Bút dạ quang**, **Đường thẳng**, **Mũi tên**,
   **Khung chữ nhật**, **Elip**, **Chữ**, **Số bước**, **Làm mờ**, **Cắt**.
3. Chọn màu (tám màu có sẵn hoặc màu tự chọn) và độ dày nét, rồi kéo trên ảnh để vẽ. Với công cụ Chữ thì bấm vào
   chỗ muốn đặt và gõ.
4. Bấm công cụ Chọn rồi bấm một hình để chọn nó: kéo đi được, đổi màu hay độ dày được, bấm Delete để xoá.
5. Phóng to thu nhỏ để vẽ cho chính xác; Ctrl+cuộn chuột đổi mức phóng, có nút "Vừa khung".
6. Ctrl+Z quay lại, Ctrl+Y làm lại.
7. Lưu (Ctrl+S) hoặc "Lưu thành…"; hoặc chép (Ctrl+C) ảnh đã ghép hình vẽ vào clipboard.
8. Đóng cửa sổ; nếu còn thay đổi chưa lưu thì được hỏi: lưu, bỏ, hay quay lại sửa tiếp.

## Inputs

| Người dùng chọn | Mặc định | Quyết định điều gì |
| --- | --- | --- |
| Công cụ | Chọn | hình gì được vẽ khi kéo |
| Màu | đỏ | màu nét, màu chữ, màu số |
| Độ dày nét | 4 pixel (1 tới 20) | độ dày bút, đường, mũi tên, khung |
| Cỡ chữ | 18 pixel | chữ và số bước lớn bao nhiêu |
| Mức phóng | vừa khung | chỉ cách nhìn; **không** đổi cỡ ảnh khi lưu |
| Nơi và tên file khi lưu | thư mục lưu và tên theo quy tắc của chụp màn hình | file ghi ở đâu, dạng PNG hay JPG theo đuôi file |

## Outputs

- File ảnh (PNG hoặc JPG) hoặc ảnh trong clipboard: ảnh gốc với mọi hình vẽ ghép vào, cùng cỡ pixel với ảnh gốc
  (hoặc phần đã cắt).
- Ảnh gốc trong bộ nhớ không đổi cho tới khi lưu; trước lúc lưu, mọi hình vẽ còn sửa được.

## Key entities

Ảnh gốc, hình vẽ (nét tay, dạ quang, đường, mũi tên, khung, elip, chữ, số bước, vùng làm mờ), công cụ, lịch sử thao tác,
vùng cắt.

## Hình vẽ và lịch sử

**Mỗi thao tác là một bước lịch sử.** Vẽ một nét, đổi màu, kéo đi, xoá, cắt: mỗi việc một bước.

- Cho vẽ ba hình rồi bấm Undo hai lần → **còn một hình**
- Cho tình huống đó rồi bấm Redo một lần → **hai hình**
- Cho Undo một bước rồi vẽ hình mới → Redo **không còn** (nhánh cũ bị bỏ)
- Cho chọn một hình rồi đổi màu → Undo trả lại **màu cũ**, hình vẫn được chọn
- Cho vẽ hình xong rồi kéo nó đi → Undo trả nó về **chỗ vẽ ban đầu**, hình không mất

**Bút dạ quang trong suốt một phần.** Chữ dưới nét dạ quang vẫn đọc được.

- Cho nét dạ quang màu vàng kéo ngang qua chữ đen → điểm ảnh của chữ **vẫn đen** sau khi ghép, nền quanh chữ ngả vàng

**Đường thẳng, mũi tên, khung, elip vẽ từ hai góc kéo.** Kéo theo hướng nào cũng ra cùng hình.

- Cho kéo mũi tên từ (100, 100) tới (300, 200) → mũi tên **nhọn ở (300, 200)**
- Cho giữ Shift khi kéo đường thẳng → đường **chỉ nằm ngang, dọc hoặc chéo 45 độ**, gần nhất với hướng kéo
- Cho giữ Shift khi kéo khung → **hình vuông**; khi kéo elip → **hình tròn**

## Chữ

- Cho gõ "Đường ống 45° — thử nghiệm" bằng công cụ Chữ → **hiện đủ dấu tiếng Việt, ký hiệu độ, gạch ngang dài**
- Cho chữ đang gõ → Enter **xuống dòng**; Ctrl+Enter, Esc, hoặc bấm ra ngoài **chốt** chữ
- Cho chữ đã chốt, chọn bằng công cụ Chọn rồi bấm đúp → **sửa lại được**

## Số bước

**Bấm vào ảnh đặt một hình tròn có số, số tăng dần.** Dùng để đánh dấu "bước 1, 2, 3".

- Cho ảnh chưa có số nào → số đầu tiên là **1**, rồi **2**, **3**
- Cho ảnh có số 1, 2, 3 và xoá số 3 → số kế tiếp là **3**
- Cho ảnh có số 1, 2, 3 và xoá số 2 → các số còn lại **giữ nguyên 1 và 3**, số kế tiếp là **4**

## Làm mờ

**Vùng làm mờ là ghép ô vuông, và không khôi phục được từ file đã lưu.** Đây là chỗ che mật khẩu, tên người, số tài
khoản: làm mờ nhẹ mà còn đọc được lại là hỏng việc.

- Cho vùng làm mờ 200 × 100 pixel ở mức mặc định → trong file lưu, vùng đó chia ô **12 × 12 pixel**, mỗi ô **một màu
  duy nhất**
- Cho vùng làm mờ rồi lưu → **không có** cách lấy lại chữ gốc từ file đó
- Cho vùng làm mờ chưa lưu → vẫn di chuyển và xoá được; xoá thì vùng gốc **hiện lại**

## Cắt

**Cắt giữ phần đã chọn, đưa các hình vẽ theo, và bỏ được bằng Undo.**

- Cho ảnh 1000 × 600, chọn vùng cắt từ (100, 50) tới (500, 350), bấm Enter → ảnh mới **rộng 400, cao 300**
- Cho hình vẽ nằm ở (150, 100) trong ảnh gốc → sau cắt nó nằm ở **(50, 50)**
- Cho hình vẽ nằm hoàn toàn ngoài vùng cắt → **bị bỏ khỏi ảnh** (Undo thì trở lại)
- Cho hình vẽ bị vùng cắt cắt ngang → **phần nằm ngoài bị bỏ** khi lưu
- Cho vùng cắt kéo ra ngoài ảnh → **kẹp theo mép ảnh**

## Lưu và chép

- Cho ảnh vừa chụp chưa có tên, bấm Ctrl+S → **hộp Lưu thành** mở với thư mục và tên theo quy tắc của chụp màn hình
- Cho ảnh mở từ file rồi sửa và bấm Ctrl+S → **hỏi một lần** "ghi đè file gốc hay lưu bản mới"; chọn ghi đè thì các lần
  Ctrl+S sau ghi thẳng, không hỏi lại
- Cho lưu ảnh có phần trong suốt thành JPG → phần trong suốt **trắng**, không đen
- Cho Ctrl+C → clipboard có **ảnh đã ghép mọi hình vẽ**, đúng cỡ pixel của ảnh, **không phụ thuộc mức phóng** đang xem
- Cho mức phóng 400% khi lưu → file **vẫn** đúng cỡ pixel của ảnh gốc
- Cho hình vẽ ở (150, 100) khi xem ở 100% → trong file lưu nó **đúng (150, 100)**, không lệch

## Edge cases

- Mỗi cửa sổ sửa giữ một ảnh; mở ảnh khác thì mở cửa sổ mới.
- Ảnh có chữ tiếng Việt và ký tự Unicode phải hiện và lưu đúng dấu.
- Màu và độ dày đổi trong lúc chọn một hình thì đổi **hình đang chọn**; không chọn hình nào thì đổi **cho hình vẽ sau**.

## When it does not do the job

| # | Case | What the user sees |
| --- | --- | --- |
| F1 | lưu không được (ổ đầy, file chỉ đọc, file đang mở ở nơi khác) | thông báo nêu đường dẫn và lý do; các chỉnh sửa vẫn còn; cửa sổ **không** đóng |
| F2 | mở file không phải ảnh, hoặc ảnh hỏng | thông báo nêu tên file và "không đọc được"; không có cửa sổ trắng nào mở ra |
| F3 | dán khi clipboard không có ảnh | thông báo "clipboard không có ảnh"; ảnh đang sửa không đổi |
| F4 | công cụ Chữ chốt khi chưa gõ gì | **không** tạo chữ nào, và không báo gì: bỏ một khung chữ rỗng là ý người dùng, không phải lỗi |
| F5 | vùng cắt nhỏ hơn 1 × 1 pixel, hoặc nằm hoàn toàn ngoài ảnh | không cắt, ảnh giữ nguyên, hiện "vùng cắt không hợp lệ" |
| F6 | Undo hay Redo khi không còn bước nào | nút **mờ đi**, phím tắt không làm gì; không báo lỗi |
| F7 | đóng cửa sổ khi còn thay đổi chưa lưu | hỏi lưu, bỏ, hay quay lại; **không** đóng im lặng làm mất chỉnh sửa |
| F8 | ảnh mở quá lớn không đủ bộ nhớ | thông báo "ảnh quá lớn để mở"; ứng dụng không đóng đột ngột |
| F9 | ghi đè file gốc mà file đó vừa bị đổi hoặc xoá bởi chương trình khác | thông báo nêu file, và đề nghị "Lưu thành…"; không ghi im lặng vào chỗ khác |

## Assumptions

- Chỉ đọc và ghi PNG, JPG, BMP (BMP chỉ đọc). GIF, WebP, PDF: chưa.
- Làm mờ là ghép ô vuông (pixel hoá), không phải làm nhoè, vì nhoè nhẹ có thể đảo ngược.
- Số bước không đánh lại khi xoá một số giữa chừng: người dùng đã nhắc "bước 2" trong văn bản đi kèm nên số đã đặt
  không tự đổi.
- Phím tắt trong trình sửa theo Paint và FastStone: Ctrl+Z, Ctrl+Y, Ctrl+S, Ctrl+C, Ctrl+V, Delete, Esc.

## Clarifications

### Session 2026-09-20

- Q: làm mờ nhoè hay ghép ô vuông? -> A: ghép ô vuông 12 pixel (mặc định của mình, chờ người dùng duyệt).
- Q: xoá số 2 giữa chừng thì đánh lại số? -> A: không đánh lại (mặc định của mình, chờ người dùng duyệt).

## What it does not do yet

- Hộp của chữ, dùng để chọn và để giữ hay bỏ chữ khi cắt, là ước lượng (0,6 × cỡ chữ mỗi ký tự), không đo bằng phông: chữ toàn ký tự rộng có thể lòi ra ngoài hộp.
- BMP chỉ đọc: "Lưu" một BMP đã mở hỏi "Lưu thành" và đề xuất tên PNG; tên lưu có đuôi lạ được thêm ".png".
- Tẩy từng phần nét bút (chỉ xoá cả hình đã chọn).
- Xoay, lật, đổi cỡ ảnh, đổ bóng, khung viền ảnh.
- Hiệu ứng như chỉnh sáng tối, độ tương phản.
- Mở nhiều ảnh trong một cửa sổ (tab).
- Chèn ảnh khác lên ảnh.
- Lưu ở dạng có thể sửa tiếp (giữ các hình vẽ tách rời) sau khi đóng cửa sổ.
