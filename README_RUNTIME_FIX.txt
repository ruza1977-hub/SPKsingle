SPK Windows v1.1.1 - Runtime Fix

Perubahan:
1. IncludeAllContentForSelfExtract=true untuk WinUI 3 single-file.
2. WindowsAppSdkUndockedRegFreeWinRTInitialize=true secara eksplisit.
3. EXE tidak lagi dinamakan semula selepas publish.
4. Startup log: %LOCALAPPDATA%\SistemPengurusanKehadiran\logs\startup.log
5. GitHub Actions menjalankan smoke test 10 saat. Jika EXE terus mati, build dianggap gagal dan startup.log dicetak pada Actions.

Cara guna:
- Upload/overwrite folder SPK dan .github ke repository.
- Actions > Build SPK Windows v1.1.1 Runtime Fix > Run workflow.
- Hanya gunakan artifact jika Smoke test EXE berwarna hijau.
