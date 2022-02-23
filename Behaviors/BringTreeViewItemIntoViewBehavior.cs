using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HocrEditor.ViewModels;
using Microsoft.Xaml.Behaviors;

namespace HocrEditor.Behaviors;

/// <summary>
/// Class implements an attached behaviour to bring a selected TreeViewItem in TreeView
/// into view when selection is driven by the viewmodel (not the user).
/// </summary>
/// <example>
/// <code>
/// &lt;i:Interaction.Behaviors>
///     &lt;behav:BringTreeViewItemIntoViewBehavior SelectedItems="{Binding SelectedItems}" />
/// &lt;/i:Interaction.Behaviors>
/// </code>
/// </example>
/// <remarks>
/// Adapted from BringVirtualTreeViewItemIntoViewBehavior from LazyLoading_VirtualizedTreeViewDemo:
/// https://www.codeproject.com/Articles/1206685/Advanced-WPF-TreeViews-Part-of-n
///
/// Additional sources:
/// http://stackoverflow.com/q/183636/46635
/// http://code.msdn.microsoft.com/Changing-selection-in-a-6a6242c8/sourcecode?fileId=18862&pathId=753647475
/// </remarks>
public class BringTreeViewItemIntoViewBehavior : Behavior<TreeView>
{
    #region SelectedItems (Public Dependency Property)

    /// <summary>
    /// The dependency property definition for the SelectedItems property.
    /// </summary>
    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        "SelectedItems",
        typeof(ISet<HocrNodeViewModel>),
        typeof(BringTreeViewItemIntoViewBehavior),
        new FrameworkPropertyMetadata(SelectedItemsChanged)
    );

    private static readonly Dictionary<(DependencyObject, INotifyCollectionChanged), NotifyCollectionChangedEventHandler>
        SelectedItemsChangedHandlers = new();

    private static void SelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged oldCollection && SelectedItemsChangedHandlers.Remove((d, oldCollection), out var oldHandler))
        {
            oldCollection.CollectionChanged -= oldHandler;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            NotifyCollectionChangedEventHandler newHandler = (_, args) =>
                SelectedItemsOnCollectionChanged((BringTreeViewItemIntoViewBehavior)d, (ICollection<HocrNodeViewModel>)newCollection, args);

            SelectedItemsChangedHandlers.Add((d, newCollection), newHandler);

            newCollection.CollectionChanged += newHandler;
        }
    }

    private static void SelectedItemsOnCollectionChanged(
        BringTreeViewItemIntoViewBehavior behavior,
        ICollection<HocrNodeViewModel> collection,
        NotifyCollectionChangedEventArgs e
    )
    {
        if (e.NewItems == null)
        {
            return;
        }

        var tree = behavior.AssociatedObject;

        behavior.Dispatcher.BeginInvoke(
            () =>
            {
                foreach (var node in e.NewItems.Cast<HocrNodeViewModel>())
                {
                    var nodePath = node.Ascendants.Reverse().Append(node).Skip(1);

                    ItemsControl currentParent = tree;

                    foreach (var parent in nodePath)
                    {
                        // first try the easy way
                        var newParent = currentParent.ItemContainerGenerator.ContainerFromItem(parent) as TreeViewItem;
                        if (newParent == null)
                        {
                            // if this failed, it's probably because of virtualization, and we will have to do it the hard way.
                            // this code is influenced by TreeViewItem.ExpandRecursive decompiled code, and the MSDN sample at http://code.msdn.microsoft.com/Changing-selection-in-a-6a6242c8/sourcecode?fileId=18862&pathId=753647475
                            // see also the question at http://stackoverflow.com/q/183636/46635
                            currentParent.ApplyTemplate();

                            if (currentParent.Template.FindName(
                                    "ItemsHost",
                                    currentParent
                                ) is ItemsPresenter itemsPresenter)
                            {
                                itemsPresenter.ApplyTemplate();
                            }
                            else
                            {
                                currentParent.UpdateLayout();
                            }

                            var panel = GetItemsHost(currentParent);

                            CallEnsureGenerator(panel);

                            var index = currentParent.Items.IndexOf(parent);
                            if (index < 0)
                            {
                                // This is raised when the item in the path array is not part of the tree collection
                                // This can be tricky, because Binding an ObservableDictionary to the treeview will
                                // require that we need an array of KeyValuePairs<K,T>[] here :-(
#if DEBUG
                                throw new InvalidOperationException($"Node '{parent}' cannot be fount in container");
#else
                        // Use your favourite logger here since the exception will otherwise kill the application
                        System.Console.WriteLine("Node '" + node + "' cannot be fount in container");
#endif
                            }

                            // virtualizingPanel?.BringIndexIntoViewPublic(index);
                            newParent = currentParent.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
                        }

                        if (newParent == null)
                        {
#if DEBUG
                            throw new InvalidOperationException(
                                $"Tree view item cannot be found or created for node '{parent}'"
                            );
#else
                    // Use your favourite logger here since the exception will otherwise kill the application
                    System.Console.WriteLine("Node '" + node + "' cannot be fount in container");
#endif
                        }

                        if (parent == node)
                        {
                            // Only bring item into view when it's a single selection.
                            if (collection.Count == 1)
                            {
                                newParent.BringIntoView();
                            }
                            break;
                        }

                        // Make sure nodes (except for last child node) are expanded to make children visible
                        newParent.IsExpanded = true;

                        currentParent = newParent;
                    }
                }
            },
            DispatcherPriority.ContextIdle
        );
    }

    /// <summary>
    /// Gets or sets the selected items.
    /// </summary>
    public ISet<HocrNodeViewModel> SelectedItems
    {
        get => (ISet<HocrNodeViewModel>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    #endregion SelectedItems (Public Dependency Property)

    #region Methods

    #region Functions to get internal members using reflection

    // Some functionality we need is hidden in internal members, so we use reflection to get them

    #region ItemsControl.ItemsHost

    private static readonly PropertyInfo? ItemsHostPropertyInfo = typeof(ItemsControl).GetProperty(
        "ItemsHost",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static Panel? GetItemsHost(ItemsControl itemsControl) =>
        ItemsHostPropertyInfo?.GetValue(itemsControl, null) as Panel;

    #endregion ItemsControl.ItemsHost

    #region Panel.EnsureGenerator

    private static readonly MethodInfo? EnsureGeneratorMethodInfo = typeof(Panel).GetMethod(
        "EnsureGenerator",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static void CallEnsureGenerator(Panel? panel)
    {
        EnsureGeneratorMethodInfo?.Invoke(panel, null);
    }

    #endregion Panel.EnsureGenerator

    #endregion Functions to get internal members using reflection

    #endregion
}
