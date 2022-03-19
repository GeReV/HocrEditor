using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HocrEditor.Services
{
    public class TesseractService : IDisposable
    {
        private readonly TesseractApi tesseractApi;

        public TesseractService(string tesseractPath)
        {
            tesseractApi = Tesseract.CreateApi(tesseractPath);
        }

        public string GetVersion() => tesseractApi.Version();

        public string[] GetLanguages() => tesseractApi.GetAvailableLanguages();

        public async Task<string> PerformOcr(string filename, IEnumerable<string> languages, Rectangle region = new()) =>
            await Task.Run(
                () =>
                {
                    tesseractApi.Init(string.Join('+', languages));
                    tesseractApi.SetVariable("hocr_font_info", "1");

                    using var image = Image.FromFile(filename);

                    if (image is not Bitmap bmp)
                    {
                        bmp = new Bitmap(image);
                    }

                    var bmpData = bmp.LockBits(
                        new Rectangle(0, 0, bmp.Width, bmp.Height),
                        ImageLockMode.ReadOnly,
                        bmp.PixelFormat
                    );
                    var bpp = Image.GetPixelFormatSize(bmpData.PixelFormat) / 8;
                    var size = bmpData.Height * bmpData.Stride;
                    var bytes = new byte[size];

                    Marshal.Copy(bmpData.Scan0, bytes, 0, size);

                    bmp.UnlockBits(bmpData);

                    if (bmp != image)
                    {
                        bmp.Dispose();
                    }

                    tesseractApi.SetInputName(filename);
                    tesseractApi.SetImage(bytes, bmpData.Width, bmpData.Height, bpp, bmpData.Stride);

                    tesseractApi.SetPageSegMode(TesseractPageSegMode.PsmAuto);
                    tesseractApi.SetSourceResolution((int)image.HorizontalResolution);

                    if (!region.IsEmpty)
                    {
                        tesseractApi.SetRectangle(region.X, region.Y, region.Width, region.Height);
                    }

                    var text = tesseractApi.GetHOCRText();

                    tesseractApi.Clear();

                    return text;
                }
            );

        public void Dispose()
        {
            tesseractApi.Dispose();
        }
    }
}
