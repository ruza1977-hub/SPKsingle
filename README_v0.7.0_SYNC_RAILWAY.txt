SISTEM PENGURUSAN KEHADIRAN — WINDOWS NATIVE v0.7.0
FOKUS: SYNC RAILWAY / FASTAPI SEBENAR (PARITI SENI BINA macOS)

APA YANG DITAMBAH
1. Halaman Sync Railway direka semula:
   - status server
   - queue lokal
   - konflik
   - revision pull
   - masa sync terakhir
   - device_id
   - pecahan pending mengikut modul
   - senarai konflik read-only
2. Auto Sync pilihan pengguna:
   - 5 / 15 / 30 / 60 minit
   - default tidak aktif
   - tidak berjalan jika Server Config/token tiada
   - tidak bertindih dengan sync manual
3. Enjin sync incremental dua hala:
   - push rekod sync_status='pending'
   - pull berdasarkan last_pull_revision
   - pagination has_more
   - UUID + version + updated_at + soft delete
   - BLOB gambar/PDF dikod base64 mengikut Sync API Contract v1
4. Tenant guard diperketat:
   - school_id Server Config mesti sama dengan SQLite lokal
   - respons server yang membawa school_id asing ditolak
5. Keselamatan apply server:
   - nama table hanya daripada whitelist SyncTables
   - column dari server ditapis mengikut schema SQLite sebenar
6. Konflik:
   - rekod lokal pending tidak ditimpa
   - sync_status='conflict'
   - local_version/server_version direkod
   - konflik sama tidak menduplikasi tanpa had
7. Token server kekal di Windows DPAPI (CurrentUser), bukan plaintext dalam SQLite/backup.
8. Restore database kini menghentikan Auto Sync sepanjang Maintenance Mode dan menghidupkannya semula selepas restore/rollback.
9. Backup kekal standard macOS canonical:
   database/sistem_pengurusan_kehadiran.sqlite3

SERVER CONFIG ZIP
- base_url: HTTPS Railway/FastAPI
- api_path: /api/v1/sync
- school_id: ID tenant/sekolah yang dikeluarkan server
- token: token sync server

Jangan gunakan school_id rekaan atau school_id sekolah lain.

UJIAN CADANGAN DI WINDOWS
1. Clean Solution.
2. Rebuild Solution.
3. Buka Sync Railway.
4. Import Server Config ZIP melalui Backup / Restore jika belum ada.
5. Tekan Uji Server.
6. Pastikan school_id sepadan.
7. Ubah satu data kecil, contoh catatan murid.
8. Pastikan Queue Lokal meningkat.
9. Tekan Sync Sekarang.
10. Pastikan Queue Lokal menurun dan Revision berubah.
11. Aktifkan Auto Sync 15 minit jika dikehendaki.
12. Uji backup/restore sekali untuk pastikan Maintenance Mode tidak mengganggu SQLite.

CATATAN BUILD
Pakej ini disediakan dan diperiksa secara statik dalam persekitaran bukan Windows.
Build WinUI 3 sebenar perlu dibuat dalam Visual Studio Windows.
