using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.Services;
using HocrEditor.ViewModels;
using HtmlAgilityPack;
using Microsoft.Win32;

namespace HocrEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
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

            if (dialog.ShowDialog(this) == true)
            {
                var imagePaths = dialog.FileNames;

                var pages = imagePaths.Select(image => new HocrPageViewModel(image)).ToList();

                var service = new TesseractService(tesseractPath);

                foreach (var page in pages)
                {
                    ViewModel.Document.Pages.Add(page);

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

            if (!(dialog.ShowDialog(this) ?? false))
            {
                return null;
            }

            tesseractPath = dialog.FileName;

            Settings.TesseractPath = tesseractPath;

            return tesseractPath;
        }

        private void Canvas_OnNodesChanged(object? sender, NodesChangedEventArgs e)
        {
            ViewModel.Document.CurrentPage?.UpdateNodesCommand.Execute(e.Changes);
        }

        private void Canvas_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var items = e.AddedItems.Cast<HocrNodeViewModel>().ToList();
                ViewModel.Document.CurrentPage?.AppendSelectNodesCommand.TryExecute(items);

                foreach (var parent in items.SelectMany(n => n.Ascendants))
                {
                    // Close on deselect?
                    parent.IsExpanded = true;
                }
            }

            if (e.RemovedItems.Count > 0)
            {
                ViewModel.Document.CurrentPage?.DeselectNodesCommand.TryExecute(
                    e.RemovedItems.Cast<HocrNodeViewModel>().ToList()
                );
            }
        }

        private void DocumentTreeView_OnNodeEdited(object? sender, NodesEditedEventArgs e)
        {
            ViewModel.Document.CurrentPage?.EditNodesCommand.Execute(e.Value);
        }

        private void DocumentTreeView_OnNodesMoved(object? sender, NodesMovedEventArgs e)
        {
            ViewModel.Document.CurrentPage?.MoveNodesCommand.Execute(e);
        }
    }
}
