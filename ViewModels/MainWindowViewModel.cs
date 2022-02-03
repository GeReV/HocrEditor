using System;
using System.Diagnostics;
using System.IO;
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

            SaveCommand = new RelayCommand<bool>(Save);
            OpenCommand = new RelayCommand(Open);
        }

        public bool AutoClean
        {
            get => Settings.AutoClean;
            set => Settings.AutoClean = value;
        }

        public HocrDocumentViewModel Document { get; set; } = new();

        public IRelayCommand<bool> SaveCommand { get; }
        public IRelayCommand OpenCommand { get; }
        public IRelayCommand ImportCommand { get; }


        public bool IsSelecting { get; set; }

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

        private void Save(bool forceSaveAs)
        {
            if (Document.Filename == null || forceSaveAs)
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save hOCR File",
                    Filter = "hOCR file (*.hocr)|*.hocr",
                };

                if (dialog.ShowDialog(window) != true)
                {
                    return;
                }

                Document.Filename = dialog.FileName;
            }

            var hocrDocument = new HocrDocument(Document.Pages.Select(p => p.HocrPage!));

            var htmlDocument = new HocrWriter(Document.Filename).Build(hocrDocument);

            htmlDocument.Save(Document.Filename);
        }

        private void Open()
        {
            if (Document.IsDirty)
            {
                // TODO: Handle existing document.
            }

            var tesseractPath = GetTesseractPath();

            if (tesseractPath == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Open hOCR File",
                Filter = "hOCR file (*.hocr)|*.hocr",
            };

            if (dialog.ShowDialog(window) != true)
            {
                return;
            }

            Document = new HocrDocumentViewModel
            {
                Filename = dialog.FileName
            };

            Task.Run(
                    () => new HocrParser().Parse(Document.Filename)
                )
                .ContinueWith(
                    async hocrDocumentTask =>
                    {
                        try
                        {
                            var hocrDocument = await hocrDocumentTask;

                            foreach (var page in hocrDocument.Pages.Select(hocrPage => new HocrPageViewModel(hocrPage)))
                            {
                                Document.Pages.Add(page);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"{ex.Message}\n{ex.Source}\n\n{ex.StackTrace}");
                        }
                    },
                    TaskScheduler.FromCurrentSynchronizationContext()
                );
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

                            return new HocrParser().Parse(doc);
                        }
                    )
                    .ContinueWith(
                        async hocrDocumentTask =>
                        {
                            try
                            {
                                var hocrDocument = await hocrDocumentTask;

                                Debug.Assert(hocrDocument.Pages.Count == 1);

                                page.Build(hocrDocument.Pages.First());

                                if (page.HocrPage == null)
                                {
                                    // Unreachable.
                                    throw new InvalidOperationException("page.HocrPage cannot be null.");
                                }

                                var averageFontSize = page.HocrPage.Descendants
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
