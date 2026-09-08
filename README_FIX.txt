SPK Windows v1.1.1 GitHub Fix

Punca build gagal: beberapa fail source dalam SPK/Pages tidak masuk ke repo GitHub.
Fail yang perlu ditambah semula:
- SettingsPage.xaml
- ModulePreviewPage.xaml
- ModulePreviewPage.xaml.cs
- CounsellingPage.xaml
- EvidencePage.xaml.cs
- TeachersPage.xaml.cs

Selain itu, csproj ditambah IncludeAllContentForSelfExtract=true untuk WinUI 3 single-file.
Workflow baharu juga membuat semakan fail wajib sebelum restore/publish.
