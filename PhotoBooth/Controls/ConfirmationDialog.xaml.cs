using System;
using System.Windows;

namespace Photobooth.Controls
{
    public partial class ConfirmationDialog : Window
    {
        public bool Result { get; private set; } = false;

        public ConfirmationDialog()
        {
            InitializeComponent();
        }

        public void SetContent(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            TitleText.Text = title;
            MessageText.Text = message;
            ConfirmButton.Content = confirmText;
            CancelButton.Content = cancelText;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        /// <summary>
        /// Show a confirmation dialog with modern styling
        /// </summary>
        public static bool ShowConfirmation(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", Window? owner = null)
        {
            var dialog = new ConfirmationDialog();
            dialog.SetContent(title, message, confirmText, cancelText);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else
            {
                // Try to find the main window
                dialog.Owner = Application.Current.MainWindow;
            }
            
            dialog.ShowDialog();
            return dialog.Result;
        }

        /// <summary>
        /// Quick helper for delete confirmations
        /// </summary>
        public static bool ShowDeleteConfirmation(string itemName, string itemType = "item", Window? owner = null)
        {
            return ShowConfirmation(
                $"Delete {itemType}",
                $"Are you sure you want to delete '{itemName}'?\n\nThis action cannot be undone.",
                "Delete",
                "Cancel",
                owner
            );
        }
    }
} 