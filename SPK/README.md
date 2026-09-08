# Sistem Pengurusan Kehadiran — Windows Native v0.1.0

Port rasmi daripada **macOS v1.0.2 final** yang dibekalkan pengguna.

## Teknologi
- C# / .NET 8
- WinUI 3 / Windows App SDK
- Microsoft.Data.Sqlite
- SQLite local-first
- FastAPI + PostgreSQL Railway sync contract v1 yang sama seperti macOS
- Unpackaged + self-contained Windows App SDK untuk memudahkan edaran folder/installer kemudian

## Modul aktif v0.1
- Dashboard
- Import ZIP Data (Fail Ponteng Import Package v1)
- Murid
- Guru
- Takwim
- Kehadiran (holiday guard + auto HADIR)
- Laporan & Analitik asas
- Sync Railway (push/pull v1)
- Backup / Restore ZIP
- Server Config ZIP
- Tetapan sekolah

## Struktur sudah tersedia sejak v0.1
SQLite menggunakan skema final untuk Profil Kes, Hubungan Penjaga, Lawatan Rumah, Kaunseling, Intervensi, Surat Amaran, Evidens, Eviden Video dan Dipulihkan. Sehingga v0.6.0, modul Dipulihkan juga telah dipindahkan ke UI Windows tanpa menukar skema canonical Mac atau server Railway.

## Buka di Windows
1. Pasang Visual Studio 2022/2026 dengan workload **Windows application development** dan .NET desktop.
2. Buka `SistemPengurusanKehadiran.csproj`.
3. Restore NuGet.
4. Pilih `x64` atau `ARM64`.
5. Tekan **F5**.

## Data
SQLite Windows disimpan di:
`%LOCALAPPDATA%\\SistemPengurusanKehadiran\\data\\sistem_pengurusan_kehadiran.sqlite3`

## Keserasian Railway
Server yang telah digunakan oleh macOS boleh digunakan semula. Import Server Config ZIP yang sama **hanya jika Windows ini untuk sekolah/tenant yang sama**.

## Nota UI
Tema mengikut identiti final macOS: navy + biru + mint, logo SK Sungai Pergam, kad putih rounded, butang ceria dan NavigationView Windows 11.

## v0.1.1 DebugFix
Untuk debug unpackaged WinUI 3, projek kini menggunakan `Properties/launchSettings.json` dengan `commandName=Project`. Platform x64 dipadankan kepada `win-x64` dan output RID tidak ditambah ke path debug, supaya Visual Studio menemui `SistemPengurusanKehadiran.exe` pada folder `bin\\x64\\Debug\\<TFM>`. Jika projek pernah dibuka daripada ZIP/lokasi lama, extract ke folder baharu dan Rebuild Solution.
