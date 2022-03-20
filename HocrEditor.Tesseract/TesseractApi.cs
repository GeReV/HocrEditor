using System.Runtime.InteropServices;

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

    public void SetInputName(string name) =>
        dllHandle.TessBaseAPISetInputName(apiHandle.DangerousGetHandle(), name);

    public void SetImage(byte[] data, int width, int height, int bytesPerPixel, int bytesPerLine) =>
        dllHandle.TessBaseAPISetImage(apiHandle.DangerousGetHandle(), data, width, height, bytesPerPixel, bytesPerLine);

    public string Version() =>
        Marshal.PtrToStringUTF8(dllHandle.TessVersion()) ?? ThrowEmptyString();

    public string GetUtf8Text() =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetUTF8Text(apiHandle.DangerousGetHandle())) ?? ThrowEmptyString();

    public string GetHocrText(int pageNumber = 0) =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetHOCRText(apiHandle.DangerousGetHandle(), pageNumber)) ?? ThrowEmptyString();

    public string GetAltoText(int pageNumber = 0) =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetAltoText(apiHandle.DangerousGetHandle(), pageNumber)) ?? ThrowEmptyString();

    public string GetTsvText(int pageNumber = 0) =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetTsvText(apiHandle.DangerousGetHandle(), pageNumber)) ?? ThrowEmptyString();

    public void SetSourceResolution(int ppi) => dllHandle.TessBaseAPISetSourceResolution(apiHandle.DangerousGetHandle(), ppi);

    public void SetRectangle(int x, int y, int width, int height) =>
        dllHandle.TessBaseAPISetRectangle(apiHandle.DangerousGetHandle(), x, y, width, height);

    public void SetPageSegMode(PageSegmentationMode mode) => dllHandle.TessBaseAPISetPageSegMode(apiHandle.DangerousGetHandle(), (int)mode);
    public bool SetVariable(string key, string value) => dllHandle.TessBaseAPISetVariable(apiHandle.DangerousGetHandle(), key, value);
    public void ReadConfigFile(string file) => dllHandle.TessBaseAPIReadConfigFile(apiHandle.DangerousGetHandle(), file);

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

    private static string[] GetStringVector(IntPtr ptr)
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

    private static string ThrowEmptyString() => throw new InvalidOperationException("Unexpected null string");

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
