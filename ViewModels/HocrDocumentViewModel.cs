using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels;

public class HocrDocumentViewModel : ViewModelBase, IUndoRedoCommandsService
{
    public UndoRedoManager UndoRedoManager { get; } = new();

    public bool IsDirty { get; set; }

    public string? Filename { get; set; }

    private HocrDocument HocrDocument { get; set; }

    public ObservableCollection<HocrPageViewModel> Pages { get; }

    public ICollectionView PagesCollectionView { get; }

    public string OcrSystem
    {
        get => HocrDocument.OcrSystem;
        set => HocrDocument.OcrSystem = value;
    }

    public List<string> Capabilities => HocrDocument.Capabilities;

    public HocrPageViewModel? CurrentPage
    {
        get => (HocrPageViewModel?)PagesCollectionView.CurrentItem;
        set => PagesCollectionView.MoveCurrentTo(value);
    }

    public bool ShowText { get; set; }

    public Rect SelectionBounds
    {
        get => CurrentPage?.SelectionBounds ?? Rect.Empty;
        set
        {
            if (CurrentPage != null)
            {
                CurrentPage.SelectionBounds = value;
            }
        }
    }

    public ReadOnlyObservableCollection<NodeVisibility> NodeVisibility { get; } = new(
        new ObservableCollection<NodeVisibility>(
            new[]
                {
                    HocrNodeType.Page,
                    HocrNodeType.ContentArea,
                    HocrNodeType.Paragraph,
                    HocrNodeType.Line,
                    HocrNodeType.Header,
                    HocrNodeType.Footer,
                    HocrNodeType.Caption,
                    HocrNodeType.TextFloat,
                    HocrNodeType.Word,
                    HocrNodeType.Image
                }
                .Select(k => new NodeVisibility(k))
        )
    );

    public IRelayCommand<HocrPageViewModel> DeletePageCommand { get; }

    public IRelayCommand NextPageCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }

    public HocrDocumentViewModel(HocrDocument hocrDocument, IEnumerable<HocrPageViewModel> pages)
    {
        HocrDocument = hocrDocument;

        Pages = new ObservableCollection<HocrPageViewModel>(pages);

        PagesCollectionView = CollectionViewSource.GetDefaultView(Pages);
        PagesCollectionView.CurrentChanged += PagesCollectionViewOnCurrentChanged;

        DeletePageCommand = new RelayCommand<HocrPageViewModel>(DeletePage, CanDeletePage);

        NextPageCommand = new RelayCommand(
            () => PagesCollectionView.MoveCurrentToNext(),
            () => !PagesCollectionView.IsCurrentLast()
        );
        PreviousPageCommand = new RelayCommand(
            () => PagesCollectionView.MoveCurrentToPrevious(),
            () => !PagesCollectionView.IsCurrentFirst()
        );
    }

    public bool CanDeletePage(HocrPageViewModel? page) => page != null && Pages.Contains(page);

    public void DeletePage(HocrPageViewModel? page)
    {
        if (page == null)
        {
            return;
        }

        UndoRedoManager.ExecuteCommand(Pages.ToCollectionRemoveCommand(page));
    }

    public HocrDocumentViewModel() : this(
        new HocrDocument(Enumerable.Empty<HocrPage>()),
        Enumerable.Empty<HocrPageViewModel>()
    )
    {
    }

    public HocrDocument BuildDocumentModel()
    {
        HocrDocument.Pages.Clear();

        if (Pages.Any(page => page.HocrPage == null))
        {
            throw new InvalidOperationException("Expected all HocrPages to not be null");
        }

        HocrDocument.Pages.AddRange(Pages.Select(page => page.HocrPage!));

        return HocrDocument;
    }

    private void PagesCollectionViewOnCurrentChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentPage));
    }
}
