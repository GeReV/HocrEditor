using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GongSolutions.Wpf.DragDrop;
using HocrEditor.Core;
using HocrEditor.Helpers;
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

    private HocrNodeViewModel? editingNode;

    private void TreeViewItem_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return || editingNode != null || SelectedItems == null)
        {
            return;
        }

        editingNode = SelectionHelper.SelectEditable(SelectedItems);

        if (editingNode is not { IsEditing: false })
        {
            return;
        }

        var stack = new Stack<HocrNodeViewModel>();

        var parent = editingNode;

        while (parent != null && !SelectedItems.Contains(parent))
        {
            stack.Push(parent);

            parent = parent.Parent;
        }

        if (parent == null)
        {
            return;
        }

        var treeViewItem = TreeView.FindChildFromItem(parent);

        while (treeViewItem != null && stack.TryPop(out var item))
        {
            treeViewItem.ExpandSubtree();
            treeViewItem = (TreeViewItem)treeViewItem.ItemContainerGenerator.ContainerFromItem(item);
        }


        Dispatcher.InvokeAsync(
            () =>
            {
                if (treeViewItem?.FindVisualChild<EditableTextBlock>() is not { } editableTextBlock)
                {
                    return;
                }

                editingNode.IsEditing = true;
                editableTextBlock.IsEditing = true;
            },
            DispatcherPriority.Input
        );

        e.Handled = true;
    }

    private void EditableTextBlock_OnTextChanged(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not EditableTextBlock editableTextBlock)
        {
            return;
        }

        Ensure.IsNotNull(nameof(editingNode), editingNode);

        OnNodeEdited(editableTextBlock.Text);

        editingNode!.IsEditing = false;
        editingNode = null;

        editableTextBlock.IsEditing = false;

        // TODO: Is it possible to avoid this call?
        editableTextBlock.GetBindingExpression(EditableTextBlock.TextProperty)?.UpdateSource();
    }

    private void EditableTextBlock_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (editingNode == null)
        {
            return;
        }

        editingNode.IsEditing = false;
        editingNode = null;

        if (this.FindVisualChild<EditableTextBlock>() is { } editableTextBlock)
        {
            editableTextBlock.IsEditing = false;
        }
    }

    private void OnNodeEdited(string value)
    {
        RaiseEvent(
            new NodesEditedEventArgs(
                NodesEditedEvent,
                this,
                SelectionHelper.SelectAllEditable(SelectedItems ?? Enumerable.Empty<HocrNodeViewModel>()),
                value
            )
        );
    }
}
