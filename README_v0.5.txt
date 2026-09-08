FAIL KES PONTENG DIGITAL — WINDOWS NATIVE v0.5.0
================================================

Fokus v0.5.0: Evidens gambar/PDF dan Eviden Video.

BARU
----
1. Modul Evidens
   - Simpan gambar dan PDF terus sebagai BLOB dalam SQLite local-first.
   - Pautkan evidens kepada murid, tarikh, jenis, tajuk dan catatan.
   - Carian rekod evidens.
   - Pratonton gambar dalam aplikasi.
   - Pratonton PDF melalui WebView2 terbina dalam aplikasi.
   - Eksport semula fail asal daripada SQLite.
   - Soft delete + sync_status pending untuk Railway sync.

2. Modul Eviden Video
   - Sokong pautan YouTube, YouTube Shorts dan Google Drive.
   - Kenal pasti provider dan source ID secara automatik.
   - Pratonton video di dalam aplikasi melalui WebView2.
   - YouTube Shorts dipaparkan dalam susun atur menegak yang lebih sesuai.
   - Metadata video disimpan dalam SQLite; video atas talian tidak dimasukkan sebagai BLOB.
   - Pilihan Cache Video Lokal: pengguna boleh pilih salinan MP4/MOV/M4V/WEBM untuk cache pada komputer.
   - Cache lokal disimpan di LocalAppData/video_cache dan tidak membesarkan SQLite.
   - AutoSave selepas rekod video disimpan kali pertama.
   - Carian rekod video.
   - Soft delete + sync_status pending.

3. Navigasi
   - Menu Evidens dan Eviden Video kini membuka modul sebenar, bukan halaman placeholder.
   - Tajuk aplikasi ditukar kepada Fail Kes Ponteng Digital.

KEKAL
-----
- SQLite local-first.
- UUID, timestamp, versioning, soft delete dan sync_status.
- SyncTables sudah merangkumi evidence_files dan video_evidence.
- Import ZIP Fail Ponteng Import Package v1.
- Surat Amaran PDF v0.4.

BUILD WINDOWS
-------------
1. Buka SPK_Windows.sln dalam Visual Studio.
2. Build > Clean Solution.
3. Build > Rebuild Solution.
4. Jalankan projek x64.

Nota: Projek menggunakan Microsoft.WindowsAppSDK 1.8.260710003 dan .NET 8.
