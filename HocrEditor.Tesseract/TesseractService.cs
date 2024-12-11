using System.Drawing;
using System.Security;
using Microsoft.Win32;
using Optional;
using SkiaSharp;

namespace HocrEditor.Tesseract;

public sealed class TesseractService : IDisposable
{
    private bool isDisposed;
    private readonly Lock lck = new();
    private readonly TesseractApi tesseractApi;

    public static Option<string> DefaultPath
    {
        get
        {
            const string tesseractKey = "SOFTWARE\\Tesseract-OCR";

            try
            {
                using var tesseractRegistryKey = Registry.CurrentUser.OpenSubKey(tesseractKey) ??
                                                 Registry.LocalMachine.OpenSubKey(tesseractKey);

                if (tesseractRegistryKey?.GetValue("Path") is string path)
                {
                    return Option.Some(path);
                }
            }
            catch (SecurityException)
            {
            }

            return Option.None<string>();
        }
    }

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
        ObjectDisposedException.ThrowIf(isDisposed, this);

        lock (lck)
        {
            tesseractApi.SetImage(image.GetPixelSpan(), image.Width, image.Height, image.BytesPerPixel, image.RowBytes);

            return tesseractApi.GetThresholdedImage();
        }
    }

    public async Task<string> Recognize(SKBitmap image, string imageFilename, Rectangle region = new())
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        return await Task.Run(
                () =>
                {
                    lock (lck)
                    {
                        ObjectDisposedException.ThrowIf(isDisposed, this);

                        tesseractApi.SetInputName(imageFilename);
                        tesseractApi.SetImage(
                            image.GetPixelSpan(),
                            image.Width,
                            image.Height,
                            image.BytesPerPixel,
                            image.RowBytes
                        );

                        // tesseractApi.SetSourceResolution(300);

                        if (!region.IsEmpty)
                        {
                            tesseractApi.SetRectangle(region.X, region.Y, region.Width, region.Height);
                        }

                        return tesseractApi.GetHocrText();
                    }
                }
            )
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        lock (lck)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);

            tesseractApi.Clear();
            tesseractApi.Dispose();
        }

        isDisposed = true;
    }
}
