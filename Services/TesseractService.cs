using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HocrEditor.Services
{
    public class TesseractService
    {
        public TesseractService(string tesseractPath)
        {
            TesseractPath = tesseractPath;
        }

        public string TesseractPath { get; private set; }

        public async Task<List<string>> GetLanguages()
        {
            return await Task.Run(
                async () =>
                {
                    var result = await ProcessRunner.Run(TesseractPath, "--list-langs");

                    return result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
                }
            );
        }

        public async Task<string> PerformOcr(string filename, string[] languages)
        {
            using var image = Image.FromFile(filename);

            return await ProcessRunner.Run(
                TesseractPath,
                $"{filename} stdout --dpi {(int)image.HorizontalResolution} -l {string.Join('+', languages)} --psm 3 -c hocr_font_info=1 hocr"
            );
        }

        public async Task<string> PerformOcrRegion(string filename, Rectangle region, string[] languages)
        {
            using var image = Image.FromFile(filename);

            using var bitmap = new Bitmap(image);

            using var cropped = bitmap.Clone(region, bitmap.PixelFormat);

            var tempFile = new FileInfo(Path.GetTempFileName());

            cropped.Save(tempFile.FullName, ImageFormat.Bmp);

            var result = await PerformOcr(tempFile.FullName, languages);

            tempFile.Delete();

            return result;
        }
    }
}
