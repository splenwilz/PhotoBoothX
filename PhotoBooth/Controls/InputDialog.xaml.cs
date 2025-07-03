using System;
using System.Windows;
using System.Windows.Input;

namespace Photobooth.Controls
{
    public partial class InputDialog : Window
    {
        public bool Result { get; private set; } = false;
        public string InputText { get; private set; } = "";

        public InputDialog()
        {
            InitializeComponent();
            InputTextBox.Focus();
        }

        public void SetContent(string title, string message, string defaultValue = "")
        {
            TitleText.Text = title;
            MessageText.Text = message;
            InputTextBox.Text = defaultValue;
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                MessageBox.Show("Please enter a valid name.", "Invalid Input", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                InputTextBox.Focus();
                return;
            }

            InputText = InputTextBox.Text.Trim();
            Result = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }

        /// <summary>
        /// Show an input dialog
        /// </summary>
        public static string? ShowInputDialog(string title, string message, string defaultValue = "", Window? owner = null)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            
            var dialog = new InputDialog();
            dialog.SetContent(title, message, defaultValue);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            
            dialog.ShowDialog();
            return dialog.Result ? dialog.InputText : null;
        }
    }
} 