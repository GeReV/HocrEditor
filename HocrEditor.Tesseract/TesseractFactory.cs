namespace HocrEditor.Tesseract;

internal static class TesseractFactory
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
