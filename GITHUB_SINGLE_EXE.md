# SPK Windows Native v1.1.1 — GitHub Single EXE

Versi asas: `SPK_Windows_Native_v1.1.1_ProvisioningFix`.

## Build di GitHub
1. Upload semua kandungan folder ini ke repository GitHub.
2. Pastikan branch utama bernama `main`.
3. Buka **Actions**.
4. Pilih **Build SPK Windows v1.1.1 Single EXE**.
5. Klik **Run workflow**.
6. Muat turun artifact `SPK-Windows-v1.1.1-SingleEXE-x64`.

Fail utama: `SPK_Windows_v1.1.1_x64.exe`.

## Nota WinUI 3
Aplikasi menggunakan Windows App SDK 1.8. `PublishSingleFile` membungkus assembly .NET,
manakala native library boleh diekstrak secara automatik semasa aplikasi bermula.
Folder `Assets` atau `Config` hanya disertakan dalam artifact jika publish masih memerlukannya.

Database pengguna, backup dan token sekolah tidak dibundle ke dalam EXE.
