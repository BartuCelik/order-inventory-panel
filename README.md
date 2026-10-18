# Sipariş & Stok Paneli

KOBİ'ler için sipariş ve stok takibi yapan bir yönetim paneli. Angular ile yazılmış bir arayüz, .NET 10 ile katmanlı mimaride bir Web API ve SQL Server veritabanından oluşur.

> **Durum:** Proje iskeleti hazır. İş mantığı (ürünler, siparişler, stok hareketleri) henüz eklenmedi.

## Teknolojiler

| Katman      | Teknoloji                                                        |
| ----------- | ---------------------------------------------------------------- |
| Arayüz      | Angular 22 (standalone components, signals), Angular Material 3 |
| API         | .NET 10 Web API, Entity Framework Core 10, OpenAPI               |
| Veritabanı  | SQL Server 2022 (Docker)                                         |

## Mimari

```
siparis-stok-paneli/
├── api/                          .NET 10 solution (SiparisStok.slnx)
│   └── src/
│       ├── SiparisStok.Domain          Entity'ler ve domain kuralları (bağımlılığı yok)
│       ├── SiparisStok.Application     Use case'ler, arayüzler      → Domain
│       ├── SiparisStok.Infrastructure  EF Core, DbContext, dış servisler → Application
│       └── SiparisStok.Api             HTTP katmanı, DI kurulumu    → Application, Infrastructure
├── client/                       Angular uygulaması
│   └── src/app/
│       ├── core/                       Uygulama genelindeki servisler (API, health)
│       ├── layout/                     Uygulama kabuğu (toolbar, sidenav)
│       └── features/                   Özellik modülleri (dashboard, ...)
└── docker-compose.yml            SQL Server
```

Bağımlılıklar dıştan içe doğru gider (Clean Architecture). Domain hiçbir katmana bağlı değildir. Altyapıya özel kod (EF Core, SQL Server) yalnızca Infrastructure katmanında bulunur.

## Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) ve npm
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows'ta WSL 2 ve BIOS'ta sanallaştırma açık olmalı)

## Çalıştırma

**1. Veritabanı**

```bash
cp .env.example .env          # gerekirse parolayı değiştirin
docker compose up -d
```

**2. API** → http://localhost:5080

```bash
cd api
dotnet run --project src/SiparisStok.Api
```

Development ortamında API açılırken veritabanı yoksa oluşturulur ve bekleyen EF Core migration'ları uygulanır.

**3. Client** → http://localhost:4200

```bash
cd client
npm install
npm start
```

Client'tan `/api/*` adresine giden istekler, geliştirme ortamında `proxy.conf.json` üzerinden API'ye yönlendirilir.

> `.env` dosyasındaki parolayı değiştirirseniz `api/src/SiparisStok.Api/appsettings.Development.json` içindeki connection string'i de güncelleyin. Alternatif olarak user-secrets kullanabilirsiniz:
> `dotnet user-secrets set "ConnectionStrings:Default" "<connection-string>" --project src/SiparisStok.Api`

## Endpoint'ler

| Endpoint                | Açıklama                                                    |
| ----------------------- | ----------------------------------------------------------- |
| `GET /health`           | Uygulama ve veritabanı bağlantısının durumu (sorun varsa 503) |
| `GET /health/live`      | Uygulamanın ayakta olup olmadığı, dış bağımlılıklara bakmaz |
| `GET /openapi/v1.json`  | OpenAPI dokümanı (yalnızca Development)                     |

## Portlar

| Servis     | Port |
| ---------- | ---- |
| Client     | 4200 |
| API        | 5080 |
| SQL Server | 1433 |

## Yol haritası

- [ ] Ürün ve kategori yönetimi
- [ ] Stok hareketleri (giriş / çıkış / sayım)
- [ ] Sipariş yönetimi ve durum akışı
- [ ] Kritik stok uyarıları
- [ ] Kimlik doğrulama ve yetkilendirme
- [ ] Testler ve CI
