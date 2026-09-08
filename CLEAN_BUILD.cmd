@echo off
cd /d "%~dp0"
echo Membersihkan bin/obj...
if exist SPK\bin rmdir /s /q SPK\bin
if exist SPK\obj rmdir /s /q SPK\obj
echo Selesai. Buka SPK_Windows.sln, Restore NuGet Packages, kemudian Rebuild Solution.
pause
