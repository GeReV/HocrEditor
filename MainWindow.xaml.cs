using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
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
    public partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

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
                    "Image files (*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png)|*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png"
            };

            if (dialog.ShowDialog(this) == true)
            {
                Task.Run(
                        async () =>
                        {
                            var imagePath = dialog.FileName;

                            var service = new TesseractService(tesseractPath);

                            var body = await service.PerformOcr(imagePath, new[] { "script/Hebrew", "eng" });

                            var doc = new HtmlDocument();
                            doc.LoadHtml(body);

                            return new HocrDocumentParser().Parse(doc);
                        }
                    )
                    .ContinueWith(
                        async hocr =>
                        {
                            try
                            {
                                var hocrDocument = await hocr;

                                ViewModel.Document = new HocrDocumentViewModel(hocrDocument);

                                var averageFontSize = hocrDocument.Items
                                    .Where(node => node.NodeType == HocrNodeType.Word)
                                    .Cast<HocrWord>()
                                    .Average(node => node.FontSize);

                                var (dpix, dpiy) = hocrDocument.RootNode.Dpi;

                                const float fontInchRatio = 1.0f / 72f;

                                var noiseNodes = ViewModel.Document.Nodes.Where(
                                        node => node.NodeType == HocrNodeType.ContentArea &&
                                                string.IsNullOrEmpty(node.InnerText) &&
                                                (node.BBox.Width < averageFontSize * fontInchRatio * dpix ||
                                                 node.BBox.Height < averageFontSize * fontInchRatio * dpiy)
                                    )
                                    .ToList();

                                ViewModel.DeleteCommand.Execute(noiseNodes);

                                var graphics = ViewModel.Document.Nodes.Where(
                                        node => node.NodeType == HocrNodeType.ContentArea &&
                                                string.IsNullOrEmpty(node.InnerText)
                                    )
                                    .ToList();

                                ViewModel.ConvertToImageCommand.Execute(graphics);

                                ViewModel.UndoRedoManager.Clear();
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

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedNodes = ViewModel.Document?.SelectedNodes;

            if (e.NewValue is not HocrNodeViewModel node)
            {
                return;
            }

            // selectedNodes?.ReplaceRange(new []{ node });
        }

        private void Canvas_OnNodesChanged(object? sender, NodesChangedEventArgs e)
        {
            ViewModel.UpdateNodesCommand.Execute(e.Changes);
        }

        private void Canvas_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var items = e.AddedItems.Cast<HocrNodeViewModel>().ToList();
                ViewModel.SelectNodesCommand.TryExecute(items);

                foreach (var parent in items.SelectMany(n => n.Ascendants))
                {
                    // Close on deselect?
                    parent.IsExpanded = true;
                }
            }

            if (e.RemovedItems.Count > 0)
            {
                ViewModel.DeselectNodesCommand.TryExecute(e.RemovedItems.Cast<HocrNodeViewModel>().ToList());
            }
        }
    }
}
