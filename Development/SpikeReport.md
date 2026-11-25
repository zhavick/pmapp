# Spike Report — Fase 0 (Validasi Teknis Multi-Tenant & Plugin Engine)

Laporan ini mendokumentasikan hasil eksekusi **Fase 0 (Spike S1, S2, S3)** sesuai [DEV_STEP.md](DEV_STEP.md) §3.2 untuk memvalidasi asumsi teknis berisiko tinggi sebelum membangun monorepo **Fase 1**.

* **Lingkungan Uji**: `.NET SDK 8.0.428`, `Node.js v24.19.0`, `npm 11.17.0`, `Docker 29.9.2` (`postgres:16-alpine`).
* **Lokasi Kode Throwaway**: `_spikes/BackendSpike` dan `_spikes/FrontendSpike` *(kode eksperimen sementara; tidak masuk ke produksi)*.

---

## Ringkasan Hasil Validasi

| Kode | Fokus Validasi | Status | Ringkasan Hasil |
| :--- | :--- | :--- | :--- |
| **S1** | Isolasi Ganda Multi-Tenant (EF Core Global Filter + PostgreSQL RLS) | **LULUS (Automated Test)** | Query EF Core maupun *raw SQL* tanpa klausa `WHERE tenant_id` terbukti 100% terisolasi oleh PostgreSQL RLS. |
| **S2** | Pemuatan Plugin `.dll` saat Startup (`AssemblyLoadContext`) & Gerbang Entitlement | **LULUS (Automated Test)** | Tenant berstatus `Active` mendapat `200 OK`; Tenant berstatus `PendingApproval` ditolak `403 Forbidden` oleh `PluginEntitlementEndpointFilter`. |
| **S3** | Dynamic Frontend Plugin Bundle (`Vite Library Mode`) | **LULUS (Build & Output Verified)** | `vite build` menghasilkan `index.es.js` (`0.55 kB`) dengan `react` & `react/jsx-runtime` tereksternalisasi bersih; ditemukan catatan penting mengenai *import map* vs *global shim* untuk Fase 2. *(Catatan: eksekusi DOM di headless browser tidak terverifikasi karena keterbatasan stdout CLI browser pada mesin ini).* |

---

## Temuan Teknis Penting (Wajib Diterapkan di Fase 1 & Fase 2)

### 1. Temuan S1 — PostgreSQL Row-Level Security (RLS)
* **Wajib Role Database Non-Superuser (`NOBYPASSRLS`)**: User bawaan `postgres` (superuser) secara default **melewati (bypass)** seluruh aturan RLS. Pada aplikasi produksi & koneksi EF Core (`Core.Api`), aplikasi **wajib** terhubung menggunakan role khusus seperti `pmapp_app` yang dibuat dengan atribut `NOSUPERUSER NOBYPASSRLS`.
* **`FORCE ROW LEVEL SECURITY`**: Wajib diaktifkan pada setiap tabel agar pemilik tabel sekalipun tetap tunduk pada policy RLS.
* **Session Variable**: Menggunakan `SELECT set_config('app.current_tenant_id', @tenantId::text, false);` di `DbConnectionInterceptor.ConnectionOpened` (atau `true` jika dikunci di dalam transaksi) dan `NULLIF(current_setting('app.current_tenant_id', true), '')::uuid` pada policy mencegah error tipe data UUID saat session variable belum diset.

### 2. Temuan S2 — .NET Plugin Loader & Entitlement Gate
* **Berbagi Identitas Tipe `Abstractions.dll`**: Pada proyek plugin (`SamplePlugin.csproj`), referensi ke `Plugin.Abstractions` **wajib** menggunakan `<Private>false</Private>` dan `<ExcludeAssets>runtime</ExcludeAssets>`. Tanpa ini, `Abstractions.dll` ikut tersalin ke folder output plugin, yang berisiko dimuat dua kali di `AssemblyLoadContext` berbeda sehingga pengecekan `typeof(IPlugin).IsAssignableFrom(...)` gagal.
* **Penegakan Entitlement Terpusat**: Membungkus `app.MapGroup($"/api/v1/p/{plugin.PluginId}")` dengan `PluginEntitlementEndpointFilter` berhasil memblokir akses tenant non-aktif (`403 Forbidden` ProblemDetails) secara seragam tanpa harus menaruh pengecekan manual di setiap endpoint plugin.
* **Catatan Testing (`WebApplicationFactory`)**: Konfigurasi `PluginDir` yang dibaca pada fase top-level `Program.cs` harus disuntikkan melalui `UseSetting(...)` (bukan `ConfigureAppConfiguration` yang dievaluasi belakangan) agar terbaca saat startup *integration test*.

### 3. Temuan S3 — Vite Library Mode & Berbagi Instance React
* **Ukuran Bundle Sangat Ringan**: Dengan `external: ['react', 'react-dom', 'react/jsx-runtime']`, bundle plugin React (`index.es.js`) hanya berukuran `0.55 kB`.
* **Rekomendasi Mekanisme Runtime Sharing (Perbaikan untuk Fase 2)**:
  * Menyisipkan `<script type="importmap">` secara dinamis dari dalam JavaScript (`main.tsx`) berisiko *race condition* dengan pemuatan modul ES browser.
  * **Keputusan Implementasi Fase 2**: Gunakan **static `<script type="importmap">` di `index.html`** yang diarahkan ke endpoint/shim `/shared/*.js` (atau gunakan plugin Vite/Rollup pada build plugin yang langsung membaca `window.__PM_SHARED__`), sehingga pemuatan `React`, `ReactRouter`, dan `TanStack Query` dijamin tunggal dan bebas error *Invalid hook call*.

### 4. Catatan Lingkungan Pengembangan Windows
* PowerShell Execution Policy pada mesin ini memblokir eksekusi `npm.ps1`. Seluruh skrip atau perintah build di Windows harus memanggil `npm.cmd` / `npx.cmd`.

---

## Kesimpulan Fase 0

**Fase 0 (Spike) dinyatakan SELESAI.** Seluruh risiko teknis utama (S1, S2, S3) telah tervalidasi dan solusinya telah dirumuskan. Pengembangan siap dilanjutkan ke **Fase 1 — Fondasi Core, Multi-Tenancy & Auth**.
