using System.Runtime.InteropServices;

namespace HocrEditor.Tesseract;

public class SafeDllHandle : SafeHandle
{
    protected SafeDllHandle(string dllPath) : base(IntPtr.Zero, true)
    {
        var handlePtr = NativeHelpers.LoadLibraryEx(dllPath, IntPtr.Zero, (uint)LoadLibraryFlags.LoadLibrarySearchDllLoadDir);

        if (handlePtr == IntPtr.Zero)
        {
            var errorCode = Marshal.GetLastWin32Error();

            throw new Exception($"Failed to load library (ErrorCode: {errorCode})");
        }

        SetHandle(handlePtr);
    }

    protected TDelegate GetProc<TDelegate>(string name) =>
        Marshal.GetDelegateForFunctionPointer<TDelegate>(NativeHelpers.GetProcAddress(handle, name));

    protected override bool ReleaseHandle() => NativeHelpers.FreeLibrary(handle);

    public override bool IsInvalid => handle == IntPtr.Zero;
}
