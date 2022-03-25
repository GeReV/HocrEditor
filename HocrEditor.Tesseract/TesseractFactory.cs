namespace HocrEditor.Tesseract;

internal static class TesseractFactory
{
    public static TesseractApi CreateApi(string tesseractPath)
    {
        var tesseractDllPath = Directory.GetFiles(tesseractPath, "libtess*.dll").First();

        if (tesseractDllPath == null)
        {
            throw new Exception($"Could not find Tesseract DLL in {tesseractPath}.");
        }

        var leptonicaDllPath = Directory.GetFiles(tesseractPath, "liblept*.dll").First();

        if (leptonicaDllPath == null)
        {
            throw new Exception($"Could not find Leptonica DLL in {tesseractPath}.");
        }

        var dataPath = Path.Join(tesseractPath, "tessdata");

        return new TesseractApi(tesseractDllPath, leptonicaDllPath, dataPath);
    }
}
