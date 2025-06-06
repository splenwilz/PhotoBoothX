using System.Windows;

namespace Photobooth
{
    public partial class PasswordChangeDialog : Window
    {
        public string NewPassword { get; private set; } = string.Empty;

        public PasswordChangeDialog(string userType)
        {
            InitializeComponent();
            HeaderText.Text = $"Change {userType} Password";
            NewPasswordBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate passwords
            if (string.IsNullOrWhiteSpace(NewPasswordBox.Password))
            {
                ShowError("Password cannot be empty.");
                return;
            }

            if (NewPasswordBox.Password.Length < 4)
            {
                ShowError("Password must be at least 4 characters long.");
                return;
            }

            if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
            {
                ShowError("Passwords do not match.");
                return;
            }

            // Save the password and close dialog
            NewPassword = NewPasswordBox.Password;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = Visibility.Visible;
        }
    }
} 