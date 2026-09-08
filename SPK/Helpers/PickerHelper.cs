using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SistemPengurusanKehadiran.Helpers;

public static class PickerHelper
{
    public static FileOpenPicker CreateOpenPicker(Window window)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        return picker;
    }

    public static FileSavePicker CreateSavePicker(Window window)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        return picker;
    }
}
