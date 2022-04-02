using System.Runtime.InteropServices;
using SkiaSharp;

namespace HocrEditor.Tesseract;

public sealed class TesseractApi : IDisposable
{
    private bool isDisposed;

    private readonly string dataPath;
    private readonly TesseractDllHandle tesseractDllHandle;
    private readonly LeptonicaDllHandle leptonicaDllHandle;
    private readonly TesseractApiHandle apiHandle;

    internal TesseractApi(string tesseractDllPath, string leptonicaDllPath, string dataPath)
    {
        this.dataPath = dataPath;

        tesseractDllHandle = new TesseractDllHandle(tesseractDllPath);
        leptonicaDllHandle = new LeptonicaDllHandle(leptonicaDllPath);
        apiHandle = new TesseractApiHandle(tesseractDllHandle);
    }

    public void Clear() => tesseractDllHandle.TessBaseAPIClear(apiHandle.DangerousGetHandle());

    public void Init(string language, OcrEngineMode oem = OcrEngineMode.Default) =>
        tesseractDllHandle.TessBaseAPIInit(apiHandle.DangerousGetHandle(), dataPath, language, (int)oem, IntPtr.Zero, 0);


    /// <summary>
    /// Gets Tesseract version string
    /// </summary>
    /// <remarks>This string does not need to be freed as it has a static lifetime.</remarks>
    public string Version() => Marshal.PtrToStringAnsi(tesseractDllHandle.TessVersion()) ?? string.Empty;


    public int GetThresholdedImageScaleFactor() =>
        tesseractDllHandle.TessBaseAPIGetThresholdedImageScaleFactor(apiHandle.DangerousGetHandle());

    public SKBitmap GetThresholdedImage()
    {
        var pixPtr = IntPtr.Zero;
        try
        {
            pixPtr = tesseractDllHandle.TessBaseAPIGetThresholdedImage(apiHandle.DangerousGetHandle());

            var pix = Marshal.PtrToStructure<Pix>(pixPtr);

            // Each row is encoded into 32-bit integers, so get round up to the nearest multiple of 32.
            var width = (int)(pix.w + 31) / 32 * 32;

            var info = new SKImageInfo(width, (int)pix.h, SKColorType.Gray8);

            var bitmap = new SKBitmap(info);

            var words = info.Width * info.Height / 32;

            var pixels = bitmap.GetPixels();

            unsafe
            {
                var ptr = (byte*)pixels.ToPointer();

                for (var i = 0; i < words; i++)
                {
                    var pixel = Marshal.ReadInt32(pix.data, i * sizeof(int));

                    for (var bit = 0; bit < 32; bit++)
                    {
                        var index = i * 32 + bit;

                        if (index >= info.BytesSize)
                        {
                            break;
                        }

                        *ptr = (byte)((pixel & 0x80000000) == 0 ? 0xff : 0x0);
                        ptr++;

                        pixel <<= 1;
                    }
                }
            }

            return bitmap;
        }
        finally
        {
            if (pixPtr != IntPtr.Zero)
            {
                DestroyPix(pixPtr);
            }
        }
    }

    public string GetUtf8Text() =>
        GetText(tesseractDllHandle.TessBaseAPIGetUTF8Text(apiHandle.DangerousGetHandle()));

    public string GetHocrText(int pageNumber = 0) =>
        GetText(tesseractDllHandle.TessBaseAPIGetHOCRText(apiHandle.DangerousGetHandle(), pageNumber));

    public string GetAltoText(int pageNumber = 0) =>
        GetText(tesseractDllHandle.TessBaseAPIGetAltoText(apiHandle.DangerousGetHandle(), pageNumber));

    public string GetTsvText(int pageNumber = 0) =>
        GetText(tesseractDllHandle.TessBaseAPIGetTsvText(apiHandle.DangerousGetHandle(), pageNumber));

    public void SetInputName(string name) =>
        tesseractDllHandle.TessBaseAPISetInputName(apiHandle.DangerousGetHandle(), name);

    public void SetImage(byte[] data, int width, int height, int bytesPerPixel, int bytesPerLine) =>
        tesseractDllHandle.TessBaseAPISetImage(apiHandle.DangerousGetHandle(), data, width, height, bytesPerPixel, bytesPerLine);

    public void SetSourceResolution(int ppi) =>
        tesseractDllHandle.TessBaseAPISetSourceResolution(apiHandle.DangerousGetHandle(), ppi);

    public void SetRectangle(int x, int y, int width, int height) =>
        tesseractDllHandle.TessBaseAPISetRectangle(apiHandle.DangerousGetHandle(), x, y, width, height);

    public void SetPageSegMode(PageSegmentationMode mode) =>
        tesseractDllHandle.TessBaseAPISetPageSegMode(apiHandle.DangerousGetHandle(), (int)mode);

    public bool SetVariable(string key, string value) =>
        tesseractDllHandle.TessBaseAPISetVariable(apiHandle.DangerousGetHandle(), key, value);

    public void ReadConfigFile(string file) =>
        tesseractDllHandle.TessBaseAPIReadConfigFile(apiHandle.DangerousGetHandle(), file);

    public string[] GetLoadedLanguages()
    {
        try
        {
            Init(string.Empty);

            return GetStringVector(tesseractDllHandle.TessBaseAPIGetLoadedLanguagesAsVector(apiHandle.DangerousGetHandle()));
        }
        finally
        {
            Clear();
        }
    }

    public string[] GetAvailableLanguages()
    {
        try
        {
            Init(string.Empty);

            return GetStringVector(tesseractDllHandle.TessBaseAPIGetAvailableLanguagesAsVector(apiHandle.DangerousGetHandle()));
        }
        finally
        {
            Clear();
        }
    }

    private static int GetVectorLength(IntPtr ptr)
    {
        var count = 0;

        while (Marshal.ReadIntPtr(ptr) != IntPtr.Zero)
        {
            count++;

            ptr += IntPtr.Size;
        }

        return count;
    }

    private string[] GetStringVector(IntPtr ptr)
    {
        try
        {
            var count = GetVectorLength(ptr);

            var result = new string[count];

            for (var i = 0; i < count; i++)
            {
                var str = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(ptr, i * IntPtr.Size)) ?? ThrowEmptyString();

                result[i] = str;
            }

            return result;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                DeleteTextArray(ptr);
            }
        }
    }

    private string GetText(IntPtr text)
    {
        try
        {
            return Marshal.PtrToStringUTF8(text) ?? ThrowEmptyString();
        }
        finally
        {
            if (text != IntPtr.Zero)
            {
                DeleteText(text);
            }
        }
    }

    private static string ThrowEmptyString() => throw new InvalidOperationException("Unexpected null string");

    private void DestroyPix(IntPtr pix) => leptonicaDllHandle.PixDestroy(ref pix);

    private void DeleteText(IntPtr text) => tesseractDllHandle.TessDeleteText(text);

    private void DeleteTextArray(IntPtr textArray) => tesseractDllHandle.TessDeleteTextArray(textArray);

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        apiHandle.Dispose();
        tesseractDllHandle.Dispose();

        isDisposed = true;
    }
}
