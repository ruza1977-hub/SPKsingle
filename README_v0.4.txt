Sistem Pengurusan Kehadiran Windows Native v0.4.0
==================================================

Baharu:
- Surat Amaran aktif sepenuhnya.
- AMARAN 1 / AMARAN 2 / AMARAN 3 / NOTIS KHAS.
- Nama/alamat penjaga diambil daripada Murid.
- Jumlah ketidakhadiran dikira automatik daripada attendance sehingga tarikh surat.
- Jana PDF A4 tanpa dependency PDF pihak ketiga.
- PDF disimpan sebagai BLOB dalam SQLite warning_letters.
- Pratonton menggunakan WebView2/Edge PDF viewer.
- Eksport PDF melalui FileSavePicker.
- Buka/Cetak melalui PDF viewer lalai Windows.
- Soft delete, version, sync_status kekal serasi Railway.
- Backup SQLite sedia ada membawa PDF BLOB sekali.

Ujian disyorkan:
1. Rebuild Solution.
2. F5.
3. Buka Surat Amaran.
4. Pilih murid dan semak Nama/Alamat Penjaga + Jumlah Ketidakhadiran.
5. Jana & Simpan PDF.
6. Pilih surat dalam senarai dan semak preview.
7. Eksport PDF dan Buka/Cetak.
