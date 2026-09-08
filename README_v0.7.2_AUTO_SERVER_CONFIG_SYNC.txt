SISTEM PENGURUSAN KEHADIRAN — WINDOWS NATIVE v0.7.2
AUTO SERVER CONFIG + TERUS SYNC

PERUBAHAN
- Server Config ZIP hanya perlu diimport sekali.
- Selepas config sah tersedia, tidak perlu tekan Uji Server sebelum Sync.
- Import config pada halaman Sync akan terus mengaktifkan Auto Sync dan menjalankan sync pertama.
- Pada launch seterusnya, config/token yang sudah disimpan akan digunakan terus.
- Token kekal disimpan menggunakan Windows DPAPI.
- Jika `server_config.zip` sebenar diletakkan dalam folder `SPK/Config` sebelum build, app boleh bootstrap config secara automatik tanpa klik Import.
- Template server config tidak diterima sebagai config sebenar.
- school_id mesti sama dengan SQLite lokal.
- Pilihan pengguna untuk mematikan Auto Sync dihormati selepas preference pertama ditetapkan.

STANDARD MAC KEKAL
- database/sistem_pengurusan_kehadiran.sqlite3
- backup/restore canonical Mac

ALIRAN YANG DIKEHENDAKI
SERVER CONFIG ZIP -> IMPORT/BOOTSTRAP SEKALI -> SIMPAN ENDPOINT + TOKEN TERLINDUNG -> SYNC TERUS -> LAUNCH SETERUSNYA AUTO GUNA CONFIG SEDIA ADA
