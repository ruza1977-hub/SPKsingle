SPK Windows Native v0.8.2 - Sync UNIQUE Fix

Pembaikan:
- Pull Railway tidak lagi bergantung kepada UUID sahaja bagi jadual yang mempunyai UNIQUE natural key.
- attendance dipadankan melalui school_id + student_id + tarikh.
- calendar_days dipadankan melalui school_id + tarikh.
- students dipadankan melalui school_id + no_murid.
- Jika UUID server dan lokal berbeza tetapi rekod lokal sudah synced, rekod lokal dikemas kini tanpa INSERT pendua.
- Jika rekod lokal masih pending/conflict, data lokal tidak ditimpa; konflik direkod untuk semakan.
- Safety catch SQLite error 19 UNIQUE constraint supaya satu rekod lama tidak mematikan keseluruhan sync.
- Tiada perubahan pada skema SQLite canonical atau format backup Mac/Windows.
