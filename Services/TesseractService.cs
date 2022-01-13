using System;
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

        public async void GetLanguages()
        {
            var languages = await Task.Run(
                async () =>
                {
                    var result = await ProcessRunner.Run(TesseractPath, "--list-langs");

                    return result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
                }
            );
        }

        // TODO: Determine DPI from image.
        public async Task<string> PerformOcr(string filename, string[] languages) => await ProcessRunner.Run(
            TesseractPath,
            $"{filename} stdout --dpi 300 -l {string.Join('+', languages)} --psm 3 -c hocr_font_info=1 hocr"
        );
    }
}
