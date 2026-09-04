# AsyncDocumentProcessing

.NET 8 ile geliştirilmiş, **asenkron belge işleme ve gerçek OCR** odaklı backend uygulaması.

Sistem; belgeleri HTTP request sırasında doğrudan OCR ile işlemek yerine dosyayı storage'a kaydeder, SQL Server üzerinde `Pending` durumunda bir kayıt oluşturur ve ayrı bir **Background Worker** tarafından arka planda işler.

Desteklenen belge formatları:

* PDF
* JPG
* JPEG
* PNG

PDF belgeleri Docnet ile görüntüye dönüştürülerek Tesseract OCR ile işlenir. JPG/JPEG/PNG dosyaları ise doğrudan Tesseract OCR ile işlenir.

---

## 1. Projenin Amacı

Projenin temel amacı, maliyetli belge işleme operasyonlarını HTTP request lifecycle'ından ayırarak **güvenilir, kontrollü ve izlenebilir bir arka plan işleme mimarisi** oluşturmaktır.

Başlıca özellikler:

* Belge upload API'si
* Local file storage
* SQL Server üzerinde kalıcı iş durumu
* Ayrı Background Worker
* Atomic document claiming
* Kontrollü eşzamanlılık
* Gerçek Tesseract OCR
* PDF rendering
* SHA-256 hesaplama
* Retry mekanizması
* Stale `Processing` kayıtlarının recovery işlemi
* FluentValidation
* Global exception handling
* ProblemDetails
* Serilog
* CancellationToken / graceful shutdown
* Batch sorgulama
* Pagination
* Unit testleri
* Integration testleri
* Swagger / OpenAPI

---

# 2. Mimari

Uygulama iki temel runtime bileşeninden oluşur:

* **API** — belge upload ve sorgulama işlemleri
* **Worker** — SQL Server'daki `Pending` belgeleri arka planda işler

Genel mimari:

```text
                    ┌─────────────────┐
                    │     Client      │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │   ASP.NET API   │
                    └────────┬────────┘
                             │
                  ┌──────────┴──────────┐
                  │                     │
                  ▼                     ▼
          ┌───────────────┐     ┌─────────────────┐
          │ File Storage  │     │   SQL Server    │
          │               │     │                 │
          │ PDF/JPG/PNG   │     │ Document        │
          └───────────────┘     │ Status=Pending  │
                                └────────┬────────┘
                                         │
                                         │ Polling
                                         ▼
                                ┌─────────────────┐
                                │ Background      │
                                │ Worker          │
                                └────────┬────────┘
                                         │
                                         │ Atomic Claim
                                         ▼
                                ┌─────────────────┐
                                │    Processing   │
                                └────────┬────────┘
                                         │
                                         ▼
                                ┌─────────────────┐
                                │DocumentProcessor│
                                └────────┬────────┘
                                         │
                              ┌──────────┴──────────┐
                              │                     │
                              ▼                     ▼
                         SHA-256              Tesseract OCR
                                                    │
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

# 3. Neden SQL-backed Queue Yaklaşımı?

Belge işleme durumu SQL Server üzerinde kalıcı olarak tutulmaktadır.

Upload sonrasında belge:

```text
Pending
```

durumunda veritabanına kaydedilir.

Worker daha sonra `Pending` kayıtlarını sorgulayarak işleme alır.

Bu yaklaşımın önemli avantajı:

* Worker yeniden başlatıldığında `Pending` işler kaybolmaz.
* İş durumu kalıcı olarak veritabanında tutulur.
* Birden fazla worker instance çalıştırıldığında atomic claim ile aynı belgenin iki kez alınması engellenebilir.
* İşlerin durumu API üzerinden sonradan sorgulanabilir.

Bu nedenle uygulama içi geçici bir queue yerine **SQL Server üzerindeki document state** iş kuyruğunun kalıcı kaynağı olarak kullanılmaktadır.

---

# 4. Solution Yapısı

```text
AsyncDocumentProcessing
│
├── AsyncDocumentProcessing.Api
│   ├── Controllers
│   ├── Middleware
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
│   ├── OCR
│   ├── Persistence
│   │   ├── Configurations
│   │   └── Repositories
│   ├── Processing
│   └── Storage
│
├── AsyncDocumentProcessing.Worker
│   ├── Program.cs
│   └── Worker.cs
│
├── AsyncDocumentProcessing.Tests
│
└── AsyncDocumentProcessing.sln
```

---

# 5. Teknoloji Stack

* .NET 8
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server
* Tesseract OCR
* Docnet.Core
* System.Drawing.Common
* FluentValidation
* Serilog
* xUnit
* Microsoft.AspNetCore.Mvc.Testing
* Swagger / OpenAPI

---

# 6. Belge İşleme Akışı

Upload sırasında OCR gerçekleştirilmez.

API'nin yaptığı işlemler:

1. Request validation
2. Dosya kontrolü
3. Dosya boyutu kontrolü
4. Dosya uzantısı kontrolü
5. Dosyanın storage'a kaydedilmesi
6. SQL Server üzerinde `Pending` document oluşturulması
7. Client'a `TrackingId` döndürülmesi

Worker'ın yaptığı işlemler:

1. `Pending` belgeleri sorgular.
2. Belgeyi atomic olarak `Processing` durumuna geçirir.
3. Dosyayı storage'dan açar.
4. SHA-256 hesaplar.
5. OCR gerçekleştirir.
6. PageCount hesaplar.
7. WordCount hesaplar.
8. OCR sonucunu kaydeder.
9. Belgeyi `Completed` durumuna geçirir.

İşlem sırasında hata oluşursa retry uygulanır.

---

# 7. Background Worker

Worker, .NET `BackgroundService` kullanılarak ayrı bir process olarak çalışır.

Worker her belge işlemi için ayrı bir dependency injection scope oluşturur.

Bu sayede scoped servislerin, özellikle `DbContext` ve repository'lerin yaşam döngüsü doğru şekilde yönetilir.

Temel akış:

```text
SQL Server
    │
    ▼
Pending Documents
    │
    ▼
Worker Polling
    │
    ▼
TryClaimAsync()
    │
    ├── false → Belge artık Pending değil
    │
    └── true
          │
          ▼
      Processing
          │
          ▼
    DocumentProcessor
```

---

# 8. Atomic Claim

Aynı belgenin iki farklı worker tarafından aynı anda işlenmesini önlemek için atomic claim mekanizması kullanılmaktadır.

Claim işlemi yalnızca:

```text
Id == documentId
AND
Status == Pending
```

koşulunu sağlayan kaydı:

```text
Processing
```

durumuna geçirir.

Repository `ExecuteUpdateAsync` kullanarak işlemi doğrudan SQL tarafında gerçekleştirir.

Etkilenen satır sayısı `1` değilse belge başka bir işlem tarafından alınmış kabul edilir.

Bu yaklaşım concurrent processing sırasında duplicate processing riskini azaltır.

---

# 9. Kontrollü Eşzamanlılık

Worker içerisinde `Parallel.ForEachAsync` kullanılarak maksimum eşzamanlı işlem sayısı configuration üzerinden kontrol edilir.

Örneğin:

```json
{
  "DocumentProcessing": {
    "MaxConcurrency": 1
  }
}
```

`MaxConcurrency` Worker'ın aynı anda kaç belgeyi işlemeye çalışacağını belirler.

## Neden mevcut değer `1`?

PDF OCR işlemi sırasında kullanılan Docnet/FPDF native bileşenleri ile concurrent processing testlerinde native memory access violation gözlemlenmiştir.

Örneğin:

```text
System.AccessViolationException:
Attempted to read or write protected memory.
```

Bu nedenle mevcut ortamda **stabilite önceliğiyle `MaxConcurrency = 1`** kullanılmaktadır.

Mimari olarak concurrency artırılabilir şekilde tasarlanmıştır; ancak mevcut PDF rendering/OCR kombinasyonunda daha yüksek concurrency için native renderer izolasyonu veya farklı bir PDF rendering yaklaşımı değerlendirilmelidir.

---

# 10. Retry Mekanizması

Belge işleme sırasında exception oluşursa retry uygulanır.

Document üzerinde aşağıdaki bilgiler tutulur:

* `RetryCount`
* `LastErrorMessage`
* `ErrorMessage`
* `Status`

Örneğin:

```text
Processing
    │
    ▼
   Error
    │
    ├── Retry 1
    ├── Retry 2
    ├── Retry 3
    │
    └── Limit exceeded
             │
             ▼
           Failed
```

Mevcut configuration:

```json
{
  "DocumentProcessing": {
    "MaxRetryCount": 3
  }
}
```

Bu değer:

```text
1 initial attempt
+
3 retries
=
4 total attempts
```

anlamına gelir.

Retry limiti aşıldığında belge `Failed` durumuna geçirilir.

---

# 11. Stale Processing Recovery

Worker beklenmedik şekilde kapanırsa belge:

```text
Processing
```

durumunda kalabilir.

Bu durumun sistemde sonsuza kadar takılı kalmasını önlemek için stale processing recovery mekanizması bulunmaktadır.

Mevcut değerler:

```text
StaleProcessingTimeout = 10 dakika
StaleRecoveryInterval  = 1 dakika
```

Akış:

```text
Processing
    │
    │ 10 dakikadan uzun süredir işleniyor
    ▼
Pending
    │
    ▼
Worker tekrar claim eder
```

Bu mekanizma yarım kalmış işlemlerin yeniden işlenebilmesini sağlar.

---

# 12. Gerçek OCR

Projede OCR simülasyonu yerine gerçek **Tesseract OCR** kullanılmaktadır.

Tesseract language configuration:

```text
tur + eng
```

Türkçe ve İngilizce OCR desteği birlikte kullanılmaktadır.

## PDF OCR

PDF işlemi:

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

PDF sayfaları:

```csharp
new PageDimensions(1654, 2339)
```

boyutlarında render edilir.

Her sayfa OCR işleminden geçirilir.

## Image OCR

JPG, JPEG ve PNG dosyaları doğrudan Tesseract'a gönderilir:

```text
Image
  │
  ▼
Tesseract
  │
  ▼
Extracted Text
```

---

# 13. Document Status

Belge yaşam döngüsü:

```text
             ┌──────────────┐
             │    Pending   │
             └──────┬───────┘
                    │
              Atomic Claim
                    │
                    ▼
             ┌──────────────┐
             │  Processing  │
             └──────┬───────┘
                    │
             ┌──────┴──────┐
             │             │
          Success         Error
             │             │
             ▼             ▼
       ┌───────────┐    Retry
       │ Completed │      │
       └───────────┘      │
                          ▼
                       Failed
```

Status değerleri:

```text
Pending    = 1
Processing = 2
Completed  = 3
Failed     = 4
```

---

# 14. SHA-256

Her belge işleme sırasında dosyanın SHA-256 hash değeri hesaplanır.

Hash değeri:

* Dosya bütünlüğünün takip edilmesi
* İşlenen içeriğin doğrulanması
* Aynı dosyanın tespit edilmesi

gibi senaryolarda kullanılabilir.

Hash database üzerinde `Sha256Hash` alanında saklanır.

---

# 15. Validation ve API Hardening

Upload endpoint'i aşağıdaki kontrolleri gerçekleştirir:

* `DocumentType` zorunluluğu
* `BatchId` zorunluluğu
* `SourceSystem` zorunluluğu
* Maksimum alan uzunlukları
* Dosya zorunluluğu
* Maksimum dosya boyutu
* Desteklenen dosya uzantısı

Configuration:

```json
{
  "DocumentProcessing": {
    "MaxFileSizeMb": 10,
    "AllowedExtensions": [
      ".pdf",
      ".jpg",
      ".jpeg",
      ".png"
    ]
  }
}
```

Batch endpoint'i ayrıca:

```text
page >= 1
1 <= pageSize <= 100
BatchId <= 100 karakter
```

kontrollerini uygular.

Manuel olarak test edilen edge-case'ler:

* Desteklenmeyen `.exe` dosyası
* Büyük dosya
* Dosyasız upload
* Olmayan document ID
* Boş BatchId
* 101 karakter BatchId
* `page=0`
* `pageSize=101`

---

# 16. Global Exception Handling

API'de merkezi exception handling için custom middleware bulunmaktadır.

Beklenmeyen exception'lar `ProblemDetails` formatında döndürülür.

Örnek:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Beklenmeyen bir hata oluştu.",
  "traceId": "..."
}
```

404 gibi framework tarafından oluşturulan hata response'ları da ProblemDetails formatında dönebilir.

`TraceId`, log ile HTTP response arasında correlation sağlamak için kullanılmaktadır.

---

# 17. Logging

Serilog kullanılmaktadır.

Loglanan önemli lifecycle noktaları:

```text
Document Worker started
Pending document count
Document claimed for processing
Document file processing started
Document OCR started
Document OCR completed
Document retrying
Document processing failed
Recovered stale processing documents
Document Worker stopped
```

Loglar:

* Console
* Günlük rolling log dosyaları

üzerinden tutulur.

Log dosyaları:

```text
Logs/app-YYYYMMDD.log
```

formatında oluşturulur.

---

# 18. Graceful Shutdown

Worker `CancellationToken` kullanmaktadır.

Application shutdown sırasında cancellation signal'ı worker'a iletilir.

Temel akış:

```text
Application Shutdown
        │
        ▼
CancellationToken
        │
        ▼
Worker stops
        │
        ▼
Document Worker stopped
```

Bu sayede worker normal .NET host lifecycle'ına uyumlu şekilde kapanır.

---

# 19. API Endpoints

## Upload Document

```http
POST /api/Documents/upload
```

`multipart/form-data` kullanılır.

Form alanları:

```text
DocumentType
BatchId
SourceSystem
file
```

Başarılı işlem sonucunda:

```text
202 Accepted
```

döner.

Response:

```json
{
  "trackingId": "document-guid"
}
```

Client daha sonra bu `trackingId` ile document durumunu sorgulayabilir.

---

## Get Document

```http
GET /api/Documents/{id}
```

Document mevcutsa detay bilgileri döndürülür.

Document bulunamazsa:

```text
404 Not Found
```

döner.

Response içerisinde temel olarak:

```text
Id
FileName
DocumentType
BatchId
SourceSystem
Status
PageCount
WordCount
Sha256Hash
ExtractedText
ErrorMessage
CreatedAt
ProcessingStartedAt
CompletedAt
```

alanları bulunur.

---

## Get Documents By Batch

```http
GET /api/Documents/batch/{batchId}
```

Pagination desteklenmektedir.

Örnek:

```http
GET /api/Documents/batch/BATCH-001?page=1&pageSize=20
```

Response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 28,
  "totalPages": 2
}
```

---

# 20. Database

Entity Framework Core ve SQL Server kullanılmaktadır.

Ana entity:

```text
Document
```

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

---

# 21. Configuration

Worker'ın temel configuration'ı:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=AsyncDocumentProcessingDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "DocumentProcessing": {
    "MaxConcurrency": 1,
    "MaxRetryCount": 3
  }
}
```

API tarafındaki processing configuration:

```json
{
  "DocumentProcessing": {
    "MaxRetryCount": 3,
    "MaxFileSizeMb": 10,
    "AllowedExtensions": [
      ".pdf",
      ".jpg",
      ".jpeg",
      ".png"
    ]
  }
}
```

> `MaxConcurrency` değerini fiilen kullanan bileşen Worker'dır. Mevcut stabil çalışma ayarı Worker tarafında `1`'dir.

---

# 22. Kurulum

## Gereksinimler

* .NET 8 SDK
* SQL Server
* Visual Studio 2022 veya .NET CLI

## Database Connection

Varsayılan connection string:

```text
Server=.;
Database=AsyncDocumentProcessingDb;
Trusted_Connection=True;
TrustServerCertificate=True;
```

Migration işlemleri Entity Framework Core üzerinden yapılabilir.

Migration oluşturmak:

```powershell
dotnet ef migrations add InitialCreate `
  --project ".\AsyncDocumentProcessing.Infrastructure" `
  --startup-project ".\AsyncDocumentProcessing.Api"
```

Database'i güncellemek:

```powershell
dotnet ef database update `
  --project ".\AsyncDocumentProcessing.Infrastructure" `
  --startup-project ".\AsyncDocumentProcessing.Api"
```

Eğer `dotnet ef` komutu bulunamazsa:

```powershell
dotnet tool install --global dotnet-ef
```

---

# 23. Uygulamayı Çalıştırma

Solution klasörüne geç:

```powershell
cd "C:\Users\Emre\source\repos\AsyncDocumentProcessing"
```

Build:

```powershell
dotnet build ".\AsyncDocumentProcessing.sln"
```

## API

Yeni bir terminal:

```powershell
cd ".\AsyncDocumentProcessing.Api"

dotnet run --launch-profile https
```

API:

```text
https://localhost:7256
```

Swagger:

```text
https://localhost:7256/swagger
```

## Worker

Ayrı bir terminal:

```powershell
cd "C:\Users\Emre\source\repos\AsyncDocumentProcessing\AsyncDocumentProcessing.Worker"

dotnet run
```

API ve Worker aynı SQL Server database'ini kullanmalıdır.

---

# 24. Testler

Test framework:

```text
xUnit
```

Integration testing:

```text
Microsoft.AspNetCore.Mvc.Testing
```

Testleri çalıştırmak:

```powershell
cd "C:\Users\Emre\source\repos\AsyncDocumentProcessing"

dotnet test ".\AsyncDocumentProcessing.sln"
```

Son başarılı test sonucu:

```text
Test summary:
total: 7
failed: 0
succeeded: 7
skipped: 0
```

Build sonucu:

```text
Build succeeded
```

Test edilen temel senaryolar:

* Upload document
* Document status query
* Non-existing document
* Batch pagination
* Successful processing
* Retry limit
* Swagger endpoint

Bunlara ek olarak manuel hardening testleri ile gerçek OCR, stale recovery ve çeşitli invalid request senaryoları doğrulanmıştır.

---

# 25. Gerçek OCR Testi

Gerçek PDF OCR işlemi end-to-end olarak doğrulanmıştır.

Örnek başarılı işlem sonucu:

```text
Status: Completed
PageCount: 1
WordCount: 3
RetryCount: 0
SHA256: populated
ErrorMessage: NULL
LastErrorMessage: NULL
CompletedAt: populated
```

Worker loglarında aşağıdaki lifecycle gözlemlenmiştir:

```text
Pending document count
Document claimed for processing
Document file processing started
Document OCR started
Document OCR completed
Document processing completed
```

---

# 26. Teknik Kararlar

## SQL Server'ı iş kuyruğu olarak kullanmak

İş durumunun kalıcı olması ve Worker restart sonrasında `Pending` kayıtların tekrar bulunabilmesi için SQL Server document state kullanılmıştır.

## Atomic Claim

Concurrent worker senaryolarında aynı document'ın birden fazla kez işlenmesini önlemek için database-level atomic update uygulanmıştır.

## BackgroundService

OCR gibi uzun süren işlemleri HTTP request lifecycle'ından ayırmak için kullanılmıştır.

## Tesseract

Gerçek OCR ihtiyacını karşılamak ve Türkçe/İngilizce OCR desteği sağlamak amacıyla kullanılmıştır.

## Docnet

PDF sayfalarını OCR motorunun işleyebileceği görüntülere dönüştürmek amacıyla kullanılmıştır.

## Retry

Geçici veya tekrar denenmesi anlamlı olan processing hatalarında belgeyi doğrudan `Failed` durumuna düşürmemek için uygulanmıştır.

## Stale Recovery

Worker'ın beklenmedik şekilde kapanması sonucunda `Processing` durumunda kalan belgelerin yeniden işlenebilmesini sağlamak için uygulanmıştır.

## Serilog

Uygulama lifecycle'ının ve processing hatalarının izlenebilir olması amacıyla kullanılmıştır.

---

# 27. Projenin Güçlü Yönleri

Bu proje özellikle aşağıdaki backend konularını göstermeyi amaçlamaktadır:

* Clean Architecture yaklaşımı
* Separation of Concerns
* Dependency Injection
* Repository Pattern
* Background Processing
* SQL-backed durable work state
* Atomic concurrency control
* Retry / recovery
* CancellationToken
* Validation
* Global exception handling
* ProblemDetails
* Structured logging
* Real OCR integration
* Pagination
* Automated testing
* API hardening

---

# 28. Bilinen Kısıtlar

Mevcut PDF OCR implementasyonunda Docnet/FPDF native katmanının concurrent processing altında `AccessViolationException` üretebildiği gözlemlenmiştir.

Bu nedenle mevcut production-like çalışma configuration'ında:

```text
MaxConcurrency = 1
```

tercih edilmektedir.

Daha yüksek concurrency için aşağıdaki yaklaşımlar değerlendirilebilir:

* PDF rendering işlemlerini izole etmek
* Native renderer kullanımını serialize etmek
* Farklı bir PDF rendering kütüphanesine geçmek
* OCR işlemlerini ayrı worker process'lerine dağıtmak

Bu durum projenin concurrency tasarımının önünde kalıcı bir mimari engel olarak değil, kullanılan native PDF processing katmanının mevcut davranışı olarak değerlendirilmektedir.

---

# 29. Genel İş Akışı

```text
                     Client
                       │
                       ▼
                POST /upload
                       │
                       ▼
                Request Validation
                       │
                       ▼
                 File Validation
                       │
                       ▼
                 File Storage
                       │
                       ▼
               SQL Document
                 Status=Pending
                       │
                       ▼
                Background Worker
                       │
                       ▼
                 TryClaimAsync
                       │
                       ▼
                Status=Processing
                       │
                       ▼
              DocumentProcessor
                       │
             ┌─────────┴─────────┐
             │                   │
             ▼                   ▼
          SHA-256             OCR
                                 │
                        ┌────────┴────────┐
                        │                 │
                       PDF              Image
                        │                 │
                      Docnet          Tesseract
                        │                 │
                        └────────┬────────┘
                                 │
                                 ▼
                           Extracted Text
                                 │
                                 ▼
                            SQL Server
                                 │
                    ┌────────────┴────────────┐
                    │                         │
                 Success                    Error
                    │                         │
                    ▼                         ▼
                Completed                   Retry
                                              │
                                   ┌──────────┴──────────┐
                                   │                     │
                               Successful             Failed
                                   │                     │
                                   ▼                     ▼
                              Completed               Failed
```

---

# 30. Sonuç

`AsyncDocumentProcessing`, belge upload ve OCR işlemlerini birbirinden ayıran, SQL Server tabanlı kalıcı iş durumu kullanan ve Background Worker ile belge işleyen bir .NET 8 backend uygulamasıdır.

Temel yaklaşım:

> **HTTP request'i hızlı tamamla, belgeyi kalıcı olarak kaydet ve ağır OCR işlemlerini kontrollü, izlenebilir ve recovery destekli şekilde arka planda gerçekleştir.**

Proje; gerçek OCR, retry, stale recovery, atomic claim, validation, logging, exception handling ve automated testing gibi gerçek backend problemlerini çözmeye odaklanmaktadır.
