# 🏸 Hướng Dẫn Sử Dụng API Đề Xuất Cơ Sở Thể Thao (Venue Recommendations)

Tài liệu này hướng dẫn chi tiết cách sử dụng, các tham số, cơ chế tính điểm đề xuất và tích hợp API **Venue Recommendations** (`GET /api/v1/venues/recommendations`) cho Client (Mobile Flutter / Web / Postman).

---

## 1. Tổng Quan API

- **Endpoint**: `GET /api/v1/venues/recommendations`
- **Quyền truy cập**: Public (cho phép cả khách vãng lai và người dùng đã đăng nhập).
- **Mục tiêu**:
  1. Trả về danh sách các cơ sở thể thao được chấm điểm và xếp hạng tốt nhất.
  2. Hỗ trợ **linh hoạt cả 2 kịch bản**: Người dùng **BẬT** vị trí GPS hoặc **TẮT / KHÔNG CẤP QUYỀN** vị trí.
  3. Hỗ trợ lọc theo môn thể thao và khung giờ còn sân trống.
  4. Hỗ trợ **phân trang (Pagination / Infinite Scroll)** mượt mà, ổn định không trùng lặp record.
  5. **Tối ưu hiệu năng 100% tại Database**: Tính khoảng cách Haversine, Rating trung bình, Favourite log score và phân trang trực tiếp bằng SQL, không load toàn bộ dữ liệu vào RAM.

---

## 2. Danh Sách Query Parameters

Tất cả các tham số đều được truyền qua Query String:

| Tham số | Kiểu dữ liệu | Bắt buộc? | Mặc định | Mô tả |
| :--- | :--- | :---: | :---: | :--- |
| `latitude` | `double` | Không | `null` | Vĩ độ hiện tại của người dùng (từ -90 đến 90). |
| `longitude` | `double` | Không | `null` | Kinh độ hiện tại của người dùng (từ -180 đến 180). |
| `maxDistanceKm`| `double` | Không | `20.0` | Bán kính tìm kiếm tối đa (km). Chỉ áp dụng khi có truyền toạ độ. |
| `sportTypeId` | `Guid` | Không | `null` | Lọc theo môn thể thao (VD: Cầu lông, Pickleball, Tennis...). |
| `date` | `DateOnly` (`yyyy-MM-dd`) | Không | `null` | Ngày muốn đặt sân (Bắt buộc nếu truyền `startTime` / `endTime`). |
| `startTime` | `TimeOnly` (`HH:mm:ss`) | Không | `null` | Giờ bắt đầu khung giờ cần tìm sân trống. |
| `endTime` | `TimeOnly` (`HH:mm:ss`) | Không | `null` | Giờ kết thúc khung giờ cần tìm sân trống. |
| `pageNumber` | `int` | Không | `1` | Số thứ tự trang (>= 1). |
| `pageSize` | `int` | Không | `20` | Số lượng bản ghi trên một trang (1 - 100). |

> [!NOTE]
> - Nếu người dùng **không truyền toạ độ** (`latitude` và `longitude` để trống): Hệ thống sẽ đề xuất dựa trên **Đánh giá (Rating)** và **Độ yêu thích (Favourite)** của cơ sở trên toàn hệ thống.
> - Nếu truyền toạ độ: Bắt buộc phải truyền **cả 2** (`latitude` và `longitude`).

---

## 3. Thuật Toán Chấm Điểm Đề Xuất (Recommendation Score)

Điểm đề xuất (`recommendationScore`) được tính theo thang điểm **0 đến 100**.

### Kịch bản 1: Người dùng CÓ truyền vị trí
Công thức tính điểm kết hợp 3 thành phần:
$$\text{RecommendationScore} = (\text{DistanceScore} \times 0.50) + (\text{RatingScore} \times 0.35) + (\text{FavouriteScore} \times 0.15)$$

1. **DistanceScore (50%) - Khoảng cách càng gần điểm càng cao**:
   $$\text{DistanceScore} = \max\left(0, 100 \times \left(1 - \frac{\text{DistanceKm}}{\text{MaxDistanceKm}}\right)\right)$$
2. **RatingScore (35%) - Đánh giá sao từ khách hàng**:
   $$\text{RatingScore} = \frac{\text{AverageRating}}{5.0} \times 100$$
3. **FavouriteScore (15%) - Lượt yêu thích (áp dụng hàm Logarithm để tránh cơ sở cũ áp đảo)**:
   $$\text{FavouriteScore} = \min\left(100, \frac{\ln(1 + \text{FavouriteCount})}{\ln(1 + 1000)} \times 100\right)$$

**Thứ tự sắp xếp (Tie-breaker)**:
`RecommendationScore DESC` $\rightarrow$ `AverageRating DESC` $\rightarrow$ `DistanceKm ASC` $\rightarrow$ `Venue.Id ASC`.

---

### Kịch bản 2: Người dùng KHÔNG truyền vị trí
Khi không có vị trí, `distanceKm` sẽ trả về `null`. Trọng số được chuẩn hoá lại dựa trên chất lượng và độ uy tín:
$$\text{RecommendationScore} = (\text{RatingScore} \times 0.70) + (\text{FavouriteScore} \times 0.30)$$

**Thứ tự sắp xếp (Tie-breaker)**:
`RecommendationScore DESC` $\rightarrow$ `AverageRating DESC` $\rightarrow$ `FavouriteCount DESC` $\rightarrow$ `Venue.Id ASC`.

---

## 4. Lọc Tính Khả Dụng Khung Giờ (Time Availability Filter)

Nếu Client truyền bộ 3 tham số `date`, `startTime`, `endTime`, API sẽ chỉ đề xuất các cơ sở thỏa mãn đồng thời:
1. Cơ sở đang hoạt động (`IsActive = true`).
2. Giờ mở cửa của cơ sở bao phủ khung giờ yêu cầu: `OpenTime <= startTime` và `CloseTime >= endTime`.
3. Có ít nhất một sân đấu (`Court`):
   - Đang hoạt động (`IsActive = true`).
   - Đúng môn thể thao nếu có chọn `sportTypeId`.
   - **Không bị trùng lịch đặt sân**: Không có `BookingDetail` (trừ trạng thái Cancelled) giao thoa với khung giờ yêu cầu.
   - **Không bị trùng lịch bảo trì**: Không có `CourtMaintenance` giao thoa với khung giờ yêu cầu.

---

## 5. Xác Thực & Nhận Diện Người Dùng (Authentication)

- API **không yêu cầu truyền `userId` qua Query/Body**.
- Nếu người dùng đã đăng nhập, Client gửi Header:
  ```http
  Authorization: Bearer <JWT_ACCESS_TOKEN>
  ```
- Backend tự động trích xuất `UserId` từ JWT Claims để xác định cờ:
  - `isFavourite: true` nếu user hiện tại đã thả tim cơ sở này.
  - `isFavourite: false` nếu chưa thả tim hoặc là khách vãng lai (chưa đăng nhập).

---

## 6. Cấu Trúc Dữ Liệu Trả Về (Response JSON)

```json
{
  "items": [
    {
      "idVenue": "4cb37f68-7fb0-4e3a-b8cb-46540c5f2122",
      "nameVenue": "Legends Badminton Center Cầu Giấy",
      "address": "123 Đường Dịch Vọng Hậu, Cầu Giấy, Hà Nội",
      "latitude": 21.0312,
      "longitude": 105.7891,
      "primaryImageUrl": "https://storage.gig.vn/venues/caugiay.jpg",
      "distanceKm": 1.25,
      "averageRating": 4.8,
      "reviewCount": 142,
      "favouriteCount": 380,
      "minPricePerHour": 80000.00,
      "sportTypes": [
        {
          "id": "a90bcf2e-9d22-4a0b-967a-e45f9c464e81",
          "name": "Cầu Lông"
        }
      ],
      "recommendationScore": 92.4,
      "isFavourite": true
    }
  ],
  "totalCount": 35,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 2,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Chi tiết các trường dữ liệu:
- `items`: Danh sách các cơ sở được đề xuất theo thứ tự điểm cao nhất.
  - `idVenue`: ID định danh duy nhất của sân (`Guid`).
  - `nameVenue`: Tên cơ sở.
  - `address`: Địa chỉ cơ sở.
  - `latitude` & `longitude`: Toạ độ địa lý của cơ sở.
  - `primaryImageUrl`: Ảnh đại diện chính của cơ sở (hoặc ảnh đầu tiên nếu chưa set ảnh chính).
  - `distanceKm`: Khoảng cách từ vị trí người dùng đến cơ sở (km, làm tròn 2 số thập phân). Trả về `null` nếu request không truyền toạ độ.
  - `averageRating`: Điểm đánh giá trung bình từ 1.0 đến 5.0 (0 nếu chưa có review).
  - `reviewCount`: Tổng số lượt đánh giá.
  - `favouriteCount`: Tổng số lượt người dùng yêu thích cơ sở này.
  - `minPricePerHour`: Mức giá thấp nhất mỗi giờ (VNĐ) lấy từ bảng giá hoặc danh sách sân.
  - `sportTypes`: Danh sách các môn thể thao mà cơ sở hỗ trợ.
  - `recommendationScore`: Điểm đề xuất tổng hợp (thang điểm 100).
  - `isFavourite`: `true` nếu user đang đăng nhập đã thích sân này, ngược lại `false`.
- Thông tin phân trang:
  - `totalCount`: Tổng số lượng cơ sở thỏa mãn điều kiện lọc.
  - `pageNumber`: Trang hiện tại.
  - `pageSize`: Số lượng bản ghi trên 1 trang.
  - `totalPages`: Tổng số trang.
  - `hasNextPage`: `true` nếu còn trang kế tiếp (để trigger lazy load / infinite scroll).
  - `hasPreviousPage`: `true` nếu có trang trước đó.

---

## 7. Các Ví Dụ Request Thực Tế

### Ví Dụ 1: Đề xuất mặc định (User không bật định vị / Web vãng lai)
```http
GET /api/v1/venues/recommendations?pageNumber=1&pageSize=20
```

### Ví Dụ 2: Đề xuất theo vị trí GPS của người dùng (Bán kính 10km)
```http
GET /api/v1/venues/recommendations?latitude=21.028511&longitude=105.854167&maxDistanceKm=10&pageNumber=1&pageSize=20
```

### Ví Dụ 3: Đề xuất có vị trí + lọc môn thể thao (Cầu Lông)
```http
GET /api/v1/venues/recommendations?latitude=21.028511&longitude=105.854167&sportTypeId=a90bcf2e-9d22-4a0b-967a-e45f9c464e81&pageNumber=1&pageSize=20
```

### Ví Dụ 4: Đề xuất tìm sân còn trống vào khung giờ cụ thể (18:00 - 20:00 ngày 15/10/2026)
```http
GET /api/v1/venues/recommendations?latitude=21.028511&longitude=105.854167&date=2026-10-15&startTime=18:00:00&endTime=20:00:00&pageNumber=1&pageSize=20
```

### Ví Dụ 5: User đã đăng nhập (Gửi kèm Token để biết trạng thái yêu thích `isFavourite`)
```http
GET /api/v1/venues/recommendations?latitude=21.028511&longitude=105.854167&pageNumber=1&pageSize=20
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## 8. Hướng Dẫn Tích Hợp Cho Mobile (Flutter / Dart)

### 1. Model Dart
```dart
class VenueRecommendationDto {
  final String idVenue;
  final String nameVenue;
  final String address;
  final double latitude;
  final double longitude;
  final String? primaryImageUrl;
  final double? distanceKm;
  final double averageRating;
  final int reviewCount;
  final int favouriteCount;
  final double? minPricePerHour;
  final List<SportTypeDto> sportTypes;
  final double recommendationScore;
  final bool isFavourite;

  VenueRecommendationDto.fromJson(Map<String, dynamic> json)
      : idVenue = json['idVenue'],
        nameVenue = json['nameVenue'],
        address = json['address'],
        latitude = (json['latitude'] as num).toDouble(),
        longitude = (json['longitude'] as num).toDouble(),
        primaryImageUrl = json['primaryImageUrl'],
        distanceKm = json['distanceKm'] != null ? (json['distanceKm'] as num).toDouble() : null,
        averageRating = (json['averageRating'] as num).toDouble(),
        reviewCount = json['reviewCount'] ?? 0,
        favouriteCount = json['favouriteCount'] ?? 0,
        minPricePerHour = json['minPricePerHour'] != null ? (json['minPricePerHour'] as num).toDouble() : null,
        sportTypes = (json['sportTypes'] as List? ?? [])
            .map((e) => SportTypeDto.fromJson(e))
            .toList(),
        recommendationScore = (json['recommendationScore'] as num).toDouble(),
        isFavourite = json['isFavourite'] ?? false;
}
```

### 2. Gọi API với Dio (Infinite Scroll)
```dart
Future<PagedResult<VenueRecommendationDto>> getRecommendedVenues({
  double? latitude,
  double? longitude,
  double maxDistanceKm = 20,
  String? sportTypeId,
  DateTime? date,
  String? startTime,
  String? endTime,
  int pageNumber = 1,
  int pageSize = 20,
}) async {
  final Map<String, dynamic> queryParams = {
    'pageNumber': pageNumber,
    'pageSize': pageSize,
  };

  if (latitude != null && longitude != null) {
    queryParams['latitude'] = latitude;
    queryParams['longitude'] = longitude;
    queryParams['maxDistanceKm'] = maxDistanceKm;
  }

  if (sportTypeId != null) {
    queryParams['sportTypeId'] = sportTypeId;
  }

  if (date != null && startTime != null && endTime != null) {
    queryParams['date'] = date.toIso8601String().split('T').first;
    queryParams['startTime'] = startTime; // "18:00:00"
    queryParams['endTime'] = endTime;     // "20:00:00"
  }

  final response = await dio.get(
    '/api/v1/venues/recommendations',
    queryParameters: queryParams,
  );

  return PagedResult.fromJson(
    response.data,
    (item) => VenueRecommendationDto.fromJson(item),
  );
}
```

---

## 9. Mã Lỗi Thường Gặp (Error Responses)

| HTTP Code | Nguyên nhân | Ví dụ Response |
| :---: | :--- | :--- |
| **`400 Bad Request`** | Toạ độ truyền không hợp lệ (Ví dụ: truyền `latitude` nhưng thiếu `longitude`, hoặc toạ độ ngoài phạm vi [-90, 90]). | `{"type":"ValidationError","errors":["Longitude is required when Latitude is provided."]}` |
| **`400 Bad Request`** | Truyền giờ nhưng thiếu ngày (`date`). | `{"type":"ValidationError","errors":["Date is required when filtering by time slot."]}` |
| **`400 Bad Request`** | `endTime` nhỏ hơn hoặc bằng `startTime`. | `{"type":"ValidationError","errors":["EndTime must be greater than StartTime."]}` |
| **`200 OK`** | Thành công. Trả về mảng `items` kèm thông tin phân trang `hasNextPage`. | *(Xem mục 6)* |
