SISTEM PENGURUSAN KEHADIRAN — WINDOWS NATIVE v0.3.0
====================================================

Asas: C# + WinUI 3 + SQLite local-first.
UI CrispUI v0.2.1 dikekalkan.

MODUL KES YANG DIAKTIFKAN
1. Profil Kes
   - Murid, tarikh buka, status, kategori, guru bertanggungjawab
   - Ringkasan dan catatan
   - Carian, kemas kini, soft delete

2. Hubungan Penjaga
   - Telefon / WhatsApp / SMS / Bersemuka / Surat / Lain-lain
   - Nama penjaga + telefon auto-refresh apabila murid ditukar
   - Hasil/respons dan catatan

3. Lawatan Rumah
   - Murid, tarikh, guru
   - Alamat auto-isi daripada profil murid untuk rekod baharu
   - Dapatan, tindakan susulan, catatan

4. Kaunseling
   - Individu / Kelompok / Ibu Bapa-Penjaga / Lain-lain
   - Guru/kaunselor, rumusan, tindakan seterusnya, catatan

5. Intervensi
   - Jenis intervensi, tindakan/strategi, guru, tarikh sasaran
   - Status: DIRANCANG / DALAM TINDAKAN / SELESAI / TIDAK BERJAYA
   - Hasil dan catatan

SEMUA REKOD
- UUID
- school_id
- created_at / updated_at / deleted_at
- version
- sync_status='pending'
- serasi dengan Railway Sync v0.7.1 sedia ada

UJIAN PERTAMA WINDOWS
1. Extract ke C:\SPK030
2. Buka SPK_Windows.sln
3. Debug | x64
4. Build > Rebuild Solution
5. F5

Uji urutan:
Murid -> Guru -> Profil Kes -> Hubungan Penjaga -> Lawatan Rumah -> Kaunseling -> Intervensi.
