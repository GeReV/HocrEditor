using System;
using System.Windows;
using System.Windows.Forms;

namespace HocrEditor.Helpers;

public static class Win32WindowExtensions
{
    public static IWin32Window GetIWin32Window(this Window? window) => new OldWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle);

    private class OldWindow : IWin32Window
    {
        private readonly IntPtr handle;

        public OldWindow(IntPtr handle)
        {
            this.handle = handle;
        }

        #region IWin32Window Members

        IntPtr IWin32Window.Handle => handle;

        #endregion
    }
}
