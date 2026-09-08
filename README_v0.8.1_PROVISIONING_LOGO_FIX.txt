SPK WINDOWS NATIVE v0.8.1

PEMBAIKAN
1. First install/SQLite kosong kini dianggap UNBOUND.
2. Import Server Config kali pertama akan mengikat school_id SQLite kepada school_id server.
3. Selepas terikat, Server Config sekolah lain tetap ditolak.
4. device_id unik dijana untuk setiap komputer.
5. Logo sekolah dibaca dari AppContext.BaseDirectory/Assets/SchoolLogo.png, bukan path komputer pembangun.
6. Assets dipaksa disalin ke folder Publish (CopyToPublishDirectory).
7. Dashboard dan bar atas memaparkan logo sekolah.

ALIRAN PC GURU BARU
Install/publish -> SQLite lokal kosong -> Import Server Config -> bind school_id -> Auto Sync -> pull data sekolah.

KESELAMATAN TENANT
Database yang sudah mempunyai data/identiti sekolah dan dianggap BOUND tidak akan ditukar secara automatik kepada school_id lain.
