using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public partial class DocumentTreeView : UserControl
{
    public class NodeEditedEventArgs : EventArgs
    {
        public string Value { get; }

        public NodeEditedEventArgs(string value)
        {
            Value = value;
        }
    }

    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems),
        typeof(IList<HocrNodeViewModel>),
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

    public IList<HocrNodeViewModel>? SelectedItems
    {
        get => (IList<HocrNodeViewModel>?)GetValue(SelectedItemsProperty);
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

    public event EventHandler<NodeEditedEventArgs>? NodeEdited;

    public DocumentTreeView()
    {
        InitializeComponent();
    }

    private void TreeViewItem_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return || SelectedItems is not { Count: > 0 } || !SelectedItems.Any(n => n.IsEditable))
        {
            return;
        }

        var first = SelectedItems.First(n => n.IsEditable);

        if (!first.IsEditing)
        {
            first.IsEditing = true;
        }
        else if (e.OriginalSource is EditTextBox textBox)
        {
            OnNodeEdited((string)(textBox.GetValue(TextBox.TextProperty) ?? string.Empty));

            first.IsEditing = false;
        }

        e.Handled = true;
    }

    protected void OnNodeEdited(string value)
    {
        NodeEdited?.Invoke(this, new NodeEditedEventArgs(value));
    }
}
