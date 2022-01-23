using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HocrEditor.Controls;

 /// <summary>
    /// A user control for transitioning from a <see cref="TextBlock"/> to a <see cref="TextBox"/>.
    /// </summary>
    public partial class EditableTextBlock
    {
        #region Fields

        private string? originalValue;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EditableTextBlock"/> class.
        /// </summary>
        public EditableTextBlock()
        {
            Application.ResourceAssembly = Assembly.GetExecutingAssembly();

            InitializeComponent();
        }

        #endregion Constructors

        #region IsEditing (Dependency Property)

        /// <summary>
        /// The dependency property definition for the IsEditing property.
        /// </summary>
        public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
            "IsEditing", typeof(bool), typeof(EditableTextBlock),
            new UIPropertyMetadata(false, OnIsEditingChanged));

        /// <summary>
        /// Gets or sets the flag indicating if the control is in edit mode.
        /// </summary>
        public bool IsEditing
        {
            get => (bool)GetValue(IsEditingProperty);
            set => SetValue(IsEditingProperty, value);
        }

        /// <summary>
        /// Called when the IsEditing dependency property has changed.
        /// </summary>
        /// <param name="obj">The dependency object where the value has changed.</param>
        /// <param name="e">The <see cref="System.Windows.DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnIsEditingChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (obj is not EditableTextBlock editableTextBlock)
            {
                return;
            }

            if (!(bool)e.NewValue)
            {
                return;
            }

            // Store a copy of the original text.
            editableTextBlock.originalValue = editableTextBlock.Text;

            // Focus and select all of the text.
            editableTextBlock.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                editableTextBlock.TextBox.Focus();
                editableTextBlock.TextBox.SelectAll();
            }));
        }

        #endregion IsEditing (Dependency Property)

        #region Text (Dependency Property)

        /// <summary>
        /// The dependency property definition for the Text property.
        /// </summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(EditableTextBlock),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        #endregion Text (Dependency Property)

        #region Methods

        /// <summary>
        /// Handles the <see cref="OnMouseDoubleClick"/> event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            IsEditing = !IsEditing;
        }

        /// <summary>
        /// Handles the <see cref="OnKeyDown"/> event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    if (IsEditing)
                    {
                        OnTextChanged(new RoutedEventArgs(TextChangedEvent, this));
                    }

                    IsEditing = false;

                    e.Handled = true;
                    break;

                case Key.Escape:
                    Text = originalValue ?? string.Empty;
                    IsEditing = false;
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Handles the <see cref="OnLostFocus"/> event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (IsEditing)
            {
                OnTextChanged(new RoutedEventArgs(TextChangedEvent, this));
            }

            IsEditing = false;
        }

        #endregion Methods

        #region Public Events

        /// <summary>
        /// Event for "Text has changed"
        /// </summary>
        public static readonly RoutedEvent TextChangedEvent = EventManager.RegisterRoutedEvent(
            "TextChanged", // Event name
            RoutingStrategy.Bubble, //
            typeof(RoutedEventHandler), //
            typeof(EditableTextBlock)); //

        /// <summary>
        /// Event fired from this text box when its inner content
        /// has been changed.
        /// </summary>
        /// <remarks>
        /// The event itself is defined on TextEditor.
        /// </remarks>
        public event RoutedEventHandler TextChanged
        {
            add => AddHandler(TextChangedEvent, value);
            remove => RemoveHandler(TextChangedEvent, value);
        }

        /// <summary>
        /// Called when content in this Control changes.
        /// Raises the TextChanged event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTextChanged(RoutedEventArgs e)
        {
            RaiseEvent(e);
        }

        #endregion
    }
