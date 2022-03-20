using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HocrEditor.Tesseract;

public sealed class TesseractService : IDisposable
{
    private bool isDisposed;
    private readonly object lck = new();
    private readonly TesseractApi tesseractApi;

    public TesseractService(string tesseractPath)
    {
        tesseractApi = TesseractFactory.CreateApi(tesseractPath);
    }

    public string GetVersion() => tesseractApi.Version();

    public string[] GetLanguages() => tesseractApi.GetAvailableLanguages();

    public async Task<string> Recognize(string filename, IEnumerable<string> languages, Rectangle region = new())
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(tesseractApi));
        }

        return await Task.Run(
            () =>
            {
                lock (lck)
                {
                    if (isDisposed)
                    {
                        throw new ObjectDisposedException(nameof(tesseractApi));
                    }

                    tesseractApi.Init(string.Join('+', languages));
                    tesseractApi.SetVariable("hocr_font_info", "1");

                    using var image = Image.FromFile(filename);

                    var (bmpData, bytes) = GetBitmapData(image);
                    var bpp = Image.GetPixelFormatSize(bmpData.PixelFormat) / 8;

                    tesseractApi.SetInputName(filename);
                    tesseractApi.SetImage(bytes, bmpData.Width, bmpData.Height, bpp, bmpData.Stride);

                    tesseractApi.SetPageSegMode(PageSegmentationMode.SegmentationOcr);
                    tesseractApi.SetSourceResolution((int)image.HorizontalResolution);

                    if (!region.IsEmpty)
                    {
                        tesseractApi.SetRectangle(region.X, region.Y, region.Width, region.Height);
                    }

                    var text = tesseractApi.GetHocrText();

                    tesseractApi.Clear();

                    return text;
                }
            }
        );
    }

    private static (BitmapData bmpData, byte[] bytes) GetBitmapData(Image image)
    {
        if (image is not Bitmap bmp)
        {
            bmp = new Bitmap(image);
        }

        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly,
            bmp.PixelFormat
        );
        var size = bmpData.Height * bmpData.Stride;
        var bytes = new byte[size];

        Marshal.Copy(bmpData.Scan0, bytes, 0, size);

        bmp.UnlockBits(bmpData);

        if (bmp != image)
        {
            bmp.Dispose();
        }

        return (bmpData, bytes);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        lock (lck)
        {
            tesseractApi.Dispose();
        }

        isDisposed = true;
    }
}
