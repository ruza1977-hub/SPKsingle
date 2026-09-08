SPK Windows Native v0.9.0 — Device Provisioning + Dashboard

Asas: v0.8.2 SyncUniqueFix.

BARU
- First-run activation code seperti Telegram Desktop.
- Server production SPK terbina dalam; pengguna biasa tidak perlu import Server Config ZIP.
- Device token unik disimpan dengan Windows DPAPI (CurrentUser).
- school_id diikat daripada provisioning server tanpa reset PostgreSQL.
- ID Guru disimpan sebagai metadata lokal.
- Dashboard dipertingkat: identiti guru/sekolah, status sync, sync terakhir, device info, tindakan pantas.
- Menu Sambungkan Peranti disediakan untuk provisioning/re-provisioning.
- Legacy Server Config ZIP dan token lama masih disokong untuk migrasi.
- SyncUniqueFix v0.8.2 dikekalkan.

ALIRAN
Install -> masukkan activation code sekali -> device credential -> initial sync -> Dashboard.
