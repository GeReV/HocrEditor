using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

        public bool AutoClean
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
                    sb.Append($"Page {Document.PagesCollectionView.CurrentPosition + 1}/{Document.Pages.Count} - ");
                }

                sb.Append(ApplicationName);

                return sb.ToString();
            }
        }

        public ObservableCollection<TesseractLanguage> TesseractLanguages { get; } = new();

        public HocrDocumentViewModel Document { get; set; } = new();

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

            switch (result)
            {
                case MessageBoxResult.Cancel:
                    return false;
                case MessageBoxResult.Yes:
                    return Save(forceSaveAs: false);
                case MessageBoxResult.No:
                    // Ignore.
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool CanSave(bool _) => Document.IsChanged && Document.Pages.Any();

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

            var filename = dialog.FileName;

            Task.Run(
                    () => new HocrParser().Parse(filename)
                )
                .ContinueWith(
                    async hocrDocumentTask =>
                    {
                        try
                        {
                            var hocrDocument = await hocrDocumentTask;

                            using var service = new TesseractService(tesseractPath, Enumerable.Empty<string>());

                            var pages = new List<HocrPageViewModel>(hocrDocument.Pages.Count);

                            foreach (var hocrPage in hocrDocument.Pages)
                            {
                                var page = new HocrPageViewModel(hocrPage);

                                ArgumentNullException.ThrowIfNull(page.Image);

                                // page.ThresholdedImage = service.GetThresholdedImage(page.Image);

                                pages.Add(page);
                            }

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

            foreach (var page in pages)
            {
                Document.Pages.Add(page);

                Task.Run(
                        async () =>
                        {
                            var languages = TesseractLanguages.Where(l => l.IsSelected)
                                .Select(l => l.Language);

                            using var service = new TesseractService(tesseractPath, languages);

                            ArgumentNullException.ThrowIfNull(page.Image.Value);

                            var body = await service.Recognize(page.Image.Value, page.ImageFilename);

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
                                var hocrDocument = await hocrDocumentTask;

                                Document.OcrSystem = hocrDocument.OcrSystem;

                                Document.Capabilities.AddRange(
                                    hocrDocument.Capabilities.ToHashSet().Except(Document.Capabilities)
                                );

                                Debug.Assert(hocrDocument.Pages.Count == 1);

                                page.Build(hocrDocument.Pages.First());

                                if (page.HocrPage == null)
                                {
                                    // Unreachable.
                                    throw new InvalidOperationException("page.HocrPage cannot be null.");
                                }

                                var averageFontSize = page.HocrPage.Descendants
                                    .Where(node => node.IsLineElement)
                                    .Cast<HocrLine>()
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

        private string? GetTesseractPath()
        {
            var tesseractPath = Settings.TesseractPath;

            if (tesseractPath != null)
            {
                return tesseractPath;
            }

            var dialog = new FolderBrowserDialog
            {
                Description = "Locate Tesseract OCR...",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(window.GetIWin32Window()) != DialogResult.OK)
            {
                return null;
            }

            tesseractPath = dialog.SelectedPath;

            Settings.TesseractPath = tesseractPath;

            return tesseractPath;
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
