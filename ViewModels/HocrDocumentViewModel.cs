using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels;

public class HocrDocumentViewModel : ViewModelBase
{
    public ObservableCollection<HocrPageViewModel> Pages { get; }

    public ICollectionView PagesCollectionView { get; }

    public HocrPageViewModel? CurrentPage
    {
        get => (HocrPageViewModel?)PagesCollectionView.CurrentItem;
        set => PagesCollectionView.MoveCurrentTo(value);
    }

    public IRelayCommand NextPageCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }

    private HocrDocumentViewModel(IEnumerable<HocrPageViewModel> pages)
    {
        Pages = new ObservableCollection<HocrPageViewModel>(pages);

        PagesCollectionView = CollectionViewSource.GetDefaultView(Pages);
        PagesCollectionView.CurrentChanged += PagesCollectionViewOnCurrentChanged;

        NextPageCommand = new RelayCommand(() => PagesCollectionView.MoveCurrentToNext(), () => !PagesCollectionView.IsCurrentLast());
        PreviousPageCommand = new RelayCommand(() => PagesCollectionView.MoveCurrentToPrevious(), () => !PagesCollectionView.IsCurrentFirst());
    }

    public HocrDocumentViewModel(HocrDocument hocrDocument) : this(
        hocrDocument.Pages.Select(p => new HocrPageViewModel(p))
    )
    {
    }

    public HocrDocumentViewModel() : this(Enumerable.Empty<HocrPageViewModel>())
    {
    }

    private void PagesCollectionViewOnCurrentChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentPage));
    }
}
