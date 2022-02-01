using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HocrEditor.Core;
using HocrEditor.Models;
using HocrEditor.Services;
using HtmlAgilityPack;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Win32;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly Window window;
        private Rect selectionBounds;

        public MainWindowViewModel(Window window)
        {
            this.window = window;

            ImportCommand = new RelayCommand(Import);
            OcrRegionCommand = new RelayCommand(OcrRegion, CanOcrRegion);
        }

        public bool AutoClean
        {
            get => Settings.AutoClean;
            set => Settings.AutoClean = value;
        }

        public HocrDocumentViewModel Document { get; set; } = new();

        public IRelayCommand ImportCommand { get; }
        public IRelayCommand OcrRegionCommand { get; }

        public bool IsSelecting { get; set; }

        public Rect SelectionBounds
        {
            get => selectionBounds;
            set
            {
                selectionBounds = value;

                OcrRegionCommand.NotifyCanExecuteChanged();
            }
        }

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
                                page.Build(await hocrPage);

                                if (page.HocrPage == null)
                                {
                                    // Unreachable.
                                    throw new InvalidOperationException("page.HocrPage cannot be null.");
                                }

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

        private bool CanOcrRegion() => !SelectionBounds.IsEmpty && Document.CurrentPage != null;

        private void OcrRegion()
        {
            var tesseractPath = GetTesseractPath();

            if (tesseractPath == null)
            {
                return;
            }

            var region = new Rectangle(
                SelectionBounds.Left,
                SelectionBounds.Top,
                SelectionBounds.Width,
                SelectionBounds.Height
            );

            var page = Document.CurrentPage ?? throw new InvalidOperationException(
                $"Expected {nameof(Document)}.{nameof(Document.CurrentPage)} to not be null."
            );


            Task.Run(
                    async () =>
                    {
                        var service = new TesseractService(tesseractPath);



                        var body = await service.PerformOcrRegion(page.Image, region, new[] { "script/Hebrew", "eng" });

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
                            var p = new HocrPageViewModel(page.Image);

                            p.Build(await hocrPage);

                            var pRootNode = p.Nodes.First(n => n.IsRoot);

                            var descendants = pRootNode.Descendents.ToList();

                            foreach (var node in descendants)
                            {
                                var bbox = node.BBox;

                                bbox.Offset(SelectionBounds.Location);

                                node.Id += "_foo";
                                if (node.ParentId != null && !node.ParentId.StartsWith("page_"))
                                {
                                    node.ParentId += "_foo";
                                }
                                node.BBox = bbox;
                            }

                            var pageRootNode = page.Nodes.First(n => n.IsRoot);

                            foreach (var node in pRootNode.Children)
                            {
                                node.Parent = pageRootNode;

                                pageRootNode.Children.Add(node);
                            }



                            page.Nodes.AddRange(descendants);
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
