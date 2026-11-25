# Master Plan & Rangkuman Fitur Sistem Lama (RISE - Ultimate Project Manager & CRM)

Dokumen ini merupakan rangkuman komprehensif mengenai arsitektur, modul, dan fitur yang dimiliki oleh sistem lama (**RISE - Ultimate Project Manager & CRM** oleh Fairsketch) berdasarkan dokumentasi resmi pada `Source/documentation/index.html` dan `Source/documentation/plugin-documentation.html`. Dokumen ini disusun sebagai acuan utama (*Master Plan*) dalam memahami kapabilitas sistem untuk pengembangan lebih lanjut.

---

## 1. Gambaran Umum & Spesifikasi Teknis Sistem Lama

* **Nama Sistem**: RISE - Ultimate Project Manager & CRM (Berbasis v2.8+)
* **Kategori Aplikasi**: *All-in-One Project Management, Customer Relationship Management (CRM), Team & HR Management, Billing/Invoicing, dan Helpdesk Support System*.
* **Arsitektur & Stack Teknologi**:
  * **Backend Framework**: PHP (v7.3+) dengan **CodeIgniter 4** (`App\Controllers\Security_Controller` untuk halaman terproteksi, `App\Controllers\App_Controller` untuk halaman publik, dan `App\Models\Crud_model` untuk operasi I/O database).
  * **Database**: MySQL / MariaDB.
  * **Frontend**: Bootstrap, jQuery 3.5.1, Feather Icons, dan DataTables (dikelola melalui pustaka kustom `assets/js/app.js` / `app.min.js`).
  * **Ekstensi PHP Wajib**: `MySQLi`, `cURL`, `mbstring`, `OpenSSL`, `GD`, `intl`, `json`, `mysqlnd`, `xml` (dengan `zlib.output_compression` dinonaktifkan dan `date.timezone` terkonfigurasi).
* **Struktur Direktori Utama**:
  * `/app` — Berisi `Config`, `Controllers` (logika bisnis), `Language` (`default_lang.php` & `custom_lang.php`), `Models`, `Views`, dan `Helpers`.
  * `/assets` — File statis CSS, JavaScript, dan gambar.
  * `/files` — Direktori penyimpanan berkas unggahan lokal.
  * `/install` — Wizard instalasi awal aplikasi.
  * `/system` — Core framework CodeIgniter.
  * `/updates` — Paket pembaruan versi aplikasi.
  * `/writable` — Direktori tulis untuk cache dan log (`\writable\logs\`).

---

## 2. Peta Modul & Fitur Utama Aplikasi

Berikut adalah rincian seluruh modul dan fitur yang tersedia pada sistem lama:

### 2.1. Dashboard & Navigasi Utama (Topbar)
1. **Multiple Custom Dashboards**:
   * Pengguna dapat membuat banyak dashboard kustom (*Add new dashboard*), memberi penanda warna, serta mengubah urutan (*sortable*) daftar dashboard selain dashboard bawaan (*Default dashboard*).
2. **Drag-and-Drop Dashboard Builder**:
   * Pengaturan tata letak baris dan variasi kolom secara fleksibel.
   * *Drag-and-drop* berbagai widget bawaan maupun **Custom Widgets** (mendukung penyematan `iframe` untuk menampilkan data dari situs web eksternal).
   * Fitur *preview* widget, simpan tanpa keluar dari panel edit, serta penanganan visual otomatis apabila pengguna kehilangan hak akses ke suatu widget.
3. **Kustomisasi Dashboard Portal Klien**:
   * Admin dapat menyesuaikan tata letak dashboard khusus untuk portal klien dan menampilkan widget sesuai kebutuhan bisnis.
4. **Top Navigation Bar (Topbar)**:
   * **Sidebar Toggle**: Menyembunyikan/menampilkan menu navigasi kiri.
   * **Personal To-Do List**: Akses cepat ke daftar pekerjaan pribadi harian/mendatang.
   * **Starred Projects & Starred Clients**: Dropdown akses cepat ke proyek dan klien yang ditandai sebagai favorit.
   * **Dashboards Switcher**: Dropdown perpindahan cepat antar dashboard beserta indikator warnanya.
   * **Quick Add Button**: Tombol tambah cepat dari halaman mana pun untuk membuat *task*, *multiple tasks*, *event*, *ticket*, dll.
   * **Language Selector**: Pengaturan bahasa antarmuka khusus untuk pengguna yang sedang login.
   * **Notifications & New Messages**: Indikator dan daftar notifikasi aktivitas serta pesan masuk secara *real-time*.
   * **Profile Menu**: Akses profil pengguna, ubah kata sandi, pemilihan tema warna (*brand color*, *Light Mode* & *Dark Mode*), dan keluar (*sign out*).

---

### 2.2. CRM: Manajemen Prospek (Leads) & Pelanggan (Clients)
1. **Manajemen Prospek (Leads)**:
   * Akuisisi prospek otomatis dari formulir permintaan estimasi (*Estimate Request Form*) publik yang dapat dibagikan di media sosial atau situs eksternal.
   * Pengelolaan prospek menggunakan tampilan **Kanban View** untuk memindahkan prospek antar tahapan (*stages*).
   * Fitur impor data *leads* dan *clients* secara massal menggunakan file **Excel**.
2. **Manajemen Klien (Clients)**:
   * Operasi CRUD klien lengkap dengan fitur pencarian, filter berdasarkan **Client Groups**, pengaturan jumlah baris per halaman, **Export ke Excel**, dan **Print**.
   * **10-Second Undo Delete**: Saat menghapus klien atau kontak klien, sistem memberikan waktu 10 detik untuk membatalkan (*undo*) penghapusan sebelum data dihapus permanen dari database.
   * **Struktur Perusahaan & Multi-Kontak**: Setiap klien didaftarkan sebagai entitas perusahaan/organisasi yang dapat memiliki banyak kontak pengguna (*Contacts*) dengan akses login ke *Client Portal*.
   * **Multi-Currency per Klien**: Pengaturan mata uang dan simbol mata uang yang berbeda untuk setiap klien (misalnya klien luar negeri), yang otomatis diterapkan pada pembuatan tagihan (*invoice*).
   * **Pemantauan Aktivitas**: Pelacakan informasi login terakhir (*last login info*) pelanggan.
3. **Halaman Detail Klien (11 Tab Terintegrasi)**:
   * **Contacts**: Daftar kontak klien, tambah kontak, dan tombol **Send Invitation** untuk mengirim undangan registrasi portal klien via email.
   * **Client Info**: Informasi profil lengkap dan alamat klien.
   * **Projects**: Daftar seluruh proyek milik klien.
   * **Invoices**: Daftar seluruh tagihan klien.
   * **Payments**: Riwayat seluruh pembayaran dari klien.
   * **Estimates**: Daftar penawaran harga untuk klien.
   * **Estimate Requests**: Riwayat permintaan estimasi yang diajukan klien atau dibuat oleh tim.
   * **Tickets**: Daftar tiket dukungan (*support tickets*) klien.
   * **Notes**: Catatan internal/privat anggota tim mengenai klien (tidak terlihat oleh klien).
   * **Files**: Berkas internal/privat anggota tim terkait klien (tidak terlihat oleh klien).
   * **Events**: Daftar agenda/acara yang berkaitan dengan klien.
4. **Client Portal & Online Store**:
   * Portal mandiri bagi klien untuk memantau proyek, melihat *timesheet* proyek (jika diizinkan), menyetujui/menolak estimasi, melihat & membayar invoice secara online, membuat tiket dukungan, membaca artikel *Knowledge Base*, serta memesan produk/layanan melalui **Online Store**.

---

### 2.3. Manajemen Proyek, Tugas & Milestone (Projects, Tasks, Milestones)
1. **Manajemen Proyek (Projects)**:
   * Pembuatan, pengeditan, perubahan status, dan duplikasi proyek (**Clone Project**).
   * **Indikator Warna Deadline Otomatis**:
     * **Hitam**: Tenggat waktu masih berlaku normal.
     * **Oranye**: Tenggat waktu jatuh pada hari ini.
     * **Merah**: Tenggat waktu telah terlewati (*expired*) untuk proyek yang belum selesai.
   * **Kalkulasi Progres Berbasis Poin Tugas (*Task Points*)**:
     * *Progress bar* proyek dihitung otomatis berdasarkan bobot poin tugas:
       $$\text{Progress (\%)} = \frac{\text{Total Poin Tugas Selesai (Done)} \times 100}{\text{Total Poin Seluruh Tugas}}$$
   * Filter cepat berdasarkan satu atau beberapa **Status**, **Deadline**, dan **Labels**.
   * **Visualisasi & Grafik Proyek**:
     * *Project Progression Chart* & *Total Tasks Status Chart*.
     * *Activity Timeline* (riwayat log aktivitas proyek).
     * **Gantt Chart** untuk visualisasi *roadmap* dan jadwal proyek.
   * **Pelacakan Waktu Proyek (*Project Timer*)**:
     * Tombol **Start Time** dan **Stop Timer** langsung pada halaman detail proyek untuk mencatat *timesheet* kerja, dilengkapi opsi untuk membagikan *project timesheet* kepada pelanggan pada proyek yang dipilih.
   * **Kolaborasi Berkas & Umpan Balik**:
     * Unggah file proyek, diskusi/komentar pada file beserta notifikasi, catatan proyek, pelacakan pengeluaran proyek, serta modul **Customer Feedback** untuk menerima umpan balik langsung dari klien.
2. **Manajemen Tugas (Tasks)**:
   * Pembuatan tugas tunggal (**Add task**) maupun pembuatan banyak tugas sekaligus dalam satu modal (**Add multiple tasks**).
   * Penugasan ke anggota tim (*Assignee*) serta dukungan banyak kolaborator (**Multiple Collaborators**) pada satu tugas.
   * *Instant Checkbox* untuk menandai tugas selesai (*Done*) secara instan dari daftar tugas.
   * **Batch Update**: Mengubah *milestone*, *assigned to*, *status*, atau atribut lainnya pada banyak tugas sekaligus menggunakan panel seleksi massal.
   * Evaluasi performa tim berdasarkan rincian waktu yang dihabiskan (*time spent*) pada setiap tugas dan proyek.
   * Integrasi otomatis dengan log *commit* **GitHub** dan **Bitbucket** menggunakan pencantuman `#TaskID` pada pesan commit.
3. **Manajemen Milestone**:
   * Pengelompokan tugas ke dalam tahapan (*Milestones*) beserta tenggat waktunya.
   * Indikator warna otomatis: **Hijau** jika seluruh tugas dalam milestone selesai, dan **Merah** jika tenggat waktu berakhir hari ini/terlewat namun masih ada tugas yang belum selesai.
   * *Progress bar* per milestone menggunakan kalkulasi bobot poin tugas yang sama dengan proyek.

---

### 2.4. Estimasi, Penagihan, Pembayaran & Pengeluaran (Finance)
1. **Estimate Requests (Permintaan Estimasi)**:
   * Pembuatan formulir kustom (**Estimate Forms**) tanpa batas.
   * Formulir yang diatur sebagai **Public** dapat diakses oleh siapa saja (termasuk pengunjung yang tidak login) untuk mengumpulkan prospek dari luar.
   * Klien maupun staf dapat mengajukan permintaan estimasi, mengelola statusnya, dan langsung membuat **Estimate** dari permintaan tersebut.
2. **Estimates (Penawaran Harga)**:
   * Pembuatan dokumen penawaran harga berisi rincian item/produk.
   * Hak akses berbasis peran (*Role*) untuk staf, sedangkan klien hanya dapat melihat estimasi miliknya.
   * Klien atau admin dapat melakukan **Accept** (Setuju) atau **Reject** (Tolak) terhadap estimasi yang dikirim.
   * Setelah disetujui, estimasi dapat langsung dikonversi menjadi **Invoice** atau **Project** melalui dropdown *Actions*.
3. **Invoices & Payments (Tagihan & Pembayaran)**:
   * **Siklus Status Tagihan**:
     * **Draft**: Status awal saat invoice baru dibuat.
     * **Sent**: Saat invoice telah dikirim ke klien.
     * **Partially Paid**: Saat menerima pembayaran sebagian.
     * **Fully Paid**: Saat tagihan telah dibayar lunas.
     * **Cancelled**: Saat tagihan dibatalkan (melalui konfirmasi permanen yang tidak dapat dibatalkan).
   * **Filter Multi-Mata Uang**: Filter daftar invoice berdasarkan mata uang klien (otomatis tampil jika sistem memiliki klien dengan lebih dari satu mata uang).
   * **Manajemen Item, Pajak & Diskon**:
     * Tambah, ubah, dan hapus baris item/produk pada invoice.
     * Pengaturan multi-pajak (*Taxes*) dan **Discount** fleksibel (dapat diterapkan **sebelum atau sesudah pajak**, baik berupa **nominal tetap / fixed amount** maupun **persentase / percentage**).
   * **Distribusi & Cetak Dokumen**:
     * **Email invoice to client** (kirim email beserta lampiran PDF invoice), **Download PDF**, **Invoice Preview**, dan **Print Invoice**.
   * **Pembayaran Manual & Payment Gateway Online**:
     * Pencatatan pembayaran manual melalui modal **Add payment**.
     * Pembayaran online terintegrasi melalui **Stripe**, **PayPal**, dan **Paytm** yang secara otomatis memperbarui status pembayaran di dalam sistem dan mengirimkan notifikasi email ke pelanggan.
4. **Expenses (Pengeluaran)**:
   * Terletak pada menu **Finance > Expenses** dengan pembatasan akses berbasis peran (*Role*).
   * Pencatatan pengeluaran operasional berdasarkan kategori (*Expense Categories*), proyek, atau anggota tim untuk memantau laba/rugi (*profit/loss*).

---

### 2.5. Dukungan Pelanggan & Basis Pengetahuan (Support & Knowledge Base)
1. **Support Tickets (Tiket Bantuan)**:
   * Tiket dapat dibuat oleh staf atas nama klien, oleh klien sendiri melalui *Client Portal*, atau **dibuat otomatis dari email masuk melalui integrasi IMAP**.
   * Filter tiket berdasarkan tanggal pembuatan (*Created Date*), label (*Labels*), dan anggota tim yang menangani (*Assigned Member*).
   * **Instant Assign to Me**: Opsi penugasan instan ke diri sendiri melalui dropdown apabila tiket belum memiliki penanggung jawab.
2. **Help & Knowledge Base**:
   * Dikelola di bawah menu **Help & Support** dengan pengelompokan berbasis **Categories** dan **Articles**.
   * **Help (Internal Wiki)**: Artikel panduan dan dokumentasi internal yang hanya dapat diakses oleh anggota tim.
   * **Knowledge Base**: Artikel bantuan mandiri yang dapat diakses oleh pelanggan (serta anggota tim).

---

### 2.6. Manajemen Tim, Kehadiran & Cuti (Team & HR)
1. **Team Members (Anggota Tim)**:
   * Mendukung 2 mode tampilan daftar anggota tim.
   * Penambahan anggota tim baru melalui **Send Invitation** (undangan pendaftaran via email) atau **Add Member** (input manual).
   * **7 Tab Detail Anggota Tim**:
     1. **Timeline**: Seluruh kiriman (*posts*) dari anggota tim tersebut.
     2. **General Info**: Data diri dan informasi dasar anggota tim.
     3. **Social Links**: Tautan akun media sosial.
     4. **Account Settings**: Pengaturan akses login dan penetapan peran (**Role**).
     5. **Job Info**: Jabatan (*job title*), gaji (*salary*), dan informasi pekerjaan lainnya.
     6. **Time Cards**: Riwayat lengkap absensi/kehadiran anggota tim.
     7. **Leave**: Riwayat lengkap pengajuan dan penggunaan cuti anggota tim.
2. **Time Cards (Absensi & Jam Kerja Harian)**:
   * Pencatatan kehadiran harian oleh anggota tim menggunakan tombol **Clock In** dan **Clock Out** di dashboard.
   * Admin atau staf yang berwenang dapat menambahkan catatan waktu kerja secara manual.
   * **IP Restriction**: Pembatasan agar anggota tim non-admin hanya dapat melakukan *Clock In / Clock Out* dari daftar alamat IP yang diizinkan (pengguna Admin dikecualikan dari batasan ini dan dapat mengakses dari IP mana pun).
3. **Leave (Manajemen Cuti)**:
   * **Apply Leave**: Anggota tim mengajukan permohonan cuti sendiri.
   * **Assign Leave**: Admin atau atasan menetapkan cuti langsung untuk bawahan.
   * **Approval Workflow**: Admin atau atasan menyetujui (**Approve**) atau menolak (**Reject**) pengajuan cuti (keputusan bersifat permanen/tidak dapat diubah kembali).
   * Dilengkapi tab **All Applications** (daftar seluruh pengajuan) dan tab **Summary** (grafik ringkasan tahunan cuti yang disetujui) serta pengaturan **Leave Types** kustom.

---

### 2.7. Komunikasi, Kalender & Kolaborasi Internal
1. **Messaging (Pesan Langsung)**:
   * Seluruh anggota tim dapat saling berkirim pesan tanpa batasan.
   * Komunikasi pesan antara anggota tim dan klien dikendalikan penuh oleh Admin melalui **Client Permissions** (menentukan siapa saja staf yang diizinkan berkomunikasi dengan klien).
2. **Timeline (Dinding Kolaborasi Tim)**:
   * Area percakapan dan kolaborasi internal bagi seluruh anggota tim untuk berbagi ide, pembaruan, dan mengunggah berkas (*drag-and-drop* atau tombol *Upload File* sesuai ekstensi file yang diizinkan di General Settings).
3. **Events (Kalender Acara Terpadu)**:
   * Kalender interaktif untuk membuat acara pribadi maupun acara bersama dengan pembeda warna per acara.
   * Berbagi acara dengan anggota tim atau klien menggunakan pengaturan izin akses yang berbeda.
   * Menampilkan ikhtisar jadwal proyek, tugas, dan cuti dalam satu tampilan kalender yang sama, serta mendukung sinkronisasi dua arah dengan **Google Calendar**.
4. **Notes (Catatan Pribadi)**:
   * Seluruh catatan bersifat **privat** (hanya pembuat catatan yang dapat melihatnya).
   * Terdiri dari **Sticky Note** di dashboard untuk catatan cepat, serta halaman **Notes** (catatan yang dibuat di dalam proyek juga otomatis tergabung dalam daftar di halaman Notes).
5. **Announcements (Pengumuman)**:
   * Admin dan anggota tim terpilih dapat mempublikasikan pengumuman untuk anggota tim dan/atau klien.
   * Pengumuman aktif ditampilkan di bagian atas dashboard setiap pengguna hingga pengguna mengkliknya, dan tetap tersimpan di menu **Announcements** untuk dibaca kembali kapan saja.

---

### 2.8. Pengaturan Sistem, Hak Akses (RBAC) & Kustomisasi
1. **General Settings**:
   * **General**: Ubah logo aplikasi, judul, lokalisasi, pembatasan format file unggahan, dan kode lisensi (*item purchase code*).
   * **Email & Email Templates**: Konfigurasi pengiriman email keluar (Server Mail bawaan atau SMTP) dan editor tampilan template email sistem.
   * **Modules**: Sakelar untuk mengaktifkan atau menonaktifkan modul-modul aplikasi sesuai kebutuhan organisasi.
   * **Cron Job, Notifications, Integration, Updates**: Pengaturan penjadwalan latar belakang, matriks saluran notifikasi (Web, Email, Push, Slack) per event, konfigurasi API pihak ketiga, dan pembaruan versi aplikasi otomatis.
2. **Access Permission (Manajemen Hak Akses)**:
   * **Roles (RBAC)**: Pembuatan berbagai peran kustom dengan izin akses (*permissions*) terperinci per modul. Setiap anggota tim memiliki satu peran aktif (default: *Team Member*).
   * **Team**: Pengelompokan anggota ke dalam tim kerja untuk memudahkan penugasan peran, berbagi acara, dan pengiriman notifikasi.
   * **IP Restriction**: Pengaturan daftar alamat IP resmi untuk fitur *Time Cards*.
3. **Client Settings**:
   * **Client Permissions**: Mengatur izin login/registrasi klien serta aturan pengiriman pesan antara klien dan staf.
   * **Client Groups**: Mengelola pengelompokan kategori klien.
4. **Setup (Data Master & Konfigurasi Modul)**:
   * **Custom Fields**: Pembuatan kolom input tambahan kustom pada berbagai entitas (Projects, Clients, Tasks, Events, dll.) dengan **8 pilihan tipe field**, pengaturan apakah ditampilkan pada tabel daftar, serta pembatasan visibilitas pengguna.
   * **Tasks**: Pengaturan daftar status tugas (*Task Status*).
   * **Events**: Pengaktifan fitur Google Calendar API untuk pengguna.
   * **Leave Types**: Pengaturan kategori/jenis cuti.
   * **Tickets**: Pengaturan jenis tiket (*Ticket Types*), konfigurasi tiket, dan pengaturan IMAP.
   * **Expense Categories**: Pengaturan kategori pengeluaran keuangan.
   * **Invoice**: Pengaturan logo invoice, gaya desain invoice (*invoice style*), pengingat tagihan (*invoice reminders*), dll.
   * **Payment Methods**: Pengelolaan metode pembayaran tagihan yang tersedia.
   * **Company**: Informasi identitas perusahaan yang dicetak pada dokumen Invoice dan Estimate.
   * **Taxes**: Pengaturan daftar nama dan persentase pajak untuk Invoice dan Estimate.

---

### 2.9. Integrasi Pihak Ketiga & Otomatisasi (Integrations & Cron Job)
1. **Google Drive API**:
   * Integrasi OAuth 2.0 (`google_api/save_access_token`) untuk menjadikan Google Drive sebagai media penyimpanan *cloud* alternatif bagi seluruh berkas yang diunggah ke aplikasi.
2. **Google Calendar API**:
   * Integrasi OAuth 2.0 per pengguna (`google_api/save_access_token_of_calendar`) untuk sinkronisasi acara dua arah antara aplikasi dan Google Calendar pribadi pengguna, termasuk dukungan penambahan **Special Calendar** (seperti kalender hari libur).
   * Perubahan acara di aplikasi langsung diperbarui ke Google Calendar secara instan, sedangkan perubahan di Google Calendar ditarik secara berkala melalui Cron Job.
3. **IMAP Email-to-Ticket**:
   * Memantau kotak masuk email dukungan dan secara otomatis membuat tiket baru dari setiap email yang belum dibaca (*unread*), lalu menandainya sebagai telah dibaca (*read*).
   * Balasan email yang memuat *Ticket ID* pada subjek email akan otomatis digabungkan sebagai balasan pada tiket yang bersangkutan.
4. **Push Notification (Pusher)**:
   * Integrasi dengan **Pusher Channels** untuk mengirimkan notifikasi *real-time* (berupa *browser push notification* pada koneksi HTTPS atau *app alert notification* di dalam aplikasi).
5. **Slack Incoming Webhooks**:
   * Mengirimkan notifikasi aktivitas proyek dan sistem secara otomatis ke kanal (*channel*) workspace Slack yang ditentukan.
6. **GitHub & Bitbucket Webhooks**:
   * Menghubungkan *webhook* repositori GitHub dan Bitbucket ke *webhook listener* aplikasi untuk menampilkan riwayat *commit* secara otomatis di dalam detail tugas (*Task*) dengan menyertakan `#TaskID` di akhir pesan commit (contoh: `This is a commit of Task #10`).
7. **Cron Job**:
   * Penjadwalan perintah latar belakang untuk menjalankan proses otomatis seperti sinkronisasi Google Calendar, pembacaan email IMAP menjadi tiket, pengiriman pengingat tagihan (*invoice reminders*), serta pemicu plugin (`app_hook_after_cron_run`).

---

### 2.10. Arsitektur Ekstensibilitas & Sistem Plugin
Berdasarkan `plugin-documentation.html`, sistem memiliki arsitektur modular yang memungkinkan penambahan fitur kustom melalui **Plugins** (`Settings > Plugins`):

1. **Struktur & Komponen Plugin**:
   * Setiap plugin memiliki direktori unik dengan file metadata `index.php` dan mendukung struktur MVC penuh: **Routes**, **Controllers** (mewarisi `Security_Controller`), **Models** (mewarisi `Crud_model`), **Libraries**, **Languages** (`app_lang()`), **Views** (`$this->template->rander()`, `$this->template->view()`, `view()`), **Helpers**, **ThirdParty** (Composer autoload), serta penyimpanan aset dan file tersendiri (`PLUGIN_URL_PATH`).
2. **Lifecycle & Database Hooks**:
   * Siklus hidup plugin: `register_installation_hook` (validasi lisensi & pembuatan tabel SQL), `register_uninstallation_hook`, `register_activation_hook`, `register_deactivation_hook`, dan `register_update_hook`.
   * Observasi perubahan data database: `register_data_insert_hook`, `register_data_update_hook`, dan `register_data_delete_hook`.
3. **Action & Filter Hooks (`app_hooks()`)**:
   * **Antarmuka & Navigasi**: Menambahkan item menu sidebar staf (`app_filter_staff_left_menu`) dan klien (`app_filter_client_left_menu`), widget dashboard kustom (`app_filter_dashboard_widget`), tab AJAX baru pada halaman detail proyek/staf/klien/lead/integrasi, panel kanan detail tugas (`app_hook_task_view_right_panel_extension`), ekstensi halaman Sign In/Sign Up, `<head>`, layout utama, dan *announcement bar* di dashboard.
   * **Logika Bisnis & Konfigurasi**: Menambahkan metode pembayaran baru pada invoice (`app_filter_payment_method_settings` & `app_hook_invoice_payment_extension`), mendaftarkan kategori & aturan notifikasi kustom beserta template emailnya (`app_filter_notification_config`, `app_filter_email_templates`, `app_filter_send_email_notification`), menambahkan pengaturan hak akses pada Role (`app_hook_role_permissions_extension`), serta mengintegrasikan penyedia penyimpanan *cloud* kustom (`PLUGIN_CUSTOM_STORAGE` dengan hook *upload*, *move temp*, *get content*, *get source url*, dan *delete file*).

---

## 3. Matriks Ringkasan Modul (Master Plan Reference)

| Domain / Area | Modul Utama | Fitur Kunci yang Harus Diperhatikan |
| :--- | :--- | :--- |
| **Core & UI** | Dashboard, Topbar, Settings, Plugins | Multi-dashboard *drag & drop*, *custom widget* (`iframe`), *Quick Add*, multi-bahasa, *Light/Dark mode*, *Custom Fields* (8 tipe), RBAC granular, arsitektur Hook/Plugin. |
| **CRM & Sales** | Leads, Clients, Client Portal, Online Store | Kanban Leads, *Public Estimate Request Forms*, *Excel Import/Export*, *10s Undo Delete*, Multi-kontak per klien, Multi-mata uang, Portal mandiri klien & toko layanan/produk. |
| **Project Management** | Projects, Tasks, Milestones, Gantt | Kalkulasi progres berbasis *Task Points*, indikator warna *deadline*, *Clone Project*, *Project Timer* & *Timesheets*, *Batch Update* tugas, *Add Multiple Tasks*, komentar file, *Customer Feedback*, log commit GitHub/Bitbucket. |
| **Finance & Billing** | Estimates, Invoices, Payments, Expenses | Konversi *Estimate Request* -> *Estimate* -> *Invoice/Project*, siklus status Invoice, diskon sebelum/sesudah pajak (nominal/persen), PDF & Email Invoice, pembayaran online (Stripe, PayPal, Paytm), pelacakan pengeluaran & laba/rugi. |
| **Support & Wiki** | Support Tickets, Help, Knowledge Base | Tiket manual & otomatis via **IMAP Email**, *Instant Self-Assign*, filter label/staf/tanggal, *Internal Wiki (Help)* khusus staf & *Knowledge Base* untuk klien. |
| **HR & Team** | Team Members, Time Cards, Leave | Undangan email staf, 7 tab profil pegawai (termasuk *Job Info/Salary*), *Clock In/Out* dengan **IP Restriction** (non-admin), alur pengajuan & persetujuan cuti (*Apply/Assign/Approve/Reject*). |
| **Collaboration** | Messaging, Timeline, Events, Notes, Announcements, To-Do | Chat internal & klien (dengan aturan *Client Permissions*), dinding *Timeline* berbagi ide/file, Kalender terpadu + sinkronisasi 2 arah **Google Calendar**, *Sticky Note* & catatan privat, *Personal To-Do*, pengumuman dashboard. |
| **Integrations** | Google Drive, Google Calendar, IMAP, Pusher, Slack, GitHub, Bitbucket | Penyimpanan *cloud* Google Drive, sinkronisasi kalender, tiket otomatis via IMAP, *real-time push notification* (Pusher), webhook notifikasi Slack, dan webhook commit Git (`#TaskID`). |
