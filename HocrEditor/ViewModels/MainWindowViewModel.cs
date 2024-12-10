using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.Services;
using HocrEditor.Tesseract;
using JetBrains.Annotations;
using Microsoft.Toolkit.Mvvm.Input;
using Optional;
using Optional.Unsafe;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace HocrEditor.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly Window window;

        public MainWindowViewModel(Window window)
        {
            this.window = window;

            Document.PropertyChanged += DocumentOnPropertyChanged;

            TesseractLanguages.CollectionChanged += TesseractLanguagesChanged;
            TesseractLanguages.SubscribeItemPropertyChanged(TesseractLanguagesChanged);

            ImportCommand = new RelayCommand(Import);

            SaveCommand = new RelayCommand<bool>(forceSaveAs => Save(forceSaveAs), CanSave);
            OpenCommand = new RelayCommand(Open);
        }

        private void TesseractLanguagesChanged(object? sender, EventArgs e)
        {
            Settings.TesseractSelectedLanguages =
                TesseractLanguages.Where(l => l.IsSelected).Select(l => l.Language).ToList();
        }

        public static bool AutoClean
        {
            get => Settings.AutoClean;
            set => Settings.AutoClean = value;
        }

        private static string? ApplicationName =>
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product;

        public string WindowTitle
        {
            get
            {
                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(Document.Title))
                {
                    sb.Append(Document.Title);
                    sb.Append(" - ");
                }

                if (Document.Pages.Count > 0)
                {
                    sb.Append(
                        CultureInfo.InvariantCulture,
                        $"Page {Document.PagesCollectionView.CurrentPosition + 1}/{Document.Pages.Count} - "
                    );
                }

                sb.Append(ApplicationName);

                return sb.ToString();
            }
        }

        public ObservableCollection<TesseractLanguage> TesseractLanguages { get; } = new();

        public HocrDocumentViewModel Document { get; private set; } = new();

        public IRelayCommand<bool> SaveCommand { get; }
        public IRelayCommand OpenCommand { get; }
        public IRelayCommand ImportCommand { get; }

        [UsedImplicitly]
        private void OnDocumentChanged(HocrDocumentViewModel oldValue, HocrDocumentViewModel newValue)
        {
            oldValue.PropertyChanged -= DocumentOnPropertyChanged;
            oldValue.Dispose();

            newValue.PropertyChanged += DocumentOnPropertyChanged;
        }

        private void DocumentOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IsChanged):
                {
                    OnPropertyChanged(nameof(WindowTitle));

                    SaveCommand.NotifyCanExecuteChanged();

                    break;
                }
                case nameof(Document.CurrentPage):
                {
                    OnPropertyChanged(nameof(WindowTitle));
                    break;
                }
            }
        }

        public bool AskSaveOnExit()
        {
            var result = MessageBox.Show(
                $"Do you want to save changes to {Document.Name}?",
                ApplicationName,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );

            return result switch
            {
                MessageBoxResult.Cancel => false,
                MessageBoxResult.Yes => Save(forceSaveAs: false),
                MessageBoxResult.No =>
                    // Ignore.
                    true,
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            };
        }

        private bool CanSave(bool forceSaveAs) => (Document.IsChanged || forceSaveAs) && Document.Pages.Count > 0;

        private bool Save(bool forceSaveAs)
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
                    return false;
                }

                Document.Filename = dialog.FileName;
            }

            // TODO: Figure out syncing and using the model.
            var htmlDocument = new HocrWriter(Document, Document.Filename).Build();

            htmlDocument.Save(Document.Filename);

            Document.MarkAsUnchanged();

            return true;
        }

        private void Open()
        {
            if (Document.IsChanged && !AskSaveOnExit())
            {
                return;
            }

            var tesseractPath = GetTesseractPath();
            if (!tesseractPath.HasValue)
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

            var filename = dialog.FileName;

            _ = Task.Run(
                    () => new HocrParser().Parse(filename)
                )
                .ContinueWith(
                    async hocrDocumentTask =>
                    {
                        try
                        {
                            var hocrDocument = await hocrDocumentTask.ConfigureAwait(false);

                            using var service = new TesseractService(
                                tesseractPath.ValueOrFailure(),
                                Enumerable.Empty<string>()
                            );

                            var pages = new List<HocrPageViewModel>(hocrDocument.Pages.Count);
                            pages.AddRange(hocrDocument.Pages.Select(hocrPage => new HocrPageViewModel(hocrPage)));

                            var documentViewModel = new HocrDocumentViewModel(hocrDocument, pages)
                            {
                                Filename = filename,
                            };

                            documentViewModel.MarkAsUnchanged();

                            Document = documentViewModel;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"{ex.Message}\n{ex.Source}\n\n{ex.StackTrace}");
                        }
                    },
                    TaskScheduler.FromCurrentSynchronizationContext()
                )
                .ConfigureAwait(false);
        }

        private void Import()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pick Images",
                Filter =
                    "Image files (*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png)|*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png",
                Multiselect = true,
            };

            if (dialog.ShowDialog(window) != true)
            {
                return;
            }

            var imagePaths = dialog.FileNames;

            var pages = imagePaths.Select(image => new HocrPageViewModel(image)).ToList();

            foreach (var page in pages)
            {
                Document.Pages.Add(page);

                Task.Run(
                        async () =>
                        {
                            var languages = TesseractLanguages.Where(l => l.IsSelected)
                                .Select(l => l.Language);

                            using var service = new TesseractService(tesseractPath, languages);

                            var image = await page.Image.GetBitmap().ConfigureAwait(false);

                            var body = await service.Recognize(image, page.ImageFilename)
                                .ConfigureAwait(false);

                            // page.ThresholdedImage = service.GetThresholdedImage(page.Image);

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
                                var hocrDocument = await hocrDocumentTask.ConfigureAwait(false);

                                Document.OcrSystem = hocrDocument.OcrSystem;

                                Document.Capabilities.AddRange(
                                    hocrDocument.Capabilities
                                        .ToHashSet(StringComparer.Ordinal)
                                        .Except(Document.Capabilities, StringComparer.Ordinal)
                                );

                                Debug.Assert(hocrDocument.Pages.Count == 1);

                                page.Build(hocrDocument.Pages[0]);

                                if (page.HocrPage == null)
                                {
                                    // Unreachable.
                                    throw new InvalidOperationException("page.HocrPage cannot be null.");
                                }

                                var lines = page.HocrPage.Descendants
                                    .Where(node => node.IsLineElement)
                                    .Cast<HocrLine>()
                                    .ToList();

                                var averageFontSize = 8.0;
                                if (lines.Count > 0)
                                {
                                    averageFontSize = lines.Average(node => node.FontSize);
                                }

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
                            finally
                            {
                                page.IsProcessing = false;
                            }
                        },
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
            }
        }

        private Option<string> GetTesseractPath()
        {
            if (Settings.TesseractPath is { } path)
            {
                return Option.Some(path);
            }

            var tesseractPath = TesseractService.DefaultPath;

            if (!tesseractPath.HasValue)
            {
                MessageBox.Show("Tesseract path is invalid, please select a valid path.");

                tesseractPath = SelectTesseractPath();
            }

            tesseractPath.MatchSome(p => { Settings.TesseractPath = p; });

            return tesseractPath;
        }

        private Option<string> SelectTesseractPath()
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Locate Tesseract OCR...",
                UseDescriptionForTitle = true,
            };

            return dialog.ShowDialog(window.GetIWin32Window()) == DialogResult.OK
                ? dialog.SelectedPath.Some()
                : Option.None<string>();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            TesseractLanguages.CollectionChanged -= TesseractLanguagesChanged;
            TesseractLanguages.UnsubscribeItemPropertyChanged(TesseractLanguagesChanged);
            TesseractLanguages.Dispose();

            Document.PropertyChanged -= DocumentOnPropertyChanged;
            Document.Dispose();
        }
    }
}
