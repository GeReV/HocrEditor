using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HocrEditor.Helpers;
using Microsoft.Xaml.Behaviors;

namespace HocrEditor.Behaviors;

public class TreeViewDragDropBehavior : Behavior<TreeView>
{
    #region Fields

    private TreeViewItem? dragCandidate;
    private Point? dragStartPoint;

    #endregion Fields

    #region Dependency Properties

    public event EventHandler<TreeViewDropEventArgs>? Drop;

    /// <summary>
    /// The dependency property definition for the SelectedItems property.
    /// </summary>
    public static readonly DependencyProperty DataObjectProperty = DependencyProperty.Register(
        "DataObject",
        typeof(object),
        typeof(TreeViewDragDropBehavior)
    );

    public object DataObject
    {
        get => GetValue(DataObjectProperty);
        set => SetValue(DataObjectProperty, value);
    }

    #endregion

    #region Behavior Methods

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.AllowDrop = true;
        AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
        AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        AssociatedObject.PreviewMouseUp += OnPreviewMouseUp;
        AssociatedObject.DragEnter += OnDragEvent;
        AssociatedObject.DragOver += OnDragEvent;
        AssociatedObject.DragLeave += OnDragLeave;
        AssociatedObject.Drop += OnDrop;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.AllowDrop = false;
        AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
        AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
        AssociatedObject.PreviewMouseUp -= OnPreviewMouseUp;
        AssociatedObject.DragEnter -= OnDragEvent;
        AssociatedObject.DragOver -= OnDragEvent;
        AssociatedObject.DragLeave -= OnDragLeave;
        AssociatedObject.Drop -= OnDrop;
    }

    #endregion

    #region Event Handlers

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            dragCandidate = FindParentTreeViewItem(e.OriginalSource);
            dragStartPoint = e.GetPosition(null);
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (dragCandidate == null || !dragStartPoint.HasValue)
        {
            return;
        }

        var delta = dragStartPoint.Value - e.GetPosition(null);

        if (Math.Abs(delta.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) <= SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        dragCandidate.SetValue(DragDropAttachedProperties.IsBeingDraggedProperty, true);

        DragDrop.DoDragDrop(
            dragCandidate,
            new DataObject(typeof(object), DataObject),
            DragDropEffects.Move
        );

        dragCandidate.SetValue(DragDropAttachedProperties.IsBeingDraggedProperty, false);

        dragCandidate = null;
        dragStartPoint = null;
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        dragCandidate = null;
        dragStartPoint = null;
    }

    private static void OnDragEvent(object sender, DragEventArgs e)
    {
        var target = FindParentTreeViewItem(e.OriginalSource);
        if (target != null && e.Data.GetDataPresent(typeof(object)))
        {
            var sourceData = e.Data.GetData(typeof(object));
            var targetData = target.DataContext;

            if (sourceData != null && targetData != null && sourceData != targetData)
            {
                switch (GetDropPosition(e, target))
                {
                    case DropPosition.Above:
                        target.SetValue(DragDropAttachedProperties.IsDropAboveTargetProperty, true);
                        target.SetValue(DragDropAttachedProperties.IsDropBelowTargetProperty, false);
                        target.SetValue(DragDropAttachedProperties.IsDropOnTargetProperty, false);
                        break;

                    case DropPosition.Below:
                        target.SetValue(DragDropAttachedProperties.IsDropAboveTargetProperty, false);
                        target.SetValue(DragDropAttachedProperties.IsDropBelowTargetProperty, true);
                        target.SetValue(DragDropAttachedProperties.IsDropOnTargetProperty, false);
                        break;

                    case DropPosition.On:
                        target.SetValue(DragDropAttachedProperties.IsDropAboveTargetProperty, false);
                        target.SetValue(DragDropAttachedProperties.IsDropBelowTargetProperty, false);
                        target.SetValue(DragDropAttachedProperties.IsDropOnTargetProperty, true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }
        }

        // Not a valid drop target.
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        var target = FindParentTreeViewItem(e.OriginalSource);
        if (target == null)
        {
            return;
        }

        target.SetValue(DragDropAttachedProperties.IsDropAboveTargetProperty, false);
        target.SetValue(DragDropAttachedProperties.IsDropBelowTargetProperty, false);
        target.SetValue(DragDropAttachedProperties.IsDropOnTargetProperty, false);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(object)))
        {
            return;
        }

        var target = FindParentTreeViewItem(e.OriginalSource);
        if (target == null)
        {
            return;
        }

        var sourceData = e.Data.GetData(typeof(object));
        var targetData = target.DataContext;

        if (sourceData == null || targetData == null || sourceData == targetData)
        {
            return;
        }

        RaiseDropEvent(new TreeViewDropEventArgs(sourceData, targetData, GetDropPosition(e, target)));

        target.SetValue(DragDropAttachedProperties.IsDropAboveTargetProperty, false);
        target.SetValue(DragDropAttachedProperties.IsDropBelowTargetProperty, false);
        target.SetValue(DragDropAttachedProperties.IsDropOnTargetProperty, false);

        e.Handled = true;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Attempts to find the parent ListBoxItem from the specified event source.
    /// </summary>
    /// <param name="eventSource">The event source.</param>
    /// <returns>The parent ListBoxItem, otherwise null.</returns>
    private static TreeViewItem? FindParentTreeViewItem(object eventSource)
    {
        if (eventSource is not DependencyObject source)
        {
            return null;
        }

        var treeViewItem = source.FindVisualAncestor<TreeViewItem>();

        return treeViewItem;
    }

    /// <summary>
    /// Determines the drop position for the specified drag event and the drop target.
    /// </summary>
    /// <param name="e">The <see cref="DragEventArgs" /> instance containing the event data.</param>
    /// <param name="target">The target.</param>
    /// <returns>The drop position.</returns>
    private static DropPosition GetDropPosition(DragEventArgs e, FrameworkElement target)
    {
        var dropPoint = e.GetPosition(target);
        var targetHeight = target.ActualHeight;

        var isTopThird = dropPoint.Y <= targetHeight / 3;
        var isBottomThird = dropPoint.Y > targetHeight * 2 / 3;

        if (isTopThird)
        {
            return DropPosition.Above;
        }

        return isBottomThird ? DropPosition.Below : DropPosition.On;
    }

    #endregion Methods

    protected virtual void RaiseDropEvent(TreeViewDropEventArgs e)
    {
        Drop?.Invoke(this, e);
    }
}
