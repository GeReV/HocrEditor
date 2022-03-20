using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HocrEditor.Services;

[Flags]
internal enum LoadLibraryFlags : uint
{
    None = 0,
    DontResolveDllReferences = 0x00000001,
    LoadIgnoreCodeAuthorizationLevel = 0x00000010,
    LoadLibraryAsDatafile = 0x00000002,
    LoadLibraryAsDatafileExclusive = 0x00000040,
    LoadLibraryAsImageResource = 0x00000020,
    LoadLibrarySearchApplicationDir = 0x00000200,
    LoadLibrarySearchDefaultDirs = 0x00001000,
    LoadLibrarySearchDllLoadDir = 0x00000100,
    LoadLibrarySearchSystem32 = 0x00000800,
    LoadLibrarySearchUserDirs = 0x00000400,
    LoadWithAlteredSearchPath = 0x00000008
}

public enum TesseractOcrEngineMode
{
    OemTesseractOnly,
    OemLstmOnly,
    OemTesseractLstmCombined,
    OemDefault
};

public enum TesseractPageSegMode
{
    PsmOsdOnly,
    PsmAutoOsd,
    PsmAutoOnly,
    PsmAuto,
    PsmSingleColumn,
    PsmSingleBlockVertText,
    PsmSingleBlock,
    PsmSingleLine,
    PsmSingleWord,
    PsmCircleWord,
    PsmSingleChar,
    PsmSparseText,
    PsmSparseTextOsd,
    PsmRawLine,
    PsmCount
};

public static class TesseractDelegates
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

    internal delegate void TessBaseAPISetImage(
        IntPtr handle,
        byte[] data,
        int width,
        int height,
        int bytesPerPixel,
        int bytesPerLine
    );

    internal delegate IntPtr TessBaseAPIGetUTF8Text(IntPtr handle);

    internal delegate IntPtr TessBaseAPIGetHOCRText(IntPtr handle, int pageNumber);

    internal delegate IntPtr TessBaseAPIGetAltoText(IntPtr handle, int pageNumber);

    internal delegate IntPtr TessBaseAPIGetTsvText(IntPtr handle, int pageNumber);

    internal delegate void TessBaseAPISetSourceResolution(IntPtr handle, int ppi);

    internal delegate void TessBaseAPISetRectangle(IntPtr handle, int x, int y, int width, int height);

    internal delegate void TessBaseAPISetInputName(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string name);

    internal delegate void TessBaseAPISetPageSegMode(IntPtr handle, int mode);

    internal delegate bool TessBaseAPISetVariable(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string key,
        [MarshalAs(UnmanagedType.LPStr)] string value
    );

    internal delegate void TessBaseAPIReadConfigFile(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string file);

    internal delegate IntPtr TessBaseAPIGetLoadedLanguagesAsVector(IntPtr handle);

    internal delegate IntPtr TessBaseAPIGetAvailableLanguagesAsVector(IntPtr handle);
    // ReSharper enable InconsistentNaming
}

internal class TesseractDllHandle : SafeHandle
{
    public readonly TesseractDelegates.TessBaseAPICreate TessBaseApiCreate;
    public readonly TesseractDelegates.TessBaseAPIDelete TessBaseApiDelete;
    public readonly TesseractDelegates.TessBaseAPIClear TessBaseAPIClear;
    public readonly TesseractDelegates.TessVersion TessVersion;
    public readonly TesseractDelegates.TessBaseAPIInit1 TessBaseAPIInit;
    public readonly TesseractDelegates.TessBaseAPISetInputName TessBaseAPISetInputName;
    public readonly TesseractDelegates.TessBaseAPISetImage TessBaseAPISetImage;
    public readonly TesseractDelegates.TessBaseAPIGetUTF8Text TessBaseAPIGetUTF8Text;
    public readonly TesseractDelegates.TessBaseAPIGetHOCRText TessBaseAPIGetHOCRText;
    public readonly TesseractDelegates.TessBaseAPIGetAltoText TessBaseAPIGetAltoText;
    public readonly TesseractDelegates.TessBaseAPIGetTsvText TessBaseAPIGetTsvText;
    public readonly TesseractDelegates.TessBaseAPISetSourceResolution TessBaseAPISetSourceResolution;
    public readonly TesseractDelegates.TessBaseAPISetRectangle TessBaseAPISetRectangle;
    public readonly TesseractDelegates.TessBaseAPISetPageSegMode TessBaseAPISetPageSegMode;
    public readonly TesseractDelegates.TessBaseAPISetVariable TessBaseAPISetVariable;
    public readonly TesseractDelegates.TessBaseAPIReadConfigFile TessBaseAPIReadConfigFile;
    public readonly TesseractDelegates.TessBaseAPIGetLoadedLanguagesAsVector TessBaseAPIGetLoadedLanguagesAsVector;

    public readonly TesseractDelegates.TessBaseAPIGetAvailableLanguagesAsVector
        TessBaseAPIGetAvailableLanguagesAsVector;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    public TesseractDllHandle(string dllPath) : base(IntPtr.Zero, true)
    {
        var tesseractHandle = LoadLibraryEx(dllPath, IntPtr.Zero, (uint)LoadLibraryFlags.LoadLibrarySearchDllLoadDir);

        if (tesseractHandle == IntPtr.Zero)
        {
            var errorCode = Marshal.GetLastWin32Error();

            throw new Exception($"Failed to load library (ErrorCode: {errorCode})");
        }

        SetHandle(tesseractHandle);

        TessVersion = GetProc<TesseractDelegates.TessVersion>(nameof(TesseractDelegates.TessVersion));
        TessBaseApiCreate = GetProc<TesseractDelegates.TessBaseAPICreate>(nameof(TesseractDelegates.TessBaseAPICreate));
        TessBaseApiDelete = GetProc<TesseractDelegates.TessBaseAPIDelete>(nameof(TesseractDelegates.TessBaseAPIDelete));
        TessBaseAPIClear = GetProc<TesseractDelegates.TessBaseAPIClear>(nameof(TesseractDelegates.TessBaseAPIClear));
        TessBaseAPIInit = GetProc<TesseractDelegates.TessBaseAPIInit1>(nameof(TesseractDelegates.TessBaseAPIInit1));
        TessBaseAPISetInputName =
            GetProc<TesseractDelegates.TessBaseAPISetInputName>(nameof(TesseractDelegates.TessBaseAPISetInputName));
        TessBaseAPISetImage =
            GetProc<TesseractDelegates.TessBaseAPISetImage>(nameof(TesseractDelegates.TessBaseAPISetImage));
        TessBaseAPIGetUTF8Text =
            GetProc<TesseractDelegates.TessBaseAPIGetUTF8Text>(nameof(TesseractDelegates.TessBaseAPIGetUTF8Text));
        TessBaseAPIGetHOCRText =
            GetProc<TesseractDelegates.TessBaseAPIGetHOCRText>(nameof(TesseractDelegates.TessBaseAPIGetHOCRText));
        TessBaseAPIGetAltoText =
            GetProc<TesseractDelegates.TessBaseAPIGetAltoText>(nameof(TesseractDelegates.TessBaseAPIGetAltoText));
        TessBaseAPIGetTsvText =
            GetProc<TesseractDelegates.TessBaseAPIGetTsvText>(nameof(TesseractDelegates.TessBaseAPIGetTsvText));
        TessBaseAPISetSourceResolution =
            GetProc<TesseractDelegates.TessBaseAPISetSourceResolution>(
                nameof(TesseractDelegates.TessBaseAPISetSourceResolution)
            );
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
    }

    private TDelegate GetProc<TDelegate>(string name) =>
        Marshal.GetDelegateForFunctionPointer<TDelegate>(GetProcAddress(handle, name));

    protected override bool ReleaseHandle() => FreeLibrary(handle);

    public override bool IsInvalid => handle == IntPtr.Zero;
}

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

public sealed class TesseractApi : IDisposable
{
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

    public void Init(string language, TesseractOcrEngineMode oem = TesseractOcrEngineMode.OemDefault) =>
        dllHandle.TessBaseAPIInit(apiHandle.DangerousGetHandle(), dataPath, language, (int)oem, IntPtr.Zero, 0);

    public void SetInputName(string name) =>
        dllHandle.TessBaseAPISetInputName(apiHandle.DangerousGetHandle(), name);

    public void SetImage(byte[] data, int width, int height, int bytesPerPixel, int bytesPerLine) =>
        dllHandle.TessBaseAPISetImage(apiHandle.DangerousGetHandle(), data, width, height, bytesPerPixel, bytesPerLine);

    public string Version() =>
        Marshal.PtrToStringUTF8(dllHandle.TessVersion()) ?? ThrowEmptyString();

    public string GetUTF8Text() =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetUTF8Text(apiHandle.DangerousGetHandle())) ?? ThrowEmptyString();

    public string GetHOCRText(int pageNumber = 0) =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetHOCRText(apiHandle.DangerousGetHandle(), pageNumber)) ?? ThrowEmptyString();

    public string GetAltoText(int pageNumber = 0) =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetAltoText(apiHandle.DangerousGetHandle(), pageNumber)) ?? ThrowEmptyString();

    public string GetTSVText(int pageNumber = 0) =>
        Marshal.PtrToStringUTF8(dllHandle.TessBaseAPIGetTsvText(apiHandle.DangerousGetHandle(), pageNumber)) ?? ThrowEmptyString();

    public void SetSourceResolution(int ppi) => dllHandle.TessBaseAPISetSourceResolution(apiHandle.DangerousGetHandle(), ppi);

    public void SetRectangle(int x, int y, int width, int height) =>
        dllHandle.TessBaseAPISetRectangle(apiHandle.DangerousGetHandle(), x, y, width, height);

    public void SetPageSegMode(TesseractPageSegMode mode) => dllHandle.TessBaseAPISetPageSegMode(apiHandle.DangerousGetHandle(), (int)mode);
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
        apiHandle.Dispose();
        dllHandle.Dispose();
    }
}

public static class Tesseract
{
    public static TesseractApi CreateApi(string tesseractPath)
    {
        var dllPath = Directory.GetFiles(tesseractPath, "libtess*.dll").First();

        if (dllPath == null)
        {
            throw new Exception($"Could not find Tesseract DLL in {tesseractPath}.");
        }

        var dataPath = Path.Join(tesseractPath, "tessdata");

        return new TesseractApi(dllPath, dataPath);
    }
}
