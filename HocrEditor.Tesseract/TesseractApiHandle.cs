using System.Runtime.InteropServices;

namespace HocrEditor.Tesseract;

public sealed class TesseractApiHandle : SafeHandle
{
    private readonly TesseractDllHandle dllHandle;

    public static TesseractApiHandle Create(string dllPath) =>
        new(new TesseractDllHandle(dllPath));

    internal TesseractApiHandle(TesseractDllHandle dllHandle) : base(IntPtr.Zero, true)
    {
        this.dllHandle = dllHandle;

        handle = dllHandle.TessBaseApiCreate();
    }

    protected override bool ReleaseHandle()
    {
        dllHandle.TessBaseApiDelete(handle);

        SetHandleAsInvalid();

        return true;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;
}