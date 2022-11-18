using System.Runtime.InteropServices;

namespace HocrEditor.Tesseract;

internal class TesseractDllHandle : SafeDllHandle
{
    // ReSharper disable InconsistentNaming
    public readonly TesseractDelegates.TessBaseAPICreate TessBaseApiCreate;
    public readonly TesseractDelegates.TessBaseAPIDelete TessBaseApiDelete;
    public readonly TesseractDelegates.TessBaseAPIClear TessBaseAPIClear;
    public readonly TesseractDelegates.TessVersion TessVersion;
    public readonly TesseractDelegates.TessBaseAPIInit1 TessBaseAPIInit;
    public readonly TesseractDelegates.TessBaseAPIGetThresholdedImage TessBaseAPIGetThresholdedImage;
    public readonly TesseractDelegates.TessBaseAPIGetThresholdedImageScaleFactor TessBaseAPIGetThresholdedImageScaleFactor;
    public readonly TesseractDelegates.TessBaseAPIGetUTF8Text TessBaseAPIGetUTF8Text;
    public readonly TesseractDelegates.TessBaseAPIGetHOCRText TessBaseAPIGetHOCRText;
    public readonly TesseractDelegates.TessBaseAPIGetAltoText TessBaseAPIGetAltoText;
    public readonly TesseractDelegates.TessBaseAPIGetTsvText TessBaseAPIGetTsvText;
    public readonly TesseractDelegates.TessBaseAPIGetIntVariable TessBaseAPIGetIntVariable;
    public readonly TesseractDelegates.TessBaseAPIGetBoolVariable TessBaseAPIGetBoolVariable;
    public readonly TesseractDelegates.TessBaseAPIGetDoubleVariable TessBaseAPIGetDoubleVariable;
    public readonly TesseractDelegates.TessBaseAPIGetStringVariable TessBaseAPIGetStringVariable;
    public readonly TesseractDelegates.TessBaseAPISetInputName TessBaseAPISetInputName;
    public readonly TesseractDelegates.TessBaseAPISetImage TessBaseAPISetImage;
    public readonly TesseractDelegates.TessBaseAPISetSourceResolution TessBaseAPISetSourceResolution;
    public readonly TesseractDelegates.TessBaseAPISetRectangle TessBaseAPISetRectangle;
    public readonly TesseractDelegates.TessBaseAPISetPageSegMode TessBaseAPISetPageSegMode;
    public readonly TesseractDelegates.TessBaseAPISetVariable TessBaseAPISetVariable;
    public readonly TesseractDelegates.TessBaseAPIReadConfigFile TessBaseAPIReadConfigFile;
    public readonly TesseractDelegates.TessBaseAPIGetLoadedLanguagesAsVector TessBaseAPIGetLoadedLanguagesAsVector;
    public readonly TesseractDelegates.TessBaseAPIGetAvailableLanguagesAsVector TessBaseAPIGetAvailableLanguagesAsVector;
    public readonly TesseractDelegates.TessDeleteText TessDeleteText;
    public readonly TesseractDelegates.TessDeleteTextArray TessDeleteTextArray;
    // ReSharper enable InconsistentNaming

    public TesseractDllHandle(string dllPath) : base(dllPath)
    {
        TessVersion = GetProc<TesseractDelegates.TessVersion>(nameof(TesseractDelegates.TessVersion));
        TessBaseApiCreate = GetProc<TesseractDelegates.TessBaseAPICreate>(nameof(TesseractDelegates.TessBaseAPICreate));
        TessBaseApiDelete = GetProc<TesseractDelegates.TessBaseAPIDelete>(nameof(TesseractDelegates.TessBaseAPIDelete));
        TessBaseAPIClear = GetProc<TesseractDelegates.TessBaseAPIClear>(nameof(TesseractDelegates.TessBaseAPIClear));
        TessBaseAPIInit = GetProc<TesseractDelegates.TessBaseAPIInit1>(nameof(TesseractDelegates.TessBaseAPIInit1));
        TessBaseAPIGetThresholdedImage =
            GetProc<TesseractDelegates.TessBaseAPIGetThresholdedImage>(
                nameof(TesseractDelegates.TessBaseAPIGetThresholdedImage)
            );
        TessBaseAPIGetThresholdedImageScaleFactor =
            GetProc<TesseractDelegates.TessBaseAPIGetThresholdedImageScaleFactor>(
                nameof(TesseractDelegates.TessBaseAPIGetThresholdedImageScaleFactor)
            );
        TessBaseAPIGetUTF8Text =
            GetProc<TesseractDelegates.TessBaseAPIGetUTF8Text>(nameof(TesseractDelegates.TessBaseAPIGetUTF8Text));
        TessBaseAPIGetHOCRText =
            GetProc<TesseractDelegates.TessBaseAPIGetHOCRText>(nameof(TesseractDelegates.TessBaseAPIGetHOCRText));
        TessBaseAPIGetAltoText =
            GetProc<TesseractDelegates.TessBaseAPIGetAltoText>(nameof(TesseractDelegates.TessBaseAPIGetAltoText));
        TessBaseAPIGetTsvText =
            GetProc<TesseractDelegates.TessBaseAPIGetTsvText>(nameof(TesseractDelegates.TessBaseAPIGetTsvText));
        TessBaseAPIGetIntVariable =
            GetProc<TesseractDelegates.TessBaseAPIGetIntVariable>(nameof(TesseractDelegates.TessBaseAPIGetIntVariable));
        TessBaseAPIGetBoolVariable =
            GetProc<TesseractDelegates.TessBaseAPIGetBoolVariable>(nameof(TesseractDelegates.TessBaseAPIGetBoolVariable));
        TessBaseAPIGetDoubleVariable =
            GetProc<TesseractDelegates.TessBaseAPIGetDoubleVariable>(nameof(TesseractDelegates.TessBaseAPIGetDoubleVariable));
        TessBaseAPIGetStringVariable =
            GetProc<TesseractDelegates.TessBaseAPIGetStringVariable>(nameof(TesseractDelegates.TessBaseAPIGetStringVariable));
        TessBaseAPISetSourceResolution =
            GetProc<TesseractDelegates.TessBaseAPISetSourceResolution>(
                nameof(TesseractDelegates.TessBaseAPISetSourceResolution)
            );
        TessBaseAPISetInputName =
            GetProc<TesseractDelegates.TessBaseAPISetInputName>(nameof(TesseractDelegates.TessBaseAPISetInputName));
        TessBaseAPISetImage =
            GetProc<TesseractDelegates.TessBaseAPISetImage>(nameof(TesseractDelegates.TessBaseAPISetImage));
        TessBaseAPISetRectangle =
            GetProc<TesseractDelegates.TessBaseAPISetRectangle>(nameof(TesseractDelegates.TessBaseAPISetRectangle));
        TessBaseAPISetPageSegMode =
            GetProc<TesseractDelegates.TessBaseAPISetPageSegMode>(nameof(TesseractDelegates.TessBaseAPISetPageSegMode));
        TessBaseAPISetVariable =
            GetProc<TesseractDelegates.TessBaseAPISetVariable>(nameof(TesseractDelegates.TessBaseAPISetVariable));
        TessBaseAPIReadConfigFile =
            GetProc<TesseractDelegates.TessBaseAPIReadConfigFile>(nameof(TesseractDelegates.TessBaseAPIReadConfigFile));
        TessBaseAPIGetLoadedLanguagesAsVector =
            GetProc<TesseractDelegates.TessBaseAPIGetLoadedLanguagesAsVector>(
                nameof(TesseractDelegates.TessBaseAPIGetLoadedLanguagesAsVector)
            );
        TessBaseAPIGetAvailableLanguagesAsVector =
            GetProc<TesseractDelegates.TessBaseAPIGetAvailableLanguagesAsVector>(
                nameof(TesseractDelegates.TessBaseAPIGetAvailableLanguagesAsVector)
            );
        TessDeleteText = GetProc<TesseractDelegates.TessDeleteText>(nameof(TesseractDelegates.TessDeleteText));
        TessDeleteTextArray = GetProc<TesseractDelegates.TessDeleteTextArray>(nameof(TesseractDelegates.TessDeleteTextArray));
    }

    internal static class TesseractDelegates
    {
        // ReSharper disable InconsistentNaming
        internal delegate IntPtr TessBaseAPICreate();

        internal delegate void TessBaseAPIDelete(IntPtr handle);

        internal delegate void TessBaseAPIClear(IntPtr handle);

        internal delegate IntPtr TessVersion();

        internal delegate int TessBaseAPIInit1(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string dataPath,
            [MarshalAs(UnmanagedType.LPStr)] string language,
            int oem,
            IntPtr configs,
            int configSize
        );

        internal delegate IntPtr TessBaseAPIGetThresholdedImage(IntPtr handle);

        internal delegate int TessBaseAPIGetThresholdedImageScaleFactor(IntPtr handle);

        internal delegate IntPtr TessBaseAPIGetUTF8Text(IntPtr handle);

        internal delegate IntPtr TessBaseAPIGetHOCRText(IntPtr handle, int pageNumber);

        internal delegate IntPtr TessBaseAPIGetAltoText(IntPtr handle, int pageNumber);

        internal delegate IntPtr TessBaseAPIGetTsvText(IntPtr handle, int pageNumber);

        internal delegate void TessBaseAPISetImage(
            IntPtr handle,
            byte[] data,
            int width,
            int height,
            int bytesPerPixel,
            int bytesPerLine
        );

        internal delegate void TessBaseAPISetSourceResolution(IntPtr handle, int ppi);

        internal delegate void TessBaseAPISetRectangle(IntPtr handle, int x, int y, int width, int height);

        internal delegate void TessBaseAPISetInputName(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string name);

        internal delegate void TessBaseAPISetPageSegMode(IntPtr handle, int mode);

        internal delegate bool TessBaseAPISetVariable(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string key,
            [MarshalAs(UnmanagedType.LPStr)] string value
        );

        internal delegate bool TessBaseAPIGetIntVariable(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string key,
            ref int value
        );

        internal delegate bool TessBaseAPIGetBoolVariable(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string key,
            ref bool value
        );

        internal delegate bool TessBaseAPIGetDoubleVariable(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string key,
            ref double value
        );

        internal delegate IntPtr TessBaseAPIGetStringVariable(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string key
        );

        internal delegate void TessBaseAPIReadConfigFile(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string file);

        internal delegate IntPtr TessBaseAPIGetLoadedLanguagesAsVector(IntPtr handle);

        internal delegate IntPtr TessBaseAPIGetAvailableLanguagesAsVector(IntPtr handle);

        internal delegate void TessDeleteText(IntPtr text);

        internal delegate void TessDeleteTextArray(IntPtr textArray);
        // ReSharper enable InconsistentNaming
    }
}
