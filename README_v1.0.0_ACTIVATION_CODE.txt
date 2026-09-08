SPK Windows Native v1.0.0 — Activation Code + Dashboard
========================================================

TUJUAN
------
Versi Windows rasmi dengan sistem pengaktifan peranti seperti SPK Android.
Pengguna biasa tidak perlu import Server Config ZIP untuk pemasangan baru.

ALIRAN PERTAMA KALI
-------------------
1. Pasang / buka SPK Windows.
2. Jika PC belum diaktifkan, aplikasi terus memaparkan skrin Sambungkan SPK Desktop.
3. Masukkan activation code sekali guna yang dijana oleh SPK Mac Admin.
4. Windows menghantar activation_code + device_id + platform=windows + nama PC ke Railway.
5. Railway mengikat peranti kepada school_id dan teacher_id.
6. Railway mengeluarkan device_token unik untuk PC tersebut.
7. device_token disimpan secara terenkripsi menggunakan Windows DPAPI CurrentUser.
8. Kod pengaktifan tidak disimpan selepas pengaktifan.
9. Initial sync dijalankan dan aplikasi masuk ke Dashboard.

PENGGUNAAN SETERUSNYA
---------------------
- Aplikasi membaca credential peranti secara automatik.
- Terus masuk Dashboard tanpa login dan tanpa memasukkan kod semula.
- Auto Sync boleh berjalan menggunakan device token sendiri.

MULTI-DEVICE
------------
- Satu guru boleh mempunyai Android + Windows + Mac.
- Setiap peranti mesti menggunakan activation code baru.
- Setiap peranti mendapat device_token yang berbeza.
- Kod yang telah digunakan pada telefon tidak sah untuk PC.

KESELAMATAN
-----------
- Server production: https://fail-kes-ponteng-production.up.railway.app
- Device token tidak ditulis plaintext ke server.json.
- Token dilindungi menggunakan Windows DPAPI (CurrentUser).
- Activation code ialah sekali guna dan tempoh sah ditentukan oleh server.
- school_id aplikasi diikat selepas provisioning berjaya.

ASAS YANG DIKEKALKAN
--------------------
- SQLite local-first.
- SyncUniqueFix daripada Windows v0.8.2.
- Railway sync incremental.
- Dashboard desktop.
- Modul Kehadiran, Kes, Hubungan Penjaga, Lawatan Rumah, Intervensi, Eviden, Backup/Restore.
- Legacy Server Config masih disimpan dalam kod untuk migrasi/recovery, tetapi bukan aliran pengguna biasa.

BUILD
-----
Buka SPK_Windows.sln menggunakan Visual Studio 2022/2026 di Windows.
Build configuration: x64 atau ARM64 mengikut sasaran.

