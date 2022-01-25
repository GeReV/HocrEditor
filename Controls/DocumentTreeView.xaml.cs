using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public partial class DocumentTreeView
{
    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems),
        typeof(ICollection<HocrNodeViewModel>),
        typeof(DocumentTreeView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
    );

    public static readonly DependencyProperty ItemsSourceProperty
        = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(DocumentTreeView),
            new FrameworkPropertyMetadata(null)
        );

    public ICollection<HocrNodeViewModel>? SelectedItems
    {
        get => (ICollection<HocrNodeViewModel>?)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set
        {
            if (value == null)
            {
                ClearValue(ItemsSourceProperty);
            }
            else
            {
                SetValue(ItemsSourceProperty, value);
            }
        }
    }

    public static readonly RoutedEvent NodesEditedEvent = EventManager.RegisterRoutedEvent(
        "NodesEdited",
        RoutingStrategy.Bubble,
        typeof(EventHandler<NodesEditedEventArgs>),
        typeof(DocumentTreeView)
    );

    public event EventHandler<NodesEditedEventArgs> NodesEdited
    {
        add => AddHandler(NodesEditedEvent, value);
        remove => RemoveHandler(NodesEditedEvent, value);
    }

    public static readonly RoutedEvent NodesMovedEvent = EventManager.RegisterRoutedEvent(
        "NodesMoved",
        RoutingStrategy.Bubble,
        typeof(EventHandler<NodesMovedEventArgs>),
        typeof(DocumentTreeView)
    );

    public event EventHandler<NodesMovedEventArgs> NodesMoved
    {
        add => AddHandler(NodesMovedEvent, value);
        remove => RemoveHandler(NodesMovedEvent, value);
    }

    public DocumentTreeView()
    {
        InitializeComponent();

        DropHandler = new DocumentTreeViewDropHandler(this);
        DragHandler = new DocumentTreeViewDragHandler();
    }

    public IDragSource DragHandler { get; }
    public IDropTarget DropHandler { get; }

    private void TreeViewItem_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return || SelectedItems is not { Count: > 0 } || !SelectedItems.Any(n => n.IsEditable))
        {
            return;
        }

        var first = SelectedItems.First(n => n.IsEditable);

        if (first.IsEditing)
        {
            return;
        }

        first.IsEditing = true;

        e.Handled = true;
    }

    private void EditableTextBlock_OnTextChanged(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not EditableTextBlock editableTextBlock)
        {
            return;
        }

        OnNodeEdited(editableTextBlock.Text);

        // TODO: Is it possible to avoid this call?
        editableTextBlock.GetBindingExpression(EditableTextBlock.TextProperty)?.UpdateSource();
    }

    private void OnNodeEdited(string value)
    {
        RaiseEvent(new NodesEditedEventArgs(value));
    }
}
