using System.Runtime.InteropServices;
using SkiaSharp;

namespace HocrEditor.Tesseract;

public sealed class TesseractApi : IDisposable
{
    private bool isDisposed;

    private readonly string dataPath;
    private readonly TesseractDllHandle dllHandle;
    private readonly TesseractApiHandle apiHandle;

    internal TesseractApi(string dllPath, string dataPath)
    {
        this.dataPath = dataPath;

        dllHandle = new TesseractDllHandle(dllPath);
        apiHandle = new TesseractApiHandle(dllHandle);
    }

    public void Clear() => dllHandle.TessBaseAPIClear(apiHandle.DangerousGetHandle());

    public void Init(string language, OcrEngineMode oem = OcrEngineMode.Default) =>
        dllHandle.TessBaseAPIInit(apiHandle.DangerousGetHandle(), dataPath, language, (int)oem, IntPtr.Zero, 0);


    public string Version() => GetText(dllHandle.TessVersion());


    public int GetThresholdedImageScaleFactor() =>
        dllHandle.TessBaseAPIGetThresholdedImageScaleFactor(apiHandle.DangerousGetHandle());

    public SKImage GetThresholdedImage()
    {
        var pixPtr = IntPtr.Zero;
        try
        {
            pixPtr = dllHandle.TessBaseAPIGetThresholdedImage(apiHandle.DangerousGetHandle());

            var pix = Marshal.PtrToStructure<Pix>(pixPtr);

            // Each row is encoded into 32-bits, so get round up to the nearest multiple of 32.
            var width = (int)Math.Ceiling(pix.w / 32.0f) * 32;

            var info = new SKImageInfo(width, (int)pix.h, SKColorType.Gray8);

            var words = info.Width * info.Height / 32;
            var pixels = new byte[info.Width * info.Height];

            for (var i = 0; i < words; i++)
            {
                var pixel = Marshal.ReadInt32(pix.data, i * sizeof(int));

                for (var bit = 0; bit < 32; bit++)
                {
                    var index = i * 32 + bit;

                    if (index >= pixels.Length)
                    {
                        break;
                    }

                    pixels[index] = (byte)((pixel & 0x80000000) == 0 ? 0xff : 0x0);
                    pixel <<= 1;
                }
            }

            return SKImage.FromPixelCopy(info, pixels);
        }
        finally
        {
            if (pixPtr != IntPtr.Zero)
            {
                // TODO: pixDestroy(pix);
            }
        }
    }

    public string GetUtf8Text() =>
        GetText(dllHandle.TessBaseAPIGetUTF8Text(apiHandle.DangerousGetHandle()));

    public string GetHocrText(int pageNumber = 0) =>
        GetText(dllHandle.TessBaseAPIGetHOCRText(apiHandle.DangerousGetHandle(), pageNumber));

    public string GetAltoText(int pageNumber = 0) =>
        GetText(dllHandle.TessBaseAPIGetAltoText(apiHandle.DangerousGetHandle(), pageNumber));

    public string GetTsvText(int pageNumber = 0) =>
        GetText(dllHandle.TessBaseAPIGetTsvText(apiHandle.DangerousGetHandle(), pageNumber));

    public void SetInputName(string name) =>
        dllHandle.TessBaseAPISetInputName(apiHandle.DangerousGetHandle(), name);

    public void SetImage(byte[] data, int width, int height, int bytesPerPixel, int bytesPerLine) =>
        dllHandle.TessBaseAPISetImage(apiHandle.DangerousGetHandle(), data, width, height, bytesPerPixel, bytesPerLine);

    public void SetSourceResolution(int ppi) =>
        dllHandle.TessBaseAPISetSourceResolution(apiHandle.DangerousGetHandle(), ppi);

    public void SetRectangle(int x, int y, int width, int height) =>
        dllHandle.TessBaseAPISetRectangle(apiHandle.DangerousGetHandle(), x, y, width, height);

    public void SetPageSegMode(PageSegmentationMode mode) =>
        dllHandle.TessBaseAPISetPageSegMode(apiHandle.DangerousGetHandle(), (int)mode);

    public bool SetVariable(string key, string value) =>
        dllHandle.TessBaseAPISetVariable(apiHandle.DangerousGetHandle(), key, value);

    public void ReadConfigFile(string file) =>
        dllHandle.TessBaseAPIReadConfigFile(apiHandle.DangerousGetHandle(), file);

    public string[] GetLoadedLanguages()
    {
        try
        {
            Init(string.Empty);

            return GetStringVector(dllHandle.TessBaseAPIGetLoadedLanguagesAsVector(apiHandle.DangerousGetHandle()));
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

            return GetStringVector(dllHandle.TessBaseAPIGetAvailableLanguagesAsVector(apiHandle.DangerousGetHandle()));
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

    private void DeleteText(IntPtr text) => dllHandle.TessDeleteText(text);

    private void DeleteTextArray(IntPtr textArray) => dllHandle.TessDeleteTextArray(textArray);

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        apiHandle.Dispose();
        dllHandle.Dispose();

        isDisposed = true;
    }
}
