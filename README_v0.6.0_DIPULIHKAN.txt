SPK WINDOWS NATIVE v0.6.0 — MODUL DIPULIHKAN

Fokus v0.6:
- Menu Dipulihkan kini modul sebenar, bukan lagi preview.
- Senarai semua kes dengan penapis SEMUA / AKTIF / PEMANTAUAN / DIPULIHKAN.
- Ringkasan jumlah kes aktif, pemantauan dan dipulihkan.
- Tandakan kes sebagai DIPULIHKAN dengan tarikh, hasil pemulihan, guru pengesah, tarikh pemantauan dan catatan.
- Kes yang telah selesai boleh dikembalikan ke status PEMANTAUAN tanpa memadam sejarah pemulihan.
- Semua perubahan menggunakan jadual case_profiles canonical Mac yang sedia ada; tiada skema Windows baharu.
- UUID/version/sync_status kekal serasi dengan Railway sync.

Dikekalkan daripada v0.5.7:
- Eviden Video UI gaya Mac + panel kanan accordion.
- Player lokal UniformToFill.
- Backup/Restore standard canonical Mac.
- Database canonical: database/sistem_pengurusan_kehadiran.sqlite3.

Ujian sebelum guna:
1. Clean Solution.
2. Rebuild Solution.
3. Buka Profil Kes dan pastikan ada sekurang-kurangnya satu kes.
4. Buka Dipulihkan, pilih kes dan isi Hasil Pemulihan.
5. Tekan Tandakan Dipulihkan.
6. Pastikan kiraan DIPULIHKAN bertambah dan status menjadi SELESAI.
7. Uji Kembali Pemantauan.
