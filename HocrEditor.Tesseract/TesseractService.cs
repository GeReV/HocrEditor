using System.Drawing;
using SkiaSharp;

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

    public SKBitmap GetThresholdedImage(SKBitmap image)
    {
        tesseractApi.Init(string.Empty);

        var bytes = GetBitmapBytes(image);

        tesseractApi.SetImage(bytes, image.Width, image.Height, image.BytesPerPixel, image.RowBytes);

        var thresholdedImage = tesseractApi.GetThresholdedImage();

        tesseractApi.Clear();

        return SKBitmap.FromImage(thresholdedImage);
    }

    public async Task<string> Recognize(SKBitmap image, string imageFilename, IEnumerable<string> languages, Rectangle region = new())
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

                    var bytes = GetBitmapBytes(image);

                    tesseractApi.SetInputName(imageFilename);
                    tesseractApi.SetImage(bytes, image.Width, image.Height, image.BytesPerPixel, image.RowBytes);

                    tesseractApi.SetPageSegMode(PageSegmentationMode.SegmentationOcr);
                    // tesseractApi.SetSourceResolution(300);

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

    private static byte[] GetBitmapBytes(SKBitmap image) => image.GetPixelSpan().ToArray();

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
