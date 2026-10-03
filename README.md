# Order Management API

Prototype REST API untuk sistem Order Management menggunakan **ASP.NET Core, SQL Server, Stored Procedure, dan Microservice Architecture**.

Fokus utama sistem adalah mencegah **double order, stock minus akibat concurrent request, concurrent status update, dan race condition**.

---

## 1. Idempotency

### Alasan

Create Order menggunakan header `Idempotency-Key` sebagai identifier unik untuk setiap operasi create order.

Contoh : 
Idempotency-Key: 7d9f2f5c-exam

### Alasan memilih `Idempotency-Key`

- Merupakan pendekatan yang umum digunakan pada REST API untuk menangani request yang dapat di-retry.
- Key dapat dibuat oleh client tanpa mengubah payload request.
- Request yang sama dapat diidentifikasi ketika client melakukan double-click, timeout, atau network retry.
- Lebih jelas dibanding hanya menggunakan hash payload, karena dua request dengan payload yang sama belum tentu merupakan order yang sama.
- Memudahkan server menyimpan dan mengecek status proses berdasarkan key.

### Tujuan

- Mencegah duplicate order akibat double-click.
- Aman terhadap client retry.
- Mencegah request dengan `Idempotency-Key` yang sama membuat order kedua.
- Menjamin satu `Idempotency-Key` hanya menghasilkan satu order.

Database memiliki constraint : UNIQUE (IdempotencyKey)

Sehingga key yang sama tidak dapat digunakan untuk membuat record idempotency yang berbeda.

Jika request dengan key yang sama sudah berstatus `Completed`, request berikutnya tidak membuat order baru, tetapi mengembalikan order yang sudah dibuat sebelumnya.

---

## 2. Concurrency Handling

Sistem menggunakan kombinasi **atomic SQL operation, database transaction, optimistic concurrency, dan database constraint**.

### 2.1 Concurrent Stock Deduction

Skenario:

Stock Product X = 15

Request A -> Buy 10
Request B -> Buy 10


Kedua request datang hampir bersamaan.

Expected result:
Request A -> Success
Request B -> Insufficient Stock

Final Stock = 5


Request yang berhasil dapat berbeda tergantung request mana yang terlebih dahulu berhasil melakukan atomic update.

Stock tidak boleh menjadi `-5`.

Strategy:

- Atomic SQL Update
- Database Transaction
- Stock validation

Contoh atomic update:

UPDATE Products
SET StockQuantity = StockQuantity - @Quantity
WHERE Id = @ProductId
  AND StockQuantity >= @Quantity;

Dengan pendekatan ini, pengecekan stock dan pengurangan stock dilakukan dalam satu operasi database sehingga race condition pada stock dapat dicegah.

---

### 2.2 Concurrent Status Update

Problem:

Dua admin membaca order dengan `ConcurrencyVersion` yang sama kemudian melakukan update secara bersamaan.

Contoh:

Order Status = Pending
Version = V1

Admin A -> Pending -> Confirmed
Admin B -> Pending -> Cancelled


Kedua request menggunakan version `V1`.
Request pertama yang berhasil akan mengubah data dan menghasilkan `ROWVERSION` baru.
Request kedua masih menggunakan version lama sehingga update ditolak.

Response : 409 Conflict CONCURRENCY_CONFLICT


Client harus mengambil data terbaru sebelum melakukan update kembali.

Strategy yang digunakan adalah **optimistic concurrency menggunakan SQL Server `ROWVERSION`**.

---

### 2.3 Idempotent Create Under Race

**Problem:**

Dua request Create Order masuk secara bersamaan dengan : Idempotency-Key: ABC123

Sistem harus memastikan hanya satu order yang dibuat untuk key tersebut.

Expected result:

Request A -> Create Order
Request B -> Duplicate / InProgress


Setelah request pertama berhasil dan status idempotency menjadi `Completed`, request berikutnya dengan key yang sama akan mengembalikan order yang sudah dibuat.

Tidak boleh terdapat:

Order A
Order B


untuk `Idempotency-Key` yang sama.

Strategy:

- Unique constraint pada `IdempotencyKey`
- Database locking (`UPDLOCK`, `HOLDLOCK`)
- Idempotency status (`InProgress`, `Completed`, `Failed`)

Dengan pendekatan tersebut, database menjadi sumber utama untuk memastikan satu idempotency key tidak menghasilkan lebih dari satu order.

---

## 3. Race Condition Prevention

Selain concurrency utama di atas, terdapat beberapa race condition lain yang ditangani oleh sistem.

### 3.1 Concurrent Cancel Order

**Problem:**

Dua admin mencoba cancel order yang sama secara bersamaan.

Admin A -> Cancel
Admin B -> Cancel

Jika tidak ditangani, proses release stock berpotensi dilakukan lebih dari satu kali.

Pencegahan:

- `ROWVERSION` / `ConcurrencyVersion`
- Atomic status update
- Database transaction
- Concurrency check

Hanya request dengan `ConcurrencyVersion` yang masih valid yang dapat melakukan cancel. Request lainnya mendapatkan `409 Conflict`.

Dengan demikian proses release stock hanya dilakukan oleh operasi cancel yang berhasil.

---

### 3.2 Cancel vs Update Status

Contoh:

Admin A -> Pending -> Cancelled
Admin B -> Pending -> Confirmed

Kedua request menggunakan `ConcurrencyVersion` yang sama.

Optimistic concurrency memastikan hanya satu perubahan yang berhasil.

Request lainnya mendapatkan : 409 Conflict CONCURRENCY_CONFLICT


Validasi status transition juga memastikan perubahan status hanya dapat dilakukan sesuai aturan yang ditentukan.

---

### 3.3 Partial Failure

Karena sistem menggunakan Order Management API dan Inventory Service, proses Create Order melibatkan lebih dari satu service.

Contoh:

Order Management API
        |
        v
Inventory Service
        |
        v
Reserve Stock
        |
        X
Create Order gagal

Jika stock sudah berhasil di-reserve tetapi proses Create Order gagal, stock harus dikembalikan.

**Pencegahan:**

- Order Management API mencatat stock yang berhasil di-reserve.
- Jika proses Create Order gagal, API memanggil Inventory Service untuk melakukan release stock.
- Idempotency request ditandai `Failed`.
- Proses database pada masing-masing service menggunakan transaction sesuai boundary service.

Dengan demikian kegagalan pada proses order tidak meninggalkan stock yang sudah terpotong tanpa order.

---

## 4. Validation

Validation dilakukan sebelum Stored Procedure dijalankan untuk memastikan request valid.

Contoh validation:

- `Idempotency-Key`: Required, maximum 100 characters
- `CustomerId`: Must be greater than 0
- `OrderId`: Must be greater than 0
- `ProductId`: Must be greater than 0
- `Quantity`: Must be greater than 0
- `ShippingAddress`: Required
- Pagination: `Page >= 1`
- `PageSize`: memiliki batas maksimum
- Date Range: `FromDate <= ToDate`
- Order Status harus salah satu dari:
  - `Pending`
  - `Confirmed`
  - `Shipped`
  - `Delivered`
  - `Cancelled`

---

## 5. Error Handling

Semua error menggunakan format response yang konsisten.

Contoh:

{
  "status": 409,
  "code": "INSUFFICIENT_STOCK",
  "message": "Stock product ID 1 tidak mencukupi.",
  "traceId": "abc123"
}


HTTP status yang digunakan:

| Status | Description |
|---|---|
| 400 | Bad Request |
| 404 | Not Found |
| 409 | Conflict |
| 422 | Unprocessable Entity |
| 500 | Internal Server Error |

Centralized error handling dilakukan menggunakan `ErrorHandlingMiddleware`.

Middleware menangani custom exception seperti:

- `BadRequestException`
- `NotFoundException`
- `ConflictException`
- `UnprocessableEntityException`

Unexpected exception dikembalikan sebagai `500 Internal Server Error` dengan message umum sehingga detail internal tidak diberikan kepada client.

---

## 6. Logging

Application menggunakan **log4net**.

Setiap request memiliki `X-Correlation-ID`.

Jika client mengirim correlation ID, API akan menggunakan ID tersebut. Jika tidak ada, server membuat correlation ID baru.

Correlation ID digunakan untuk menghubungkan request dengan log yang terkait.

Contoh log event:

- Request started
- CreateOrder completed
- Order retrieved
- Orders listed
- Order status updated
- Stock restored
- Order cancelled
- Request completed

Log disimpan pada : Logs/

Contoh:

10:15:20.123 | INFO | 8a21bc3f | Request started | Method=POST | Path=/api/Order
10:15:20.245 | INFO | 8a21bc3f | CreateOrder completed | OrderId=1001
10:15:20.250 | INFO | 8a21bc3f | Request completed | StatusCode=201


---

## 7. Database

Database menggunakan **SQL Server**.

### Alasan menggunakan SQL Server

- Mendukung database transaction.
- Mendukung `ROWVERSION` untuk optimistic concurrency.
- Mendukung Stored Procedure.
- Mendukung atomic update.
- Mendukung unique constraint.
- Cocok untuk kebutuhan concurrency handling.

### Database Architecture

Order Management API
        |
        v
OrderManagementDb

Inventory Service
        |
        v
InventoryDb


`OrderManagementDb` digunakan untuk data order, sedangkan `InventoryDb` digunakan untuk data product dan stock.

---

## 8. Testing

Testing menggunakan **xUnit** untuk menguji functional requirement dan concurrency pada Order Management API.

Test yang dibuat meliputi:

- Create Order berhasil dan stock berkurang.
- Create Order ditolak ketika stock tidak mencukupi.
- Get Order.
- Update Status Order.
- Cancel Order.
- Invalid Status Transition.
- Idempotency menggunakan `Idempotency-Key`.
- Concurrency Stock Deduction.

### 8.1 Concurrency Test

Test concurrency dilakukan pada skenario Concurrent Stock Deduction

Skenario:

Initial Stock = 15

Request A -> Reserve 10
Request B -> Reserve 10


Kedua request dijalankan secara bersamaan.

Expected result:

Request A -> Success
Request B -> Conflict
Final Stock = 5


atau sebaliknya, tergantung request mana yang berhasil terlebih dahulu.

Test memastikan bahwa:

- Hanya satu request yang berhasil melakukan reserve stock.
- Request lainnya mendapatkan error `409 Conflict`.
- Stock tidak pernah menjadi negatif.
- Total stock yang berhasil dikurangi tidak melebihi stock yang tersedia.