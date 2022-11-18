namespace HocrEditor.Tesseract;

internal class LeptonicaDllHandle : SafeDllHandle
{
    // ReSharper disable InconsistentNaming
    public readonly LeptonicaDelegates.pixDestroy PixDestroy;
    public readonly LeptonicaDelegates.pixSauvolaBinarizeTiled PixSauvolaBinarizeTiled;
    public readonly LeptonicaDelegates.pixOtsuAdaptiveThreshold PixOtsuAdaptiveThreshold;
    // ReSharper enable InconsistentNaming

    public LeptonicaDllHandle(string dllPath) : base(dllPath)
    {
        PixDestroy = GetProc<LeptonicaDelegates.pixDestroy>(nameof(LeptonicaDelegates.pixDestroy));
        PixSauvolaBinarizeTiled = GetProc<LeptonicaDelegates.pixSauvolaBinarizeTiled>(nameof(LeptonicaDelegates.pixSauvolaBinarizeTiled));
        PixOtsuAdaptiveThreshold = GetProc<LeptonicaDelegates.pixOtsuAdaptiveThreshold>(nameof(LeptonicaDelegates.pixOtsuAdaptiveThreshold));
    }

    internal static class LeptonicaDelegates
    {
        internal delegate void pixDestroy(ref IntPtr pix);

        internal delegate int pixSauvolaBinarizeTiled(IntPtr pix, int windowHalfSize, float factor, int nx, int ny, ref IntPtr pixThresholds, ref IntPtr pixBinary);

        internal delegate int pixOtsuAdaptiveThreshold(IntPtr pix, int sx, int sy, int smoothX, int smoothY, float scoreFraction, ref IntPtr pixThresholds, ref IntPtr pixBinary);
    }
}
