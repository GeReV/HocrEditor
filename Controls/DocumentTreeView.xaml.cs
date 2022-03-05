using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GongSolutions.Wpf.DragDrop;
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
        nameof(NodesEdited),
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
        nameof(NodesMoved),
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

        // This stack will be used to keep track of the "path" of nodes from the currently-edited node to its closest
        // selected parent.
        var stack = new Stack<HocrNodeViewModel>();

        var parent = editingNode;

        // Stack the parents. It is assumed that one of the parent nodes is selected.
        while (parent != null && !SelectedItems.Contains(parent))
        {
            stack.Push(parent);

            parent = parent.Parent;
        }

        // This should never be reached. Parent should always terminate before reaching the root node.
        ArgumentNullException.ThrowIfNull(parent);

        // Find the item from the parent node. This is done recursively by this function.
        // To receive a result from this function, the item for the passed object  must be visible
        // (or possibly to have been visible at one time, as TreeView appears to load items lazily).
        //
        // It is assumed that the parent has a matching item that is selected, and therefore visible.
        var treeViewItem = TreeView.FindChildFromItem(parent);

        // The stack now contains a "path" from the parent node down to the target node.
        // As we expand the subtree of the current treeViewItem, we can get the next relevant child item.
        // By repeating this for each item in the stack, we traverse down the tree view until
        // the target item is visible.
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
                    throw new InvalidOperationException($"{nameof(treeViewItem)} is expected to have a {nameof(EditableTextBlock)} child.");
                }

                // At this point, the target tree view item should be visible and we can start editing it.
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


        OnNodeEdited(editableTextBlock.Text);

        if (editingNode != null)
        {
            editingNode!.IsEditing = false;
            editingNode = null;
        }

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
