SISTEM PENGURUSAN KEHADIRAN — WINDOWS NATIVE v0.5.4
STANDARD BACKUP/RESTORE = FORMAT macOS
====================================================

Perubahan utama:
1. Nama database canonical semua platform:
   database/sistem_pengurusan_kehadiran.sqlite3

2. Backup Windows kini menggunakan format manifest Mac:
   package_type: sistem_pengurusan_kehadiran_backup
   package_version: 1.0
   database_file: database/sistem_pengurusan_kehadiran.sqlite3

3. validation.json turut dijana seperti format Mac.

4. Cache video lokal TIDAK dimasukkan ke backup.
5. Token Railway/FastAPI TIDAK dimasukkan ke backup.
6. BLOB evidens/PDF dan metadata video kekal di dalam SQLite.

7. Restore Windows boleh terus membaca ZIP backup Mac tanpa conversion.
8. Backup yang dibina di Windows v0.5.4 menggunakan struktur canonical yang sama untuk dipindah antara platform.

9. Migrasi automatik Windows lama:
   data/spk_windows.sqlite3
   -> data/sistem_pengurusan_kehadiran.sqlite3
   Sidecar -wal/-shm juga dipindah jika ada.

10. Restore lama database/spk.sqlite3 masih diterima sebagai fallback migrasi,
    tetapi SEMUA backup baharu yang dicipta menggunakan standard Mac sahaja.

9. v0.5.4 membaiki sintaks restore C#: elak `entry ??= throw`; kini guna `if (entry is null) throw ...`.
