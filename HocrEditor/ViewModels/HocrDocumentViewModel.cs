using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Data;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.ViewModels;

public class HocrDocumentViewModel : ViewModelBase, IUndoRedoCommandsService
{
    public UndoRedoManager UndoRedoManager { get; } = new();

    public string? Filename { get; set; }

    public string Name => Filename ?? "New document";

    public string Title
    {
        get
        {
            var sb = new StringBuilder();

            if (IsChanged)
            {
                sb.Append('*');
            }

            sb.Append(Name);

            return sb.ToString();
        }
    }

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

    public bool ShowThresholdedImage { get; set; }

    public bool ShowText { get; set; }

    public bool ShowNumbering { get; set; }

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

    public ICanvasTool CanvasTool { get; set; }

    public ReadOnlyObservableCollection<NodeVisibility> NodeVisibility { get; } = new(
        new ObservableCollection<NodeVisibility>(
            HocrNodeTypeViewModel.NodeTypes.Select(k => new NodeVisibility(k))
        )
    );

    public IRelayCommand<HocrPageViewModel> DeletePageCommand { get; }
    public IRelayCommand NextPageCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }
    public IRelayCommand<ICanvasTool> SelectToolCommand { get; }

    public HocrDocumentViewModel() : this(
        new HocrDocument(Enumerable.Empty<HocrPage>()),
        Enumerable.Empty<HocrPageViewModel>()
    )
    {
    }

    public HocrDocumentViewModel(HocrDocument hocrDocument, IEnumerable<HocrPageViewModel> pages)
    {
        HocrDocument = hocrDocument;

        CanvasTool = DocumentCanvasTools.SelectionTool;

        Pages = new ObservableCollection<HocrPageViewModel>(pages);

        Pages.SubscribeItemPropertyChanged(PagesChanged);

        PagesCollectionView = CollectionViewSource.GetDefaultView(Pages);
        PagesCollectionView.CurrentChanging += PagesCollectionViewOnCurrentChanging;
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

        SelectToolCommand = new RelayCommand<ICanvasTool>(
            tool =>
            {
                tool ??= DocumentCanvasTools.SelectionTool;

                if (CanvasTool != tool)
                {
                    CanvasTool = tool;
                }
            },
            tool =>
            {
                tool ??= DocumentCanvasTools.SelectionTool;

                if (CurrentPage == null)
                {
                    return false;
                }

                return tool.CanMount(CurrentPage);
            }
        );

        IsChanged = false;
    }

    private void PagesChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IsChanged):
            {
                IsChanged = Pages.Any(p => p.IsChanged);
                break;
            }
        }
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

    public override void MarkAsUnchanged()
    {
        foreach (var page in Pages)
        {
            page.MarkAsUnchanged();
        }

        base.MarkAsUnchanged();
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

    private void PagesCollectionViewOnCurrentChanging(object sender, CurrentChangingEventArgs e)
    {
        if (sender is ICollectionView { CurrentItem: HocrPageViewModel page })
        {
            page.SelectedNodes.CollectionChanged -= CurrentPageSelectedNodesOnCollectionChanged;
        }
    }

    private void PagesCollectionViewOnCurrentChanged(object? sender, EventArgs e)
    {
        if (sender is ICollectionView { CurrentItem: HocrPageViewModel page })
        {
            page.SelectedNodes.CollectionChanged += CurrentPageSelectedNodesOnCollectionChanged;
        }

        SelectToolCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(CurrentPage));
    }

    private void CurrentPageSelectedNodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SelectToolCommand.NotifyCanExecuteChanged();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Pages.UnsubscribeItemPropertyChanged(PagesChanged);

        if (CurrentPage != null)
        {
            CurrentPage.SelectedNodes.CollectionChanged -= CurrentPageSelectedNodesOnCollectionChanged;
        }

        PagesCollectionView.CurrentChanging -= PagesCollectionViewOnCurrentChanging;
        PagesCollectionView.CurrentChanged -= PagesCollectionViewOnCurrentChanged;

        Pages.Dispose();

        NodeVisibility.Dispose();
    }
}
