using System.Diagnostics;
using System.Drawing;
using SkiaSharp;

namespace HocrEditor.Tesseract;

public sealed class TesseractService : IDisposable
{
    private bool isDisposed;
    private readonly object lck = new();
    private readonly TesseractApi tesseractApi;

    public TesseractService(string tesseractPath, IEnumerable<string> languages)
    {
        tesseractApi = TesseractFactory.CreateApi(tesseractPath);

        tesseractApi.Init(string.Join('+', languages));
        tesseractApi.SetVariable("hocr_font_info", "1");
        tesseractApi.SetVariable("thresholding_method", "1");
        tesseractApi.SetVariable("thresholding_smooth_kernel_size", "5.0");
        tesseractApi.SetPageSegMode(PageSegmentationMode.SegmentationOcr);
    }

    public string GetVersion()
    {
        lock (lck)
        {
            return tesseractApi.Version();
        }
    }

    public static string[] GetLanguages(string tesseractPath)
    {
        using var service = new TesseractService(tesseractPath, Enumerable.Empty<string>());

        return service.GetLanguages();
    }

    public string[] GetLanguages()
    {
        lock (lck)
        {
            return tesseractApi.GetAvailableLanguages();
        }
    }

    public SKBitmap GetThresholdedImage(SKBitmap image, Rectangle region = new())
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(tesseractApi));
        }

        lock (lck)
        {
            var bytes = GetBitmapBytes(image);

            tesseractApi.SetImage(bytes, image.Width, image.Height, image.BytesPerPixel, image.RowBytes);

            return tesseractApi.GetThresholdedImage();
        }
    }

    public async Task<string> Recognize(SKBitmap image, string imageFilename, Rectangle region = new())
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

                    var bytes = GetBitmapBytes(image);

                    tesseractApi.SetInputName(imageFilename);
                    tesseractApi.SetImage(bytes, image.Width, image.Height, image.BytesPerPixel, image.RowBytes);

                    // tesseractApi.SetSourceResolution(300);

                    if (!region.IsEmpty)
                    {
                        tesseractApi.SetRectangle(region.X, region.Y, region.Width, region.Height);
                    }

                    return tesseractApi.GetHocrText();
                }
            }
        ).ConfigureAwait(false);
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
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(tesseractApi));
            }

            tesseractApi.Clear();
            tesseractApi.Dispose();
        }

        isDisposed = true;
    }
}
