SISTEM PENGURUSAN KEHADIRAN WINDOWS NATIVE v0.8.0
LAPORAN & ANALITIK — UI MAC MATCH
============================================================

ASAS VERSI
- Dibina di atas v0.7.2 Auto Server Config + Sync Railway.
- Semua fungsi sedia ada dikekalkan.
- Standard database/backup Mac kekal canonical.

PERUBAHAN MODUL LAPORAN & ANALITIK
1. UI disusun semula berdasarkan rujukan macOS:
   - header ikon + tajuk + subtajuk;
   - penapis Tahun / Tempoh / Kelas;
   - Segar Semula / Eksport CSV / Eksport PDF;
   - 5 kad KPI;
   - Trend Kehadiran;
   - Prestasi Mengikut Kelas;
   - Murid Berisiko.

2. KPI hidup daripada SQLite:
   - Kadar Hadir = HADIR + LEWAT / jumlah rekod;
   - Tidak Hadir = TIDAK HADIR + PONTENG + MC + CUTI BERSEBAB;
   - Ponteng = status PONTENG;
   - Kes Aktif = AKTIF + PEMANTAUAN;
   - Dipulihkan = status SELESAI dalam tempoh pilihan.

3. Tempoh:
   - SEPANJANG TAHUN
   - BULAN SEMASA
   - JAN - JUN
   - JUL - DIS

4. Trend Kehadiran:
   - dikumpulkan mengikut bulan;
   - kadar hadir dipaparkan sebagai progress bar.

5. Prestasi Kelas:
   - kelas;
   - tidak hadir;
   - ponteng;
   - peratus hadir.

6. Murid Berisiko:
   - TH;
   - Ponteng;
   - Lewat;
   - % hadir;
   - tahap risiko;
   - bilangan kes aktif/pemantauan.
   - skor risiko = Ponteng x4 + TH x2 + Lewat.

7. Eksport:
   - CSV menyertakan ringkasan, trend, prestasi kelas dan murid berisiko;
   - PDF menyertakan ringkasan, trend, prestasi kelas dan murid berisiko teratas.

CATATAN BUILD
- Persekitaran pembinaan ini tidak mempunyai Windows/.NET SDK untuk compile WinUI sebenar.
- Semua XAML telah diperiksa sebagai XML dan tiada parse error.
- Tiada penggunaan WrapPanel baharu.
- Ujian akhir: Visual Studio Windows > Clean Solution > Rebuild Solution.
