# AsyncDocumentProcessing

Asenkron belge işleme, gerçek OCR ve kontrollü eşzamanlılık üzerine geliştirilmiş .NET 8 backend uygulaması.

Proje; yüklenen belgelerin HTTP isteği sırasında doğrudan işlenmesi yerine, bir **Channel tabanlı kuyruk** üzerinden arka planda işlenmesini sağlar.

Desteklenen belge formatları:

* PDF
* JPG
* JPEG
* PNG

PDF dosyalarında sayfalar görüntüye dönüştürülerek, görüntü dosyalarında ise doğrudan **Tesseract OCR** kullanılarak metin çıkarılır.

---

## 1. Projenin Amacı

Sistemin temel amacı:

* Belgeyi API üzerinden almak
* Dosyayı kalıcı storage'a kaydetmek
* Belge kaydını SQL Server'a oluşturmak
* Belgeyi asenkron kuyruğa almak
* Background Worker tarafından işlemek
* Aynı anda kontrollü sayıda belge işlemek
* Gerçek OCR gerçekleştirmek
* OCR sonucunu veritabanına kaydetmek
* Hatalarda retry uygulamak
* Uygulama kapanırken çalışan işlemleri kontrollü şekilde sonlandırmak

Bu yapı sayesinde HTTP request'i uzun süren OCR işlemi boyunca bloklanmaz.

---

## 2. Mimari

Temel işlem akışı:

```text
                    ┌─────────────────┐
                    │   Client / UI   │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │   Upload API    │
                    └────────┬────────┘
                             │
                 ┌───────────┴───────────┐
                 │                       │
                 ▼                       ▼
          ┌─────────────┐        ┌──────────────┐
          │ File Storage│        │  SQL Server  │
          └─────────────┘        └──────────────┘
                                         
                             │
                             ▼
                    ┌─────────────────┐
                    │ Channel Queue   │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Background      │
                    │ Worker          │
                    └────────┬────────┘
                             │
                     MaxConcurrency
                             │
                             ▼
                    ┌─────────────────┐
                    │DocumentProcessor│
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Tesseract OCR   │
                    │ PDF/JPG/JPEG/PNG│
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │   SQL Server    │
                    │ OCR Result      │
                    └─────────────────┘
```

---

## 3. Teknolojiler

* .NET 8
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server
* Tesseract OCR 5.2.0
* Docnet.Core 2.6.0
* Tesseract.Drawing 5.2.0
* System.Drawing.Common
* FluentValidation
* Serilog
* xUnit
* Microsoft.AspNetCore.Mvc.Testing
* Swagger / OpenAPI

---

## 4. Solution Yapısı

```text
AsyncDocumentProcessing
│
├── AsyncDocumentProcessing
│
├── AsyncDocumentProcessing.Api
│   ├── Controllers
│   ├── Middleware
│   ├── Workers
│   ├── Logs
│   └── Storage
│
├── AsyncDocumentProcessing.Application
│   ├── DTOs
│   ├── Interfaces
│   ├── Options
│   ├── Services
│   └── Validators
│
├── AsyncDocumentProcessing.Domain
│   ├── Entities
│   └── Enums
│
├── AsyncDocumentProcessing.Infrastructure
│   ├── DependencyInjection
│   ├── Migrations
│   ├── OCR
│   ├── Persistence
│   │   ├── Configurations
│   │   └── Repositories
│   ├── Processing
│   ├── Queue
│   ├── Storage
│   └── tessdata
│
├── AsyncDocumentProcessing.Tests
│
└── AsyncDocumentProcessing.Worker
```

---

## 5. Asenkron İşleme

Upload işlemi sırasında belge doğrudan OCR edilmez.

API:

1. Dosyayı storage'a kaydeder.
2. Document kaydını oluşturur.
3. Document ID'yi Channel Queue'ya ekler.
4. Client'a `TrackingId` döner.

Background Worker:

1. Queue'dan Document ID alır.
2. DocumentProcessor oluşturur.
3. Dosyayı storage'dan açar.
4. SHA-256 hash hesaplar.
5. OCR gerçekleştirir.
6. PageCount ve WordCount hesaplar.
7. OCR sonucunu veritabanına kaydeder.

Bu yaklaşım sayesinde upload endpoint'i OCR süresinden bağımsız çalışır.

---

## 6. Channel Queue

Queue için .NET'in `System.Threading.Channels` altyapısı kullanılmıştır.

Queue:

* Thread-safe çalışır.
* Producer/Consumer modelini destekler.
* Asenkron çalışır.
* Belirlenen kapasite ile sınırlandırılabilir.

Belge upload edildiğinde Document ID queue'ya eklenir.

Worker ise queue'dan Document ID'leri tüketir.

---

## 7. Kontrollü Eşzamanlılık

Background Worker içerisinde `SemaphoreSlim` kullanılarak maksimum eşzamanlı belge işleme sayısı kontrol edilir.

Örneğin:

```json
{
  "DocumentProcessing": {
    "MaxConcurrency": 3
  }
}
```

Bu durumda aynı anda en fazla 3 belge işlenebilir.

Amaç:

* CPU kullanımını kontrol etmek
* OCR kaynak tüketimini sınırlamak
* Aynı anda çok fazla işlem nedeniyle sistemin kilitlenmesini önlemek

---

## 8. Retry Mekanizması

Belge işleme sırasında hata oluşursa retry uygulanır.

Belgenin:

* `RetryCount`
* `LastErrorMessage`
* `ErrorMessage`
* `Status`

alanları güncellenir.

Retry limiti aşıldığında belge:

```text
Failed
```

durumuna geçirilir.

Örnek:

```text
Processing
   │
   ├── Hata
   │
   ▼
Retry
   │
   ├── Başarılı → Completed
   │
   └── Limit aşıldı → Failed
```

---

## 9. Gerçek OCR

OCR simülasyonu yerine gerçek **Tesseract OCR** kullanılmaktadır.

### PDF

Tesseract doğrudan PDF işlemek yerine PDF sayfaları Docnet ile görüntüye dönüştürülür.

Her sayfa OCR işleminden geçirilir.

```text
PDF
 │
 ▼
Docnet
 │
 ▼
Page Image
 │
 ▼
Tesseract
 │
 ▼
Extracted Text
```

PDF render çözünürlüğü OCR doğruluğunu artırmak amacıyla:

```csharp
new PageDimensions(1654, 2339)
```

olarak kullanılmaktadır.

### Görüntü dosyaları

JPG, JPEG ve PNG dosyaları doğrudan görüntü olarak açılır ve Tesseract'a gönderilir.

Desteklenen formatlar:

```text
.pdf
.jpg
.jpeg
.png
```

Tesseract dilleri:

```text
tur + eng
```

Türkçe ve İngilizce OCR birlikte kullanılmaktadır.

`tessdata` klasöründe gerekli language data dosyaları bulunur.

---

## 10. Belge Durumları

Belgenin yaşam döngüsü:

```text
Pending
   │
   ▼
Processing
   │
   ├───────────────┐
   │               │
   ▼               ▼
Completed        Failed
```

Başarılı işleme sonucunda:

* `Status = Completed`
* `PageCount`
* `WordCount`
* `ExtractedText`
* `Sha256Hash`
* `CompletedAt`

alanları doldurulur.

---

## 11. SHA-256

İşlenen dosyanın SHA-256 hash değeri hesaplanır.

Örnek:

```text
sha256Hash:
566c6efb4cb7114d75fbe948cbaf59c021ee1af71595171156c259a11959845b
```

Bu değer belge bütünlüğünün takip edilmesi ve aynı dosyanın tespit edilmesi gibi senaryolarda kullanılabilir.

---

## 12. Graceful Shutdown

Background Worker `CancellationToken` ile çalışmaktadır.

Uygulama kapanırken:

```text
Application is shutting down...
Document Worker stopped.
```

şeklinde kontrollü kapanış gerçekleştirilir.

Çalışan işlemler cancellation token üzerinden durdurulabilir.

Bu yaklaşım uygulamanın aniden kapanması yerine mevcut worker yaşam döngüsünün kontrollü şekilde sonlandırılmasını sağlar.

---

## 13. API

### Belge yükleme

```http
POST /api/documents
```

Örnek request:

```text
DocumentType: test
BatchId: test ocr
SourceSystem: swagger
File: example.png
```

Response:

```json
{
  "trackingId": "7bee859a-5353-4aa0-93f4-ded8e6ea5ed7"
}
```

---

### Belge durumunu sorgulama

```http
GET /api/documents/{id}
```

Örnek response:

```json
{
  "id": "7bee859a-5353-4aa0-93f4-ded8e6ea5ed7",
  "fileName": "example.png",
  "documentType": "test",
  "batchId": "test ocr",
  "sourceSystem": "swagger",
  "status": 3,
  "pageCount": 1,
  "wordCount": 10,
  "sha256Hash": "566c6efb4cb7114d75fbe948cbaf59c021ee1af71595171156c259a11959845b",
  "extractedText": "Merhaba Dünya\nBu belge gerçek OCR testi için hazırlanmıştır.",
  "errorMessage": null
}
```

---

### Batch sorgulama

```http
GET /api/documents/batch/{batchId}
```

Pagination desteklenmektedir.

Örnek:

```http
GET /api/documents/batch/test-001?page=1&pageSize=10
```

Response içerisinde:

* `items`
* `page`
* `pageSize`
* `totalCount`

alanları bulunur.

---

## 14. Validation

FluentValidation kullanılmıştır.

Upload request için:

* `DocumentType` zorunludur.
* `BatchId` zorunludur.
* `SourceSystem` zorunludur.
* Alanların maksimum uzunlukları kontrol edilir.

OCR katmanında ayrıca desteklenmeyen dosya formatları reddedilir.

---

## 15. Global Exception Handling

API içerisinde global exception middleware bulunmaktadır.

Beklenmeyen hatalar merkezi olarak ele alınır ve API tarafında standart ProblemDetails formatı kullanılır.

Amaç:

* Controller içerisinde tekrar eden exception handling kodunu azaltmak
* Standart HTTP hata response'ları üretmek
* API tüketicisine tutarlı hata bilgisi sağlamak

---

## 16. Logging

Serilog ile uygulama loglaması yapılmaktadır.

Önemli lifecycle noktaları loglanır:

```text
Document Worker started
Document processing started
Document file processing started
Document OCR started
Document OCR completed
Document retrying
Document processing failed
Document Worker stopped
```

Ayrıca aktif belge işleme sayısı da loglanmaktadır.

Örneğin:

```text
Active document processing count: 1
Active document processing count: 2
Active document processing count: 3
```

---

## 17. Database

Entity Framework Core ve SQL Server kullanılmaktadır.

Ana belge bilgileri `Documents` tablosunda tutulmaktadır.

Önemli alanlar:

```text
Id
FileName
FilePath
DocumentType
BatchId
SourceSystem
Status
PageCount
WordCount
Sha256Hash
ExtractedText
ErrorMessage
LastErrorMessage
RetryCount
CreatedAt
ProcessingStartedAt
CompletedAt
```

Database schema migration ile oluşturulabilir.

---

## 18. Kurulum

### Gereksinimler

* .NET 8 SDK
* SQL Server
* Visual Studio 2022 veya .NET CLI

### Connection String

`appsettings.json` içerisinde SQL Server bağlantısı tanımlanmalıdır.

Örnek:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AsyncDocumentProcessingDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### Migration

Solution klasöründe:

```powershell
dotnet ef database update
```

Gerekli durumda migration oluşturmak için:

```powershell
dotnet ef migrations add InitialCreate
```

---

## 19. Uygulamayı Çalıştırma

Solution klasöründe:

```powershell
dotnet build ".\AsyncDocumentProcessing.sln"
```

Testleri çalıştırmak için:

```powershell
dotnet test ".\AsyncDocumentProcessing.sln"
```

API'yi çalıştırdıktan sonra Swagger üzerinden endpointler test edilebilir.

---

## 20. Testler

Unit testler xUnit kullanılarak hazırlanmıştır.

Mevcut test senaryoları arasında:

* Başarılı document processing
* Retry limitinin aşılması
* Document'ın Failed durumuna geçmesi
* Worker davranışı
* API integration testleri

bulunmaktadır.

Son doğrulama:

```text
Test summary:
total: 7
failed: 0
succeeded: 7
skipped: 0
```

Ayrıca gerçek OCR manuel olarak doğrulanmıştır.

Test edilen örnekler:

```text
PDF → gerçek OCR → başarılı
PNG → gerçek OCR → başarılı
```

---

## 21. Teknik Kararlar

### Neden Channel?

Belge işleme işlemlerini HTTP request lifecycle'ından ayırmak ve producer/consumer modeli oluşturmak için.

### Neden Background Worker?

OCR gibi CPU/resource intensive işlemlerin API request'ini uzun süre meşgul etmesini önlemek için.

### Neden SemaphoreSlim?

Eşzamanlı OCR işlemlerinin kontrol edilmesi ve sistem kaynaklarının korunması için.

### Neden Tesseract?

Gerçek OCR ihtiyacını karşılayan, açık kaynaklı ve Türkçe language data desteğine sahip bir OCR motoru olduğu için.

### Neden Docnet?

PDF sayfalarını Tesseract'ın işleyebileceği görüntülere dönüştürmek için.

### Neden SHA-256?

Dosya bütünlüğü ve belge kimliğinin takip edilebilmesi için.

---

## 22. Projenin Genel Akışı

```text
                 Upload
                   │
                   ▼
              File Storage
                   │
                   ▼
              SQL Document
                   │
                   ▼
              Channel Queue
                   │
                   ▼
          Background Worker
                   │
          SemaphoreSlim (3)
                   │
                   ▼
           DocumentProcessor
                   │
                   ├── SHA-256
                   │
                   ▼
              Tesseract OCR
                   │
          ┌────────┼────────┐
          │        │        │
         PDF      JPG    JPEG/PNG
          │        │        │
          ▼        └───┬────┘
        Docnet          │
          │             │
          └──────┬──────┘
                 ▼
           Extracted Text
                 │
                 ▼
              SQL Server
                 │
                 ▼
             Completed
```

---

## 23. Sonuç

AsyncDocumentProcessing; belge yükleme, kuyruklama, kontrollü eşzamanlı işleme, retry, gerçek OCR, veritabanı kalıcılığı, logging ve graceful shutdown özelliklerini bir araya getiren asenkron bir backend uygulamasıdır.

Projenin temel yaklaşımı:

> **Upload işlemini hızlı tut, ağır belge işleme operasyonlarını kontrollü ve asenkron şekilde arka planda gerçekleştir.**
