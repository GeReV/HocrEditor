using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HocrEditor.Core;
using HocrEditor.Models;
using HocrEditor.Services;
using HtmlAgilityPack;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Win32;

namespace HocrEditor.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly Window window;

        public MainWindowViewModel(Window window)
        {
            this.window = window;
            ImportCommand = new RelayCommand(Import);
        }

        public bool AutoClean
        {
            get => Settings.AutoClean;
            set => Settings.AutoClean = value;
        }

        public HocrDocumentViewModel Document { get; set; } = new();

        public IRelayCommand ImportCommand { get; }

        private string? GetTesseractPath()
        {
            var tesseractPath = Settings.TesseractPath;

            if (tesseractPath != null)
            {
                return tesseractPath;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Locate tesseract.exe...",
                Filter = "Executables (*.exe)|*.exe"
            };

            if (!(dialog.ShowDialog(Window.GetWindow(window)) ?? false))
            {
                return null;
            }

            tesseractPath = dialog.FileName;

            Settings.TesseractPath = tesseractPath;

            return tesseractPath;
        }

        private void Import()
        {
            var tesseractPath = GetTesseractPath();

            if (tesseractPath == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Pick Images",
                Filter =
                    "Image files (*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png)|*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png",
                Multiselect = true
            };

            if (dialog.ShowDialog(window) != true)
            {
                return;
            }

            var imagePaths = dialog.FileNames;

            var pages = imagePaths.Select(image => new HocrPageViewModel(image)).ToList();

            var service = new TesseractService(tesseractPath);

            foreach (var page in pages)
            {
                Document.Pages.Add(page);

                Task.Run(
                        async () =>
                        {
                            var body = await service.PerformOcr(page.Image, new[] { "script/Hebrew", "eng" });

                            var doc = new HtmlDocument();
                            doc.LoadHtml(body);

                            return new HocrPageParser().Parse(doc);
                        }
                    )
                    .ContinueWith(
                        async hocrPage =>
                        {
                            try
                            {
                                page.HocrPage = await hocrPage;

                                var averageFontSize = page.HocrPage.Items
                                    .Where(node => node.NodeType == HocrNodeType.Word)
                                    .Cast<HocrWord>()
                                    .Average(node => node.FontSize);

                                var (dpix, dpiy) = page.HocrPage.Dpi;

                                const float fontInchRatio = 1.0f / 72f;

                                var noiseNodes = page.Nodes.Where(
                                        node => node.NodeType == HocrNodeType.ContentArea &&
                                                string.IsNullOrEmpty(node.InnerText) &&
                                                (node.BBox.Width < averageFontSize * fontInchRatio * dpix ||
                                                 node.BBox.Height < averageFontSize * fontInchRatio * dpiy)
                                    )
                                    .ToList();

                                page.DeleteCommand.Execute(noiseNodes);

                                var graphics = page.Nodes.Where(
                                        node => node.NodeType == HocrNodeType.ContentArea &&
                                                string.IsNullOrEmpty(node.InnerText)
                                    )
                                    .ToList();

                                page.ConvertToImageCommand.Execute(graphics);

                                page.UndoRedoManager.Clear();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"{ex.Message}\n{ex.Source}\n\n{ex.StackTrace}");
                            }
                        },
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
            }
        }
    }
}
