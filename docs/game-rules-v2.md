# Ticket to Ride Vietnam - Luật chơi v1.1

**Dự án:** Ticket to Ride Vietnam  
**Học phần:** NT106  
**Cập nhật:** 26/11/2026  
**Trạng thái:** Bản nháp; các quy tắc đã thống nhất và các nội dung còn chờ xác nhận được phân biệt rõ bên dưới.

> **Cách đọc trạng thái**
> - **ĐÃ CHỐT:** Quy tắc nhóm đã thống nhất hoặc đã được xác nhận trong phần ghi chú cập nhật.
> - **CHỜ XÁC NHẬN:** Chưa có quyết định cuối cùng; không tự suy diễn khi triển khai.
> - **ĐỀ XUẤT:** Gợi ý kỹ thuật hoặc quy tắc cần nhóm duyệt trước khi xem là luật chính thức.

## 1. Tổng quan

Ticket to Ride Vietnam là trò chơi bàn cờ đường sắt nhiều người chơi, lấy cảm hứng từ Ticket to Ride và xây dựng chủ đề xoay quanh Việt Nam. Người chơi thu thập thẻ tàu, chiếm các tuyến đường sắt, hoàn thành vé nhiệm vụ và cạnh tranh để đạt tổng điểm cuối trận cao nhất.

Bản đồ lấy cảm hứng từ địa lý Việt Nam. Cân bằng lối chơi và các tuyến đường thay thế được ưu tiên hơn việc mô phỏng chính xác khoảng cách địa lý.

## 2. Người chơi và thành phần

- Mỗi trận hỗ trợ từ 2 đến 4 người chơi.
- Mỗi người chơi có 45 toa tàu.
- Mỗi người chơi sử dụng một màu tàu riêng biệt.
- Bộ thẻ tàu gồm 110 lá:
  - 8 màu thông thường, mỗi màu 12 lá (tổng cộng 96 lá).
  - 14 lá đầu máy (Locomotive).
- Bản đồ có hơn 40 địa điểm và phải có Quảng Ngãi.
- Bộ vé nhiệm vụ dự kiến có khoảng 60 vé. Danh sách địa điểm và điểm số chính xác sẽ được mô tả riêng.

## 3. Chuẩn bị ván chơi

1. Mỗi người chơi nhận 4 thẻ tàu.
2. Lật ngửa 5 thẻ tàu ở khu vực thẻ mở.
3. Mỗi người chơi rút 3 vé nhiệm vụ và phải giữ lại ít nhất 1 vé.
4. Cách xác định người đi đầu tiên: **CHỜ XÁC NHẬN**.

## 4. Cấu trúc lượt chơi

Trong mỗi lượt, người chơi phải thực hiện đúng một hành động:

1. Rút thẻ tàu.
2. Chiếm một tuyến đường sắt.
3. Rút vé nhiệm vụ.

Người chơi không được thực hiện nhiều hơn một hành động trong cùng lượt.

## 5. Rút thẻ tàu

- Thông thường, người chơi được rút tối đa 2 thẻ tàu trong một lượt.
- Nếu thẻ mở đầu tiên người chơi rút là Locomotive, người chơi chỉ được lấy lá đó trong lượt này.
- Nếu trong 5 thẻ mở có từ 3 lá Locomotive trở lên, bỏ cả 5 lá và lật 5 lá thay thế.
- Khi chồng thẻ rút hết và còn thẻ ở chồng bỏ, xáo chồng bỏ để tạo chồng rút mới.
- Nếu cả chồng rút và chồng bỏ đều không còn thẻ, người chơi không thể rút thêm thẻ tàu.

## 6. Chiếm tuyến đường

- Độ dài tuyến đường từ 1 đến 6 toa tàu.
- Để chiếm tuyến đường, người chơi phải đánh ra bộ thẻ tàu hợp lệ, phù hợp với màu và độ dài tuyến đường, đồng thời còn đủ toa tàu để đặt.
- Điểm tuyến đường được cộng ngay khi tuyến được chiếm.
- Tuyến đường đã có người chiếm không thể bị người chơi khác chiếm lại.
- Nếu tuyến mong muốn đã bị chiếm, người chơi phải chọn tuyến thay thế hoặc đường vòng còn trống.
- Quy tắc tuyến đường đôi:
  - Trận 2-3 người: chỉ một nhánh của tuyến đôi được phép chiếm.
  - Trận 4 người: cả hai nhánh đều có thể được chiếm.

### 6.1. Điểm chiếm tuyến đường

| Độ dài tuyến đường | Điểm |
|---:|---:|
| 1 | 1 |
| 2 | 2 |
| 3 | 4 |
| 4 | 7 |
| 5 | 10 |
| 6 | 15 |

## 7. Rút vé nhiệm vụ

- Người chơi rút 3 vé nhiệm vụ và phải giữ lại ít nhất 1 vé.
- Nếu trong chồng rút còn đúng 3 vé, xáo các vé nhiệm vụ đã bỏ trở lại chồng trước khi rút.
- Nếu cả chồng rút và chồng bỏ đều không còn vé, người chơi không thể rút vé nhiệm vụ và phải chọn hành động khác.
- Danh sách địa điểm và điểm số của từng vé sẽ được mô tả trong tài liệu riêng.

## 8. Kết thúc ván chơi

Khi một người chơi còn 2 toa tàu hoặc ít hơn sau khi hoàn thành lượt của mình, tất cả người chơi, bao gồm người đó, được thực hiện thêm một lượt cuối.

Sau khi các lượt cuối kết thúc:

- Vé nhiệm vụ hoàn thành được cộng số điểm ghi trên vé.
- Vé nhiệm vụ chưa hoàn thành bị trừ số điểm ghi trên vé.
- Điểm tuyến đường đã nhận vẫn được giữ trong tổng điểm.
- Người chơi có tổng điểm cuối trận cao nhất là người thắng; nếu nhiều người bằng điểm, áp dụng quy tắc phân hạng tại mục 10.1 đối với Rank.
- Quy tắc phá hòa cuối trận cho việc xác định người thắng chung cuộc: **CHỜ XÁC NHẬN** nếu các tiêu chí ở mục 10.1 không đủ để xác định thứ tự.

## 9. Tuyến đường liên tục dài nhất

- Người chơi có tuyến đường liên tục dài nhất nhận thưởng 10 điểm.
- Nếu nhiều người đồng hạng tuyến dài nhất, mỗi người trong nhóm đồng hạng đều nhận 10 điểm.
- Thuật toán xác định tuyến liên tục, bao gồm nhánh và vòng lặp, được mô tả trong thiết kế kỹ thuật.

## 10. Tài khoản, Rank, Uy tín và chế độ xếp hạng

### 10.1. Điểm Rank sau trận

**ĐÃ CHỐT**

- Mỗi tài khoản bắt đầu với **0 điểm Rank**.
- Rank và Uy tín là hai hệ thống độc lập.
- Điểm Rank được tính theo thứ hạng cuối trận và số người tham gia trận.
- Điểm Rank tối thiểu là **0**. Điểm Rank không có giới hạn tối đa; người chơi tiếp tục tích lũy điểm khi tiếp tục chơi.
- Theo mô tả chế độ trong giao diện sảnh chờ hiện tại, trận **Xếp hạng** tính điểm Rank; trận **Đấu thường** không tính điểm Rank.

#### Bảng điểm theo số người chơi

| Số người chơi | Hạng 1 | Hạng 2 | Hạng 3 | Hạng 4 |
|---:|---:|---:|---:|---:|
| 2 người | +50 | -20 | — | — |
| 3 người | +50 | +20 | -20 | — |
| 4 người | +50 | +30 | +15 | -20 |

Hạng chót được xác nhận là **-20 điểm Rank**.

#### Phân hạng khi đồng điểm

Khi hai hoặc nhiều người có cùng tổng điểm cuối trận, lần lượt áp dụng:

1. Người hoàn thành nhiều vé nhiệm vụ hơn được xếp trên.
2. Nếu vẫn bằng nhau, người có tuyến đường liên tục dài hơn được xếp trên.
3. Nếu vẫn bằng nhau, những người đó được xem là đồng hạng.

**Cách tính điểm khi đồng hạng - theo ghi chú xác nhận mới nhất:**

- Mỗi người đồng hạng nhận mức điểm Rank tương ứng với vị trí mà nhóm đó cùng đạt được.
- Các vị trí đã được nhóm đồng hạng chiếm sẽ được bỏ qua khi xếp những người còn lại.
- Ví dụ: trong trận 4 người, nếu 2 người đồng hạng 1, cả hai cùng nhận +50; người tiếp theo được tính là hạng 3 và nhận +15; người cuối cùng là hạng 4 và nhận -20.
- Nếu đồng hạng ở vị trí cuối, mỗi người đồng hạng nhận mức trừ của vị trí cuối là -20.

### 10.2. Các bậc Rank

Bậc Rank được xác định theo số điểm Rank hiện tại. Mỗi mốc là số điểm tối thiểu để đạt bậc tương ứng.

| Điểm Rank tối thiểu | Tên bậc Rank |
|---:|---|
| 0 | Học việc đường ray |
| 300 | Phụ toa |
| 600 | Soát vé |
| 1.000 | Trưởng toa |
| 1.500 | Phụ lái |
| 2.000 | Lái tàu |
| 3.000 | Trưởng tàu |
| 4.000 | Tổng điều độ |
| 5.000 | Huyền thoại đường ray |
| 10.000 | Vua tàu lửa |

**ĐÃ CHỐT:**

- Người chơi có từ 10.000 điểm trở lên giữ bậc **Vua tàu lửa**.
- Điểm Rank vẫn tiếp tục tăng trên 10.000; không giới hạn điểm tối đa.
- Khi điểm giảm, bậc Rank được xác định lại theo mốc điểm hiện tại.

### 10.3. Điểm Uy tín

**ĐÃ CHỐT**

- Tài khoản mới bắt đầu với **100 điểm Uy tín**.
- Hoàn thành trận đấu được cộng **+5 Uy tín**.
- Mỗi lần vi phạm do tự ý rời trận bị trừ **-10 Uy tín**.
- Người tự ý rời trận không nhận +5 Uy tín hoàn thành trận.
- Khi Uy tín dưới 30, tài khoản bị áp dụng thời hạn cấm tham gia theo số lần vi phạm (xem mục 10.4).
- Khi Uy tín về 0, người chơi bị hạn chế tham gia cho đến khi Uy tín được khôi phục.
- Việc chơi với bạn bè vẫn được cho phép khi Uy tín thấp; trận chơi với bạn bè không được cộng điểm Uy tín.

Tính từ 100 điểm và chưa tính các lần được cộng +5 do hoàn thành trận, lần vi phạm thứ 8 làm Uy tín còn 20 (dưới 30); lần vi phạm thứ 10 làm Uy tín về 0.

### 10.4. Mức phạt khi tự ý rời trận nhiều lần

Nguyên tắc đã chốt: số lần vi phạm càng nhiều thì mức trừ Rank càng nặng. Mỗi lần vi phạm đều trừ 10 Uy tín và người chơi không nhận +5 hoàn thành trận.

| Số lần vi phạm | Rank bị trừ | Uy tín bị trừ | Thời gian chờ trước khi được vào trận tiếp theo | Thời hạn cấm khi Uy tín dưới 30 |
|---:|---:|---:|---:|---:|
| Lần 1 | -15 | -10 | 1 phút | 10 phút |
| Lần 2 | -30 | -10 | 3 phút | 15 phút |
| Lần 3 | -50 | -10 | 5 phút | 30 phút |
| Lần 4 trở lên | -70 | -10 | 10 phút | 60 phút |

**Cách áp dụng:**

- Thời gian chờ trước khi vào trận tiếp theo và thời hạn cấm khi Uy tín dưới 30 là hai thông tin riêng trong bảng.
- Cột thời hạn cấm chỉ áp dụng khi Uy tín đã dưới 30; không tự động áp dụng chỉ vì số lần vi phạm tăng.
- Việc thời gian chờ 1/3/5/10 phút có được cộng thêm một cơ chế tìm trận chậm riêng (ví dụ từ 150 giây ở lần 2 trở đi) hay không: **CHỜ XÁC NHẬN**. Ghi chú gốc có đề cập cơ chế tìm trận lâu hơn nhưng chưa đủ rõ liệu đó là cùng thời gian chờ trong bảng hay một cơ chế độc lập.

### 10.5. Phân loại các tình huống rời trận và mất kết nối

#### A. Tự ý rời trận

- Người chơi chủ động gửi lệnh rời trận khi trận đang diễn ra và Server nhận được lệnh.
- Tính là một lần vi phạm.
- Trừ Rank theo bảng số lần vi phạm ở mục 10.4.
- Trừ 10 Uy tín và không nhận +5 Uy tín hoàn thành trận.

#### B. Mất kết nối nhưng khôi phục thành công hoặc lỗi do Server

- Nếu người chơi mất kết nối nhưng quay lại bằng `resumeToken` trước khi hết thời hạn chờ, thiết kế hiện tại dự kiến tiếp tục ván đấu và tính kết quả bình thường.
- Nếu Server xác định nguyên nhân là lỗi phía Server (ví dụ Server khởi động lại), không xem người chơi là tự ý rời trận.
- Việc không áp dụng phạt trong mọi trường hợp reconnect thành công cần được nhóm xác nhận rõ: **CHỜ XÁC NHẬN** theo nhãn trong bảng tình huống của tài liệu đặc tả.

#### C. Mất kết nối và không quay lại

**ĐÃ CHỐT:**

- Nếu hết thời hạn chờ mà người chơi không quay lại, xử lý như tình huống A (tự ý rời trận), để tránh lợi dụng việc tắt mạng hoặc đóng ứng dụng nhằm né hình phạt.
- Ngoại lệ: nếu Server xác định nguyên nhân xuất phát từ lỗi phía Server, xử lý như tình huống B.
- Đóng ứng dụng giữa trận được xếp vào tình huống C. Nếu người chơi không quay lại trong thời hạn cho phép, áp dụng quy tắc của tình huống C ở trên.

#### Thời gian phát hiện và chờ kết nối lại

Các thông số đã có trong thiết kế kết nối:

- Heartbeat được gửi mỗi 15 giây.
- Quá 45 giây không nhận tín hiệu thì Server xem kết nối là mất.
- Khi người chơi đang đến lượt, thời gian chờ kết nối lại là 60 giây.
- Khi không phải lượt của người chơi đó, thời hạn chờ tối đa là 120 giây.
- Sau khi Server khởi động lại có bước xác nhận khôi phục phiên.

### 10.6. Trận 2 người khi một người rời giữa chừng

**ĐÃ CHỐT:** Nếu trận có 2 người và một người rời giữa chừng, người còn lại được tính thắng ngay và nhận **+50 điểm Rank**.

**CHỜ XÁC NHẬN:** Với người tự ý rời trận, khoản phạt Rank lũy tiến ở mục 10.4 được cộng thêm vào mức -20 của hạng chót hay thay thế mức -20 đó? Không tự cộng hai khoản cho đến khi nhóm quyết định.

### 10.7. Hạn chế khi Uy tín thấp và khôi phục Uy tín

Theo ghi chú cập nhật của nhóm:

- Khi Uy tín dưới 30, người chơi vẫn có thể chơi với bạn bè.
- Chơi với bạn bè không làm tăng Uy tín.
- Chơi offline với máy là phương thức được ghi nhận để khôi phục Uy tín khi Uy tín thấp hoặc bằng 0.

**CHỜ XÁC NHẬN:**

- Cần xác định số Uy tín được hồi sau mỗi trận offline, điều kiện một trận được tính hợp lệ và có giới hạn hồi theo ngày hay không.
- Cần xác nhận rõ lệnh cấm khi Uy tín bằng 0 có chặn cả phòng bạn bè hay chỉ chặn tìm trận công khai/xếp hạng. Ghi chú hiện tại nói vẫn được chơi với bạn bè khi Uy tín thấp, đồng thời quy định Uy tín bằng 0 không được tiếp tục tham gia cho đến khi hồi phục.

## 11. Chống lợi dụng việc thoát trận và ghi nhận dữ liệu

Các điểm dưới đây được giữ ở mức **ĐỀ XUẤT kỹ thuật** trong tài liệu đặc tả, không tự xem là luật chơi đã chốt:

- Server là bên duy nhất xác định người chơi thuộc tình huống A, B hay C; Client không được tự khai báo kết quả phân loại.
- Việc xử phạt dựa trên hành vi rời trận, không dựa vào người chơi đang thắng hay thua tại thời điểm rời.
- Khi một vi phạm đã được xác nhận, việc kết nối lại sau đó không tự động xóa vi phạm.
- Mỗi cặp (trận, tài khoản) chỉ được áp dụng hình phạt một lần, kể cả khi Server thử lại hoặc khôi phục sau lỗi.
- Việc ghi kết quả trận và trừ điểm nên được thực hiện trong cùng giao dịch SQL để tránh trừ điểm trùng.
- Người chơi ở lại không bị trừ điểm hoặc Uy tín vì người khác rời trận.
- Dữ liệu kỹ thuật được đề xuất lưu gồm số lần vi phạm tích lũy, thời điểm hết hạn cấm và trạng thái rời trận của từng người chơi trong `MatchPlayers`.

## 12. Chế độ chơi

Theo mô tả hiện có trong giao diện sảnh chờ:

- **Xếp hạng:** kết quả trận ảnh hưởng đến điểm Rank và Uy tín.
- **Đấu thường:** không tính điểm Rank.
- **Chơi offline với máy:** không đồng bộ kết quả lên Server; được ghi nhận là phương thức khôi phục Uy tín khi Uy tín thấp.
- **Chơi với bạn bè:** vẫn được phép khi Uy tín thấp; không cộng điểm Uy tín.

**CHỜ XÁC NHẬN:** Tác động chính xác của chế độ Đấu thường đến Uy tín (cộng +5 khi hoàn thành hay không) cần được nhóm chốt.

## 13. Các nội dung còn chờ xác nhận

1. Cơ chế tìm trận lâu hơn từ lần vi phạm thứ 2 (ghi chú nêu ví dụ khoảng 150 giây trở lên): đây là thời gian chờ riêng hay chính là thời gian 1/3/5/10 phút trong bảng mục 10.4?
2. Khi tự ý rời trận, mức phạt Rank lũy tiến có cộng thêm vào mức -20 hạng chót hay thay thế mức -20?
3. Cách hồi Uy tín qua trận offline với máy: số điểm hồi, điều kiện hợp lệ và giới hạn hồi.
4. Khi Uy tín bằng 0, người chơi có được tiếp tục vào phòng bạn bè hay phải khôi phục Uy tín trước? Ghi chú hiện tại có hai cách diễn đạt cần thống nhất.
5. Trận Đấu thường có được cộng +5 Uy tín khi hoàn thành hay không?
6. Xử lý tình huống B khi người chơi reconnect thành công trước thời hạn: xác nhận chính thức rằng không phạt và tiếp tục tính kết quả bình thường.
7. Quy tắc phá hòa để xác định người thắng chung cuộc khi tổng điểm cuối trận bằng nhau, nếu các tiêu chí vé nhiệm vụ và tuyến đường liên tục vẫn không phân định được.
8. Số lần vi phạm tích lũy có được reset sau một khoảng thời gian hay không.

## 14. Lịch sử cập nhật

- **v1.1 - 26/11/2026:** Cập nhật điểm Rank khởi đầu về 0; bảng Rank theo số người chơi; cách xử lý đồng hạng; sàn Rank 0 và không giới hạn trần; bổ sung 10 bậc Rank từ Học việc đường ray đến Vua tàu lửa; cập nhật bảng trừ Rank/Uy tín và thời hạn chờ/cấm; bổ sung quy tắc mất kết nối, đóng ứng dụng giữa trận và trận 2 người khi một người rời.
- **v1.0:** Bản luật nền trước khi cập nhật chính sách Rank và Uy tín.
