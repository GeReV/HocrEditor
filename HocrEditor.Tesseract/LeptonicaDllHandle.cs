namespace HocrEditor.Tesseract;

internal class LeptonicaDllHandle : SafeDllHandle
{
    // ReSharper disable InconsistentNaming
    public readonly LeptonicaDelegates.pixDestroy PixDestroy;
    // ReSharper enable InconsistentNaming

    public LeptonicaDllHandle(string dllPath) : base(dllPath)
    {
        PixDestroy = GetProc<LeptonicaDelegates.pixDestroy>(nameof(LeptonicaDelegates.pixDestroy));
    }

    internal static class LeptonicaDelegates
    {
        internal delegate void pixDestroy(IntPtr pix);
    }
}
