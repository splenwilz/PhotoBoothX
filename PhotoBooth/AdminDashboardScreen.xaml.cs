using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using Photobooth.Models;
using Photobooth.Services;
using Photobooth.Controls;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Controls.Primitives;

namespace Photobooth
{
    /// <summary>
    /// Admin dashboard screen with comprehensive management features
    /// Supports both Master and User access levels with appropriate restrictions
    /// </summary>
    public partial class AdminDashboardScreen : UserControl
    {
        #region Events

        /// <summary>
        /// Fired when user wants to exit admin mode
        /// </summary>
        public event EventHandler? ExitAdminRequested;

        #endregion

        #region Private Fields

        private AdminAccessLevel _currentAccessLevel = AdminAccessLevel.None;
        private Dictionary<string, Grid> _tabContentPanels = new();
        private Dictionary<string, Button> _tabButtons = new();
        private readonly IDatabaseService _databaseService;

        // Sample sales data - in real implementation, this would come from database
        private SalesData _salesData = new();

        // Print status tracking
        private int _printsRemaining = 650;
        private const int PRINTS_PER_ROLL = 700;
        
        // Current user and logo tracking
        private string? _currentUserId = null;
        private string? _currentLogoPath = null;
        private string _currentOperationMode = "Coin"; // Default to Coin Operated

        // Product management
        private List<Product> _products = new();
        private bool _isLoadingProducts = false;
        private bool _hasUnsavedChanges = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the admin dashboard screen
        /// </summary>
        public AdminDashboardScreen(IDatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            InitializeTabMapping();
            InitializeSalesData();
        }

        /// <summary>
        /// Initialize the tab content and button mappings
        /// </summary>
        private void InitializeTabMapping()
        {
            _tabContentPanels.Clear();
            _tabContentPanels.Add("Sales", SalesTabContent);
            _tabContentPanels.Add("Settings", SettingsTabContent);
            _tabContentPanels.Add("Products", ProductsTabContent);
            _tabContentPanels.Add("Templates", TemplatesTabContent);
            _tabContentPanels.Add("Diagnostics", DiagnosticsTabContent);
            _tabContentPanels.Add("System", SystemTabContent);
            _tabContentPanels.Add("Credits", CreditsTabContent);

            _tabButtons.Clear();
            _tabButtons.Add("Sales", SalesTab);
            _tabButtons.Add("Settings", SettingsTab);
            _tabButtons.Add("Products", ProductsTab);
            _tabButtons.Add("Templates", TemplatesTab);
            _tabButtons.Add("Diagnostics", DiagnosticsTab);
            _tabButtons.Add("System", SystemTab);
            _tabButtons.Add("Credits", CreditsTab);
        }

        /// <summary>
        /// Initialize sample sales data
        /// </summary>
        private void InitializeSalesData()
        {
            _salesData.Today = new SalesPeriod { Amount = 127.50m, Transactions = 25 };
            _salesData.Week = new SalesPeriod { Amount = 892.25m, Transactions = 178 };
            _salesData.Month = new SalesPeriod { Amount = 3456.75m, Transactions = 689 };
            _salesData.Year = new SalesPeriod { Amount = 41481.00m, Transactions = 8296 };

            UpdateSalesDisplay();
            UpdatePrintStatus();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the access level and configure UI accordingly
        /// </summary>
        public async System.Threading.Tasks.Task SetAccessLevel(AdminAccessLevel accessLevel, string? userId = null)
        {
            Console.WriteLine($"SetAccessLevel called: accessLevel={accessLevel}, userId='{userId}'");
            _currentAccessLevel = accessLevel;
            _currentUserId = userId;
            Console.WriteLine($"SetAccessLevel: _currentUserId set to '{_currentUserId}'");
            UpdateAccessLevelDisplay();
            await LoadInitialData();
        }

        /// <summary>
        /// Refresh all sales data from database
        /// </summary>
        public async void RefreshSalesData()
        {
            try
            {
                await LoadSalesData();
                await LoadSupplyStatus();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing sales data: {ex.Message}");
            }
        }

        #endregion

        #region Access Control

        /// <summary>
        /// Enable all admin features for master access
        /// </summary>
        private void EnableAllFeatures()
        {
            // All tabs available for master access
            foreach (var button in _tabButtons.Values)
            {
                button.Visibility = Visibility.Visible;
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// Restrict features for user-level access
        /// </summary>
        private void RestrictToUserFeatures()
        {
            // User access only gets Sales tab
            var allowedTabs = new[] { "Sales" };

            foreach (var kvp in _tabButtons)
            {
                if (Array.Exists(allowedTabs, tab => tab == kvp.Key))
                {
                    kvp.Value.Visibility = Visibility.Visible;
                    kvp.Value.IsEnabled = true;
                }
                else
                {
                    kvp.Value.Visibility = Visibility.Collapsed;
                }
            }

            // Show only sales tab content
            ShowTabContent("Sales");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle exit admin button click
        /// </summary>
        private void ExitAdminButton_Click(object sender, RoutedEventArgs e)
        {
            ExitAdminRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handle tab button clicks
        /// </summary>
        private async void TabButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset all tab buttons
            SalesTab.Tag = null;
            SettingsTab.Tag = null;
            ProductsTab.Tag = null;
            TemplatesTab.Tag = null;
            DiagnosticsTab.Tag = null;
            SystemTab.Tag = null;
            CreditsTab.Tag = null;

            // Set clicked tab as active
            if (sender is Button clickedTab)
            {
                clickedTab.Tag = "Active";

                // Hide all tab contents
                SalesTabContent.Visibility = Visibility.Collapsed;
                SettingsTabContent.Visibility = Visibility.Collapsed;
                ProductsTabContent.Visibility = Visibility.Collapsed;
                TemplatesTabContent.Visibility = Visibility.Collapsed;
                DiagnosticsTabContent.Visibility = Visibility.Collapsed;
                SystemTabContent.Visibility = Visibility.Collapsed;
                CreditsTabContent.Visibility = Visibility.Collapsed;

                // Show selected tab content and update breadcrumb
                switch (clickedTab.Name)
                {
                    case "SalesTab":
                        SalesTabContent.Visibility = Visibility.Visible;
                        BreadcrumbText.Text = "Sales";
                        RefreshSalesData();
                        break;
                    case "SettingsTab":
                        SettingsTabContent.Visibility = Visibility.Visible;
                        BreadcrumbText.Text = "Settings";
                        break;
                    case "ProductsTab":
                        ProductsTabContent.Visibility = Visibility.Visible;
                        BreadcrumbText.Text = "Products";
                        await LoadProductsData();
                        break;
                    case "TemplatesTab":
                        TemplatesTabContent.Visibility = Visibility.Visible;
                        BreadcrumbText.Text = "Templates";
                        break;
                    case "DiagnosticsTab":
                        DiagnosticsTabContent.Visibility = Visibility.Visible;
                        BreadcrumbText.Text = "Diagnostics";
                        break;
                    case "SystemTab":
                        SystemTabContent.Visibility = Visibility.Visible;
                        BreadcrumbText.Text = "System";
                        break;
                    case "CreditsTab":
                        CreditsTabContent.Visibility = Visibility.Visible;
                        BreadcrumbText.Text = "Credits";
                        break;
                }
            }
        }

        /// <summary>
        /// Handle generate report button click
        /// </summary>
        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var startDate = StartDatePicker.SelectedDate;
                var endDate = EndDatePicker.SelectedDate;

                if (!startDate.HasValue || !endDate.HasValue)
                {
                    NotificationService.Quick.Info("Please select both start and end dates.");
                    return;
                }

                if (startDate > endDate)
                {
                    NotificationService.Quick.Warning("Start date cannot be after end date.");
                    return;
                }

                var totalSales = await CalculatePeriodSales(startDate.Value, endDate.Value);

                var message = $"Sales Report\n" +
                             $"Period: {startDate.Value:MM/dd/yyyy} - {endDate.Value:MM/dd/yyyy}\n" +
                             $"Total Revenue: ${totalSales:F2}";

                NotificationService.Instance.ShowInfo("Sales Report", message, 8);
            }
            catch (Exception ex)
            {
                NotificationService.Quick.Error($"Error generating report: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle simulate new roll button click
        /// </summary>
        private async void SimulateNewRoll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Simulate installing a new roll (reset to full capacity)
                var result = await _databaseService.UpdatePrintSupplyAsync(SupplyType.Paper, 700);
                if (result.Success)
                {
                    await LoadSupplyStatus();
                    NotificationService.Quick.SupplyUpdated();
                }
                else
                {
                    NotificationService.Quick.Error($"Failed to update supply: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                NotificationService.Quick.Error($"Error simulating new roll: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle save settings button click
        /// </summary>
        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logging to see current user ID
                Console.WriteLine($"SaveSettings_Click: _currentUserId = '{_currentUserId}'");
                
                if (string.IsNullOrEmpty(_currentUserId))
                {
                    NotificationService.Quick.Warning("User session invalid, please login again.");
                    return;
                }
                
                // Show loading state
                if (SaveSettingsButton != null)
                {
                    SaveSettingsButton.IsEnabled = false;
                    SaveSettingsButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                        new TextBlock { Text = "⏳", FontSize = 14, Margin = new Thickness(0,0,8,0) },
                        new TextBlock { Text = "Saving...", FontSize = 14 }
                    }};
                }

                // Save Business Information
                await SaveBusinessInformation();

                // Save Pricing Settings
                await SaveOperationModeSettings();

                // Save System Preferences
                await SaveSystemPreferences();

                // Success message
                NotificationService.Quick.SettingsSaved();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveSettings error: {ex}");
                NotificationService.Quick.SettingsError(ex.Message);
            }
            finally
            {
                // Restore button state
                if (SaveSettingsButton != null)
                {
                    SaveSettingsButton.IsEnabled = true;
                    SaveSettingsButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                        new TextBlock { Text = "💾", FontSize = 14, Margin = new Thickness(0,0,8,0) },
                        new TextBlock { Text = "Save Settings", FontSize = 14 }
                    }};
                }
            }
        }

        /// <summary>
        /// Save business information settings
        /// </summary>
        private async Task SaveBusinessInformation()
        {
            try
            {
                Console.WriteLine($"SaveBusinessInformation: _currentUserId = '{_currentUserId}'");
                
                // Get business name and location from UI elements
                var businessName = BusinessNameTextBox?.Text ?? "Downtown Event Center";
                var location = LocationTextBox?.Text ?? "Main Street, Downtown";
                var showLogo = ShowLogoToggle?.IsChecked == true;
                
                Console.WriteLine($"SaveBusinessInformation: Name='{businessName}', Location='{location}', ShowLogo={showLogo}, LogoPath='{_currentLogoPath}'");

                // Check if business info already exists
                var existingBusinessInfo = await _databaseService.GetAllAsync<BusinessInfo>();
                
                if (existingBusinessInfo.Success && existingBusinessInfo.Data?.Count > 0)
                {
                    Console.WriteLine("SaveBusinessInformation: Updating existing business info");
                    // Update existing
                    var businessInfo = existingBusinessInfo.Data[0];
                    businessInfo.BusinessName = businessName;
                    businessInfo.Address = location;
                    businessInfo.ShowLogoOnPrints = showLogo;
                    businessInfo.LogoPath = _currentLogoPath;
                    businessInfo.UpdatedAt = DateTime.Now;
                    businessInfo.UpdatedBy = _currentUserId; // Use string directly

                    var updateResult = await _databaseService.UpdateAsync(businessInfo);
                    Console.WriteLine($"Business info update result: Success={updateResult.Success}, Error='{updateResult.ErrorMessage}'");
                    
                    if (!updateResult.Success)
                    {
                        throw new Exception(updateResult.ErrorMessage ?? "Unknown error updating business info");
                    }
                }
                else
                {
                    Console.WriteLine("SaveBusinessInformation: Creating new business info");
                    // Create new
                    var businessInfo = new BusinessInfo
                    {
                        BusinessName = businessName,
                        Address = location,
                        ShowLogoOnPrints = showLogo,
                        LogoPath = _currentLogoPath,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = _currentUserId // Use string directly
                    };

                    var insertResult = await _databaseService.InsertAsync(businessInfo);
                    Console.WriteLine($"Business info insert result: Success={insertResult.Success}, Error='{insertResult.ErrorMessage}'");
                    
                    if (!insertResult.Success)
                    {
                        throw new Exception(insertResult.ErrorMessage ?? "Unknown error creating business info");
                    }
                }
                
                Console.WriteLine("SaveBusinessInformation: Completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveBusinessInformation error: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Save operation mode settings to database (products now managed in Products tab)
        /// </summary>
        private async Task SaveOperationModeSettings()
        {
            try
            {
                Console.WriteLine($"SaveOperationModeSettings: _currentUserId = '{_currentUserId}'");
                
                // NOTE: Product pricing and enabled states are now managed in the Products tab
                // Only save operation mode here (Coin vs Free) - this is system-wide setting
                var result = await _databaseService.SetSettingValueAsync("System", "Mode", _currentOperationMode, _currentUserId);
                Console.WriteLine($"Mode save result: Success={result.Success}, Error='{result.ErrorMessage}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveOperationModeSettings error: {ex}");
                throw new Exception($"Failed to save operation mode settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Save system preferences to database
        /// </summary>
        private async Task SaveSystemPreferences()
        {
            try
            {
                Console.WriteLine($"SaveSystemPreferences: _currentUserId = '{_currentUserId}'");
                
                // Get values from UI elements
                var volume = (int)(VolumeSlider?.Value ?? 75);
                var cameraFlash = CameraFlashToggle?.IsChecked == true;
                var maintenanceMode = MaintenanceModeToggle?.IsChecked == true;
                var rfidEnabled = RFIDDetectionToggle?.IsChecked == true;
                var seasonalTemplates = SeasonalTemplatesToggle?.IsChecked == true;
                
                Console.WriteLine($"SaveSystemPreferences: Volume={volume}, Flash={cameraFlash}, Maintenance={maintenanceMode}, RFID={rfidEnabled}, Seasonal={seasonalTemplates}");
                
                // Save system preferences
                var result1 = await _databaseService.SetSettingValueAsync("System", "Volume", volume, _currentUserId);
                Console.WriteLine($"Volume save result: Success={result1.Success}, Error='{result1.ErrorMessage}'");
                
                var result2 = await _databaseService.SetSettingValueAsync("System", "LightsEnabled", cameraFlash, _currentUserId);
                Console.WriteLine($"LightsEnabled save result: Success={result2.Success}, Error='{result2.ErrorMessage}'");
                
                var result3 = await _databaseService.SetSettingValueAsync("System", "MaintenanceMode", maintenanceMode, _currentUserId);
                Console.WriteLine($"MaintenanceMode save result: Success={result3.Success}, Error='{result3.ErrorMessage}'");
                
                var result4 = await _databaseService.SetSettingValueAsync("RFID", "Enabled", rfidEnabled, _currentUserId);
                Console.WriteLine($"RFID Enabled save result: Success={result4.Success}, Error='{result4.ErrorMessage}'");
                
                var result5 = await _databaseService.SetSettingValueAsync("Seasonal", "AutoTemplates", seasonalTemplates, _currentUserId);
                Console.WriteLine($"AutoTemplates save result: Success={result5.Success}, Error='{result5.ErrorMessage}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveSystemPreferences error: {ex}");
                throw new Exception($"Failed to save system preferences: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle export analytics button click
        /// </summary>
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Implement analytics export functionality
                // For now, show a confirmation message
                var result = MessageBox.Show("Export analytics data to CSV file?", "Export Analytics",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Simulate export process
                    var exportData = await GenerateAnalyticsExport();

                    NotificationService.Instance.ShowSuccess("Export Complete", 
                        $"Analytics data exported successfully!\n\nExported:\n{exportData}", 10);
                }
            }
            catch (Exception ex)
            {
                NotificationService.Quick.Error($"Error exporting analytics: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate analytics export data (placeholder implementation)
        /// </summary>
        private async System.Threading.Tasks.Task<string> GenerateAnalyticsExport()
        {
            // Simulate data gathering
            await System.Threading.Tasks.Task.Delay(500);

            var exportSummary = $"- Today: ${TodaySales.Text}\n" +
                               $"- This Week: ${WeekSales.Text}\n" +
                               $"- This Month: ${MonthSales.Text}\n" +
                               $"- This Year: ${YearSales.Text}\n" +
                               $"- Prints Remaining: {PrintsRemainingText.Text}";

            return exportSummary;
        }

        // Upload Logo logic
        private void UploadLogoButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Logo Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Create logos directory if it doesn't exist
                    var logosDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logos");
                    Directory.CreateDirectory(logosDirectory);

                    // Generate unique filename to avoid conflicts
                    var fileExtension = Path.GetExtension(dialog.FileName);
                    var fileName = $"business_logo_{DateTime.Now:yyyyMMdd_HHmmss}{fileExtension}";
                    var destinationPath = Path.Combine(logosDirectory, fileName);

                    // Copy the file to our logos directory
                    File.Copy(dialog.FileName, destinationPath, true);

                    // Store the relative path for database storage
                    _currentLogoPath = Path.Combine("Logos", fileName);

                    // Load and display the image
                    var bitmap = new BitmapImage();
                    using (var stream = new FileStream(destinationPath, FileMode.Open, FileAccess.Read))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                    LogoImage.Source = bitmap;
                    LogoImage.Visibility = Visibility.Visible;
                    LogoPlaceholderText.Visibility = Visibility.Collapsed;

                    NotificationService.Quick.LogoUploaded();
                }
                catch (Exception ex)
                {
                    NotificationService.Quick.Error($"Failed to upload logo: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Toggle password change form visibility
        /// </summary>
        private void TogglePasswordForm_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordChangeFormBorder.Visibility == Visibility.Collapsed)
            {
                PasswordChangeFormBorder.Visibility = Visibility.Visible;
                TogglePasswordFormButton.Content = "Cancel";
            }
            else
            {
                CancelPasswordChange_Click(sender, e);
            }
        }

        /// <summary>
        /// Handle password user type radio button changes
        /// </summary>
        private void PasswordUserType_Changed(object sender, RoutedEventArgs e)
        {
            // Clear form when switching user types
            if (CurrentPasswordBox != null) CurrentPasswordBox.Password = "";
            if (NewPasswordBox != null) NewPasswordBox.Password = "";
            if (ConfirmPasswordBox != null) ConfirmPasswordBox.Password = "";
            HidePasswordError();
        }

        /// <summary>
        /// Cancel password change
        /// </summary>
        private void CancelPasswordChange_Click(object sender, RoutedEventArgs e)
        {
            PasswordChangeFormBorder.Visibility = Visibility.Collapsed;
            TogglePasswordFormButton.Content = "Change";
            
            // Clear form
            if (CurrentPasswordBox != null) CurrentPasswordBox.Password = "";
            if (NewPasswordBox != null) NewPasswordBox.Password = "";
            if (ConfirmPasswordBox != null) ConfirmPasswordBox.Password = "";
            HidePasswordError();
        }

        /// <summary>
        /// Save password change
        /// </summary>
        private async void SavePassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate form
                if (string.IsNullOrWhiteSpace(CurrentPasswordBox.Password))
                {
                    ShowPasswordError("Current password is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewPasswordBox.Password))
                {
                    ShowPasswordError("New password is required.");
                    return;
                }

                if (NewPasswordBox.Password.Length < 4)
                {
                    ShowPasswordError("New password must be at least 4 characters long.");
                    return;
                }

                if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
                {
                    ShowPasswordError("New passwords do not match.");
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId))
                {
                    ShowPasswordError("User session invalid. Please login again.");
                    return;
                }

                // Get current user information
                var currentUserResult = await _databaseService.GetByUserIdAsync<AdminUser>(_currentUserId);
                if (!currentUserResult.Success || currentUserResult.Data == null)
                {
                    ShowPasswordError("Unable to load user information.");
                    return;
                }

                var currentUser = currentUserResult.Data;

                // Verify current password by attempting authentication
                var authResult = await _databaseService.AuthenticateAsync(currentUser.Username, CurrentPasswordBox.Password);
                if (!authResult.Success || authResult.Data == null)
                {
                    ShowPasswordError("Current password is incorrect.");
                    return;
                }

                // Update password for the current user
                var result = await _databaseService.UpdateUserPasswordByUserIdAsync(_currentUserId, NewPasswordBox.Password, _currentUserId);
                
                if (result.Success)
                {
                    NotificationService.Quick.Success("Password updated successfully!");
                    CancelPasswordChange_Click(sender, e);
                }
                else
                {
                    ShowPasswordError($"Failed to update password: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ShowPasswordError($"Error changing password: {ex.Message}");
            }
        }

        /// <summary>
        /// Show/hide add user form
        /// </summary>
        private void AddNewUser_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAccessLevel != AdminAccessLevel.Master)
            {
                NotificationService.Quick.Warning("Only Master Admin can add new users.");
                return;
            }

            if (AddUserFormBorder.Visibility == Visibility.Collapsed)
            {
                AddUserFormBorder.Visibility = Visibility.Visible;
                AddNewUserButton.Content = "Cancel Add";
            }
            else
            {
                CancelAddUser_Click(sender, e);
            }
        }

        /// <summary>
        /// Cancel add user
        /// </summary>
        private void CancelAddUser_Click(object sender, RoutedEventArgs e)
        {
            AddUserFormBorder.Visibility = Visibility.Collapsed;
            AddNewUserButton.Content = "+ Add User";
            
            // Clear form
            if (NewUsernameBox != null) NewUsernameBox.Text = "";
            if (NewDisplayNameBox != null) NewDisplayNameBox.Text = "";
            if (NewUserPasswordBox != null) NewUserPasswordBox.Password = "";
            if (NewUserAccessUserRadio != null) NewUserAccessUserRadio.IsChecked = true;
            HideAddUserError();
        }

        /// <summary>
        /// Save new user
        /// </summary>
        private async void SaveNewUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate form
                if (string.IsNullOrWhiteSpace(NewUsernameBox.Text))
                {
                    ShowAddUserError("Username is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewDisplayNameBox.Text))
                {
                    ShowAddUserError("Display name is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewUserPasswordBox.Password))
                {
                    ShowAddUserError("Password is required.");
                    return;
                }

                if (NewUserPasswordBox.Password.Length < 4)
                {
                    ShowAddUserError("Password must be at least 4 characters long.");
                    return;
                }

                // Create new user
                var accessLevel = NewUserAccessAdminRadio.IsChecked == true ? AdminAccessLevel.Master : AdminAccessLevel.User;
                
                var newUser = new AdminUser
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = NewUsernameBox.Text.Trim(),
                    DisplayName = NewDisplayNameBox.Text.Trim(),
                    AccessLevel = accessLevel,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = _currentUserId
                };

                // Insert user (we'll need to add this method to DatabaseService)
                var result = await _databaseService.CreateAdminUserAsync(newUser, NewUserPasswordBox.Password, _currentUserId);
                
                if (result.Success)
                {
                    await LoadUsersList(); // Refresh the users list
                    NotificationService.Quick.UserCreated(newUser.DisplayName);
                    CancelAddUser_Click(sender, e);
                }
                else
                {
                    ShowAddUserError($"Failed to create user: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ShowAddUserError($"Error creating user: {ex.Message}");
            }
        }

        /// <summary>
        /// Show password error message
        /// </summary>
        private void ShowPasswordError(string message)
        {
            if (PasswordErrorMessage != null)
            {
                PasswordErrorMessage.Text = message;
                PasswordErrorMessage.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Hide password error message
        /// </summary>
        private void HidePasswordError()
        {
            if (PasswordErrorMessage != null)
            {
                PasswordErrorMessage.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Show add user error message
        /// </summary>
        private void ShowAddUserError(string message)
        {
            if (AddUserErrorMessage != null)
            {
                AddUserErrorMessage.Text = message;
                AddUserErrorMessage.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Hide add user error message
        /// </summary>
        private void HideAddUserError()
        {
            if (AddUserErrorMessage != null)
            {
                AddUserErrorMessage.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle volume slider value changes
        /// </summary>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeValueText != null)
            {
                VolumeValueText.Text = $"{(int)e.NewValue}%";
            }
        }

        /// <summary>
        /// Refresh users list button click
        /// </summary>
        private async void RefreshUsers_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsersList();
        }

        /// <summary>
        /// Load and display all admin users
        /// </summary>
        private async Task LoadUsersList()
        {
            try
            {
                if (UserListContainer != null && NoUsersMessage != null)
                {
                    // Show loading message
                    NoUsersMessage.Text = "Loading users...";
                    NoUsersMessage.Visibility = Visibility.Visible;
                    
                    // Clear existing user items
                    UserListContainer.Children.Clear();
                    UserListContainer.Children.Add(NoUsersMessage);

                    // Load users from database
                    var result = await _databaseService.GetAllAsync<AdminUser>();
                    
                    if (result.Success && result.Data != null && result.Data.Count > 0)
                    {
                        // Hide loading message
                        NoUsersMessage.Visibility = Visibility.Collapsed;
                        UserListContainer.Children.Remove(NoUsersMessage);

                        // Add each user to the list
                        foreach (var user in result.Data)
                        {
                            var userItem = CreateUserListItem(user);
                            UserListContainer.Children.Add(userItem);
                        }
                    }
                    else
                    {
                        // Show no users message
                        NoUsersMessage.Text = "No users found";
                        NoUsersMessage.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadUsersList error: {ex}");
                if (NoUsersMessage != null)
                {
                    NoUsersMessage.Text = "Error loading users";
                    NoUsersMessage.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// Create a user list item UI element
        /// </summary>
        private Border CreateUserListItem(AdminUser user)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // User info section
            var userInfoStack = new StackPanel { Orientation = Orientation.Vertical };
            
            var nameText = new TextBlock
            {
                Text = user.DisplayName,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            
            var detailsStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            var usernameText = new TextBlock
            {
                Text = $"@{user.Username}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                Margin = new Thickness(0, 0, 12, 0)
            };
            
            var accessLevelBorder = new Border
            {
                Background = user.AccessLevel == AdminAccessLevel.Master ? 
                    new SolidColorBrush(Color.FromRgb(0xDC, 0xFD, 0xF7)) : 
                    new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            
            var accessLevelText = new TextBlock
            {
                Text = user.AccessLevel.ToString(),
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = user.AccessLevel == AdminAccessLevel.Master ?
                    new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57)) :
                    new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF))
            };
            
            accessLevelBorder.Child = accessLevelText;
            detailsStack.Children.Add(usernameText);
            detailsStack.Children.Add(accessLevelBorder);
            
            userInfoStack.Children.Add(nameText);
            userInfoStack.Children.Add(detailsStack);
            
            Grid.SetColumn(userInfoStack, 0);
            grid.Children.Add(userInfoStack);

            // Delete button (only show if not current user and user has master access)
            if (user.UserId != _currentUserId && _currentAccessLevel == AdminAccessLevel.Master)
            {
                var deleteButton = new Button
                {
                    Content = "🗑️",
                    FontSize = 14,
                    Width = 28,
                    Height = 28,
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xFE, 0xCA, 0xCA)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                    Cursor = Cursors.Hand,
                    ToolTip = $"Delete user '{user.DisplayName}'",
                    Tag = user.UserId // Store user ID for deletion
                };

                // Style the delete button
                var buttonStyle = new Style(typeof(Button));
                var template = new ControlTemplate(typeof(Button));
                
                var borderTemplate = new FrameworkElementFactory(typeof(Border));
                borderTemplate.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
                borderTemplate.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
                borderTemplate.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
                borderTemplate.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                
                var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                
                borderTemplate.AppendChild(contentPresenter);
                template.VisualTree = borderTemplate;
                
                // Add hover trigger
                var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2))));
                template.Triggers.Add(hoverTrigger);
                
                buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, template));
                deleteButton.Style = buttonStyle;

                deleteButton.Click += async (s, e) => await DeleteUser_Click(user);
                
                Grid.SetColumn(deleteButton, 1);
                grid.Children.Add(deleteButton);
            }

            border.Child = grid;
            return border;
        }

        /// <summary>
        /// Handle user deletion with confirmation
        /// </summary>
        private async Task DeleteUser_Click(AdminUser user)
        {
            try
            {
                Console.WriteLine($"DeleteUser_Click: Attempting to delete user '{user.DisplayName}' ({user.UserId})");
                
                // Prevent self-deletion
                if (user.UserId == _currentUserId)
                {
                    NotificationService.Quick.Warning("You cannot delete your own account.");
                    return;
                }
                
                // Only Master level users can delete
                if (_currentAccessLevel != AdminAccessLevel.Master)
                {
                    NotificationService.Quick.Warning("Only Master administrators can delete users.");
                    return;
                }

                // Modern confirmation dialog instead of MessageBox
                var confirmed = ConfirmationDialog.ShowDeleteConfirmation(
                    $"{user.DisplayName} (@{user.Username})", 
                    "user", 
                    Window.GetWindow(this));

                if (confirmed)
                {
                    Console.WriteLine($"DeleteUser_Click: User confirmed deletion of '{user.DisplayName}'");
                    
                    // Delete the user
                    var deleteResult = await _databaseService.DeleteAdminUserAsync(user.UserId, _currentUserId);
                    
                    if (deleteResult.Success)
                    {
                        NotificationService.Quick.UserDeleted(user.DisplayName);
                        
                        // Refresh the user list
                        await LoadUsersList();
                    }
                    else
                    {
                        NotificationService.Quick.Error($"Failed to delete user: {deleteResult.ErrorMessage}");
                    }
                }
                else
                {
                    Console.WriteLine($"DeleteUser_Click: User cancelled deletion of '{user.DisplayName}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteUser_Click error: {ex}");
                NotificationService.Quick.Error($"An error occurred while deleting the user: {ex.Message}");
            }
        }



        #endregion

        #region Helper Methods

        /// <summary>
        /// Get tab name from button content
        /// </summary>
        private string GetTabNameFromButton(Button button)
        {
            try
            {
                if (button.Content is StackPanel stackPanel)
                {
                    // Find the TextBlock with the tab name (second child)
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text) &&
                            !textBlock.Text.Contains("📊") && !textBlock.Text.Contains("⚙️") &&
                            !textBlock.Text.Contains("📦") && !textBlock.Text.Contains("📅") &&
                            !textBlock.Text.Contains("🔧") && !textBlock.Text.Contains("💻") &&
                            !textBlock.Text.Contains("💰"))
                        {
                            return textBlock.Text;
                        }
                    }
                }

                // Fallback to button name
                return button.Name?.Replace("Tab", "") ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tab name: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Show specific tab content and hide others
        /// </summary>
        private void ShowTabContent(string tabName)
        {
            foreach (var kvp in _tabContentPanels)
            {
                kvp.Value.Visibility = (kvp.Key == tabName) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Update breadcrumb text
            try
            {
                if (BreadcrumbText != null)
                {
                    BreadcrumbText.Text = tabName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating breadcrumb: {ex.Message}");
            }
        }

        /// <summary>
        /// Update tab button visual states
        /// </summary>
        private void UpdateTabButtonStates(Button activeButton)
        {
            foreach (var button in _tabButtons.Values)
            {
                button.Tag = (button == activeButton) ? "Active" : null;
            }
        }

        /// <summary>
        /// Update sales display with current data
        /// </summary>
        private void UpdateSalesDisplay()
        {
            try
            {
                TodaySales.Text = $"${_salesData.Today.Amount:F2}";
                WeekSales.Text = $"${_salesData.Week.Amount:F2}";
                MonthSales.Text = $"${_salesData.Month.Amount:F2}";
                YearSales.Text = $"${_salesData.Year.Amount:F2}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating sales display: {ex.Message}");
            }
        }

        /// <summary>
        /// Update print status display
        /// </summary>
        private void UpdatePrintStatus()
        {
            try
            {
                PrintsRemainingText.Text = _printsRemaining.ToString();
                PrintsRemainingLarge.Text = _printsRemaining.ToString();

                double percentage = (double)_printsRemaining / PRINTS_PER_ROLL;

                // Update progress bar width (assuming max width of 280 as set in XAML)
                double maxWidth = 280;
                PrintsProgressBar.Width = maxWidth * percentage;

                // Update progress indicator in header (assuming max width of 120 as set in XAML)
                double headerMaxWidth = 120;
                PrintsProgressIndicator.Width = headerMaxWidth * percentage;

                // Update progress bar color based on remaining prints
                var colorBrush = percentage < 0.1 ? "#EF4444" : percentage < 0.25 ? "#F59E0B" : "#10B981";
                var brush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorBrush));

                PrintsProgressBar.Background = brush;
                PrintsProgressIndicator.Background = brush;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating print status: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadSalesData()
        {
            try
            {
                // Load today's sales
                var todayResult = await _databaseService.GetDailySalesAsync(DateTime.Today);
                if (todayResult.Success && todayResult.Data != null)
                {
                    var todaySales = todayResult.Data;
                    TodaySales.Text = $"${todaySales.TotalRevenue:F2}";
                }
                else
                {
                    // No sales today, use dummy data or show $0.00
                    TodaySales.Text = "$0.00";
                }

                // Load week sales (sum of last 7 days)
                var weekSales = await CalculatePeriodSales(DateTime.Today.AddDays(-7), DateTime.Today);
                WeekSales.Text = $"${weekSales:F2}";

                // Load month sales
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthSales = await CalculatePeriodSales(monthStart, DateTime.Today);
                MonthSales.Text = $"${monthSales:F2}";

                // Load year sales
                var yearStart = new DateTime(DateTime.Today.Year, 1, 1);
                var yearSales = await CalculatePeriodSales(yearStart, DateTime.Today);
                YearSales.Text = $"${yearSales:F2}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sales data: {ex.Message}");
                // Set default values on error
                TodaySales.Text = "$0.00";
                WeekSales.Text = "$0.00";
                MonthSales.Text = "$0.00";
                YearSales.Text = "$0.00";
            }
        }

        private async System.Threading.Tasks.Task<decimal> CalculatePeriodSales(DateTime startDate, DateTime endDate)
        {
            try
            {
                var salesResult = await _databaseService.GetSalesOverviewAsync(startDate, endDate);
                if (salesResult.Success && salesResult.Data != null)
                {
                    decimal total = 0;
                    foreach (var sale in salesResult.Data)
                    {
                        total += sale.Revenue;
                    }
                    return total;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating period sales: {ex.Message}");
            }
            return 0;
        }

        private async System.Threading.Tasks.Task LoadSupplyStatus()
        {
            try
            {
                var paperResult = await _databaseService.GetPrintSupplyAsync(SupplyType.Paper);
                if (paperResult.Success && paperResult.Data != null)
                {
                    var paper = paperResult.Data;

                    // Update prints remaining display
                    PrintsRemainingText.Text = paper.CurrentCount.ToString();
                    PrintsRemainingLarge.Text = paper.CurrentCount.ToString();

                    // Calculate progress percentage
                    double percentage = (double)paper.CurrentCount / paper.TotalCapacity * 100;

                    // Update progress bars
                    var progressWidth = Math.Max(0, Math.Min(100, percentage)) * 2.8; // Scale to fit the UI
                    PrintsProgressIndicator.Width = progressWidth;
                    PrintsProgressBar.Width = progressWidth * 1.5; // Larger progress bar in the lower section

                    // Update error display based on supply levels
                    if (paper.CurrentCount <= paper.CriticalThreshold)
                    {
                        ErrorCodeText.Text = $"Critical: Only {paper.CurrentCount} prints remaining";
                        ErrorCodeBorder.Visibility = Visibility.Visible;
                    }
                    else if (paper.CurrentCount <= paper.LowThreshold)
                    {
                        ErrorCodeText.Text = $"Low Supply: {paper.CurrentCount} prints remaining";
                        ErrorCodeBorder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ErrorCodeBorder.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading supply status: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadSystemSettings()
        {
            try
            {
                // Load operation mode
                var modeResult = await _databaseService.GetSettingValueAsync<string>("System", "Mode");
                if (modeResult.Success && !string.IsNullOrEmpty(modeResult.Data))
                {
                    _currentOperationMode = modeResult.Data;
                    CurrentModeText.Text = modeResult.Data == "Coin" ? "Coin Operated" : "Free Mode";
                }
                else
                {
                    _currentOperationMode = "Coin";
                    CurrentModeText.Text = "Coin Operated";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading system settings: {ex.Message}");
            }
        }

        private void UpdateAccessLevelDisplay()
        {
            switch (_currentAccessLevel)
            {
                case AdminAccessLevel.Master:
                    AccessLevelText.Text = "Master Access";
                    AccessLevelBadge.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D1FAE5"));
                    AccessLevelBadge.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                    
                    // Show user management for master admin
                    if (UserManagementSection != null)
                        UserManagementSection.Visibility = Visibility.Visible;
                    break;
                case AdminAccessLevel.User:
                    AccessLevelText.Text = "User Access";
                    AccessLevelBadge.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEF3C7"));
                    AccessLevelBadge.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B"));
                    
                    // Hide user management for regular users
                    if (UserManagementSection != null)
                        UserManagementSection.Visibility = Visibility.Collapsed;
                    break;
                default:
                    AccessLevelText.Text = "No Access";
                    if (UserManagementSection != null)
                        UserManagementSection.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// Load initial data when dashboard opens
        /// </summary>
        private async System.Threading.Tasks.Task LoadInitialData()
        {
            await LoadSalesData();
            await LoadSupplyStatus();
            await LoadSystemSettings();
            await LoadBusinessInformation();
            await LoadOperationModeFromSettings();
            await LoadSystemPreferences();
            await LoadProductsData(); // Load the products data for Products tab
            await LoadUsersList(); // Load the users list
        }

        private async System.Threading.Tasks.Task LoadBusinessInformation()
        {
            try
            {
                var businessInfoResult = await _databaseService.GetAllAsync<BusinessInfo>();
                if (businessInfoResult.Success && businessInfoResult.Data?.Count > 0)
                {
                    var businessInfo = businessInfoResult.Data[0];
                    
                    // Load business name and address
                    if (BusinessNameTextBox != null)
                        BusinessNameTextBox.Text = businessInfo.BusinessName ?? string.Empty;
                    
                    if (LocationTextBox != null)
                        LocationTextBox.Text = businessInfo.Address ?? string.Empty;
                    
                    if (ShowLogoToggle != null)
                        ShowLogoToggle.IsChecked = businessInfo.ShowLogoOnPrints;
                    
                    // Load existing logo if available
                    if (!string.IsNullOrEmpty(businessInfo.LogoPath))
                    {
                        _currentLogoPath = businessInfo.LogoPath;
                        var logoFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, businessInfo.LogoPath);
                        
                        if (File.Exists(logoFullPath))
                        {
                            try
                            {
                                var bitmap = new BitmapImage();
                                using (var stream = new FileStream(logoFullPath, FileMode.Open, FileAccess.Read))
                                {
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = stream;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                }
                                
                                if (LogoImage != null)
                                {
                                    LogoImage.Source = bitmap;
                                    LogoImage.Visibility = Visibility.Visible;
                                }
                                
                                if (LogoPlaceholderText != null)
                                    LogoPlaceholderText.Visibility = Visibility.Collapsed;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to load existing logo: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading business information: {ex.Message}");
            }
            
            // Also initialize operation mode UI
        }

        private async System.Threading.Tasks.Task LoadOperationModeFromSettings()
        {
            try
            {
                // Load operation mode only (products are now managed in Products tab)
                await LoadOperationMode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading operation mode settings: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadSystemPreferences()
        {
            try
            {
#pragma warning disable CS0472 // The result of the expression is always 'true'
                // Load volume setting
                var volumeResult = await _databaseService.GetSettingValueAsync<int>("System", "Volume");
                if (volumeResult.Success && volumeResult.Data != null)
                {
                    if (VolumeSlider != null)
                    {
                        VolumeSlider.Value = volumeResult.Data;
                        if (VolumeValueText != null)
                            VolumeValueText.Text = $"{volumeResult.Data}%";
                    }
                }

                // Load system toggles
                var lightsResult = await _databaseService.GetSettingValueAsync<bool>("System", "LightsEnabled");
                if (lightsResult.Success && lightsResult.Data != null)
                {
                    if (CameraFlashToggle != null)
                        CameraFlashToggle.IsChecked = lightsResult.Data;
                }

                var maintenanceResult = await _databaseService.GetSettingValueAsync<bool>("System", "MaintenanceMode");
                if (maintenanceResult.Success && maintenanceResult.Data != null)
                {
                    if (MaintenanceModeToggle != null)
                        MaintenanceModeToggle.IsChecked = maintenanceResult.Data;
                }

                var rfidResult = await _databaseService.GetSettingValueAsync<bool>("RFID", "Enabled");
                if (rfidResult.Success && rfidResult.Data != null)
                {
                    if (RFIDDetectionToggle != null)
                        RFIDDetectionToggle.IsChecked = rfidResult.Data;
                }

                var seasonalResult = await _databaseService.GetSettingValueAsync<bool>("Seasonal", "AutoTemplates");
                if (seasonalResult.Success && seasonalResult.Data != null)
                {
                    if (SeasonalTemplatesToggle != null)
                        SeasonalTemplatesToggle.IsChecked = seasonalResult.Data;
                }
#pragma warning restore CS0472
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading system preferences: {ex.Message}");
            }
        }

        #endregion

        #region Product Management

        /// <summary>
        /// Load products data from database and update UI
        /// </summary>
        private async Task LoadProductsData()
        {
            if (_isLoadingProducts) return;
            _isLoadingProducts = true;

            try
            {
                Console.WriteLine("LoadProductsData: Starting to load products from database...");
                var result = await _databaseService.GetProductsAsync();
                if (result.Success && result.Data != null)
                {
                    _products = result.Data;
                    Console.WriteLine($"LoadProductsData: Loaded {_products.Count} products successfully");
                    foreach (var product in _products)
                    {
                        Console.WriteLine($"LoadProductsData: Product - ID: {product.Id}, Name: {product.Name}, Price: {product.Price}, Active: {product.IsActive}");
                    }
                    UpdateProductsUI();
                }
                else
                {
                    Console.WriteLine($"LoadProductsData: Failed to load products: {result.ErrorMessage}");
                }

                // Load operation mode
                await LoadOperationMode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadProductsData: Error loading products: {ex.Message}");
            }
            finally
            {
                _isLoadingProducts = false;
            }
        }

        /// <summary>
        /// Update UI with current product data
        /// </summary>
        private void UpdateProductsUI()
        {
            try
            {
                // Find products by name (fallback to default values if not found)
                var photoStrips = _products.FirstOrDefault(p => p.Name.Contains("Strip"));
                var photo4x6 = _products.FirstOrDefault(p => p.Name.Contains("4x6"));
                var smartphonePrint = _products.FirstOrDefault(p => p.Name.Contains("Phone") || p.Name.Contains("Smartphone"));

                // Update Photo Strips
                if (photoStrips != null)
                {
                    ProductPhotoStripsEnabledToggle.IsChecked = photoStrips.IsActive;
                    ProductPhotoStripsPriceTextBox.Text = photoStrips.Price.ToString("F2");
                    UpdateProductCardVisualState(PhotoStripsCard, ProductPhotoStripsPriceSection, photoStrips.IsActive);
                }

                // Update 4x6 Photos
                if (photo4x6 != null)
                {
                    ProductPhoto4x6EnabledToggle.IsChecked = photo4x6.IsActive;
                    ProductPhoto4x6PriceTextBox.Text = photo4x6.Price.ToString("F2");
                    UpdateProductCardVisualState(Photo4x6Card, ProductPhoto4x6PriceSection, photo4x6.IsActive);
                }

                // Update Smartphone Print
                if (smartphonePrint != null)
                {
                    ProductSmartphonePrintEnabledToggle.IsChecked = smartphonePrint.IsActive;
                    ProductSmartphonePrintPriceTextBox.Text = smartphonePrint.Price.ToString("F2");
                    UpdateProductCardVisualState(SmartphonePrintCard, ProductSmartphonePrintPriceSection, smartphonePrint.IsActive);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating products UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Update visual state of product card based on enabled status
        /// </summary>
        private void UpdateProductCardVisualState(Border card, StackPanel priceSection, bool isEnabled)
        {
            try
            {
                if (isEnabled)
                {
                    card.Opacity = 1.0;
                    priceSection.IsEnabled = true;
                }
                else
                {
                    card.Opacity = 0.6;
                    priceSection.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating product card visual state: {ex.Message}");
            }
        }

        /// <summary>
        /// Load operation mode from settings
        /// </summary>
        private async Task LoadOperationMode()
        {
            try
            {
                var result = await _databaseService.GetSettingValueAsync<string>("System", "Mode");
                if (result.Success && !string.IsNullOrEmpty(result.Data))
                {
                    _currentOperationMode = result.Data;
                    UpdateOperationModeUI();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading operation mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Update operation mode UI
        /// </summary>
        private void UpdateOperationModeUI()
        {
            try
            {
                if (_currentOperationMode == "Free")
                {
                    ProductCoinOperatedRadio.IsChecked = false;
                    ProductFreePlayRadio.IsChecked = true;
                    UpdateModeCardStyles(false);
                }
                else
                {
                    ProductCoinOperatedRadio.IsChecked = true;
                    ProductFreePlayRadio.IsChecked = false;
                    UpdateModeCardStyles(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating operation mode UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Update mode card visual styles
        /// </summary>
        private void UpdateModeCardStyles(bool isCoinOperated)
        {
            try
            {
                if (isCoinOperated)
                {
                    ProductCoinOperatedCard.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    ProductCoinOperatedCard.BorderThickness = new Thickness(2);
                    ProductFreePlayCard.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                    ProductFreePlayCard.BorderThickness = new Thickness(1);
                }
                else
                {
                    ProductFreePlayCard.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    ProductFreePlayCard.BorderThickness = new Thickness(2);
                    ProductCoinOperatedCard.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                    ProductCoinOperatedCard.BorderThickness = new Thickness(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating mode card styles: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle product toggle changes
        /// </summary>
        private void ProductToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                _hasUnsavedChanges = true;

                // Update visual state
                if (toggle.Name == "ProductPhotoStripsEnabledToggle")
                {
                    UpdateProductCardVisualState(PhotoStripsCard, ProductPhotoStripsPriceSection, toggle.IsChecked ?? false);
                }
                else if (toggle.Name == "ProductPhoto4x6EnabledToggle")
                {
                    UpdateProductCardVisualState(Photo4x6Card, ProductPhoto4x6PriceSection, toggle.IsChecked ?? false);
                }
                else if (toggle.Name == "ProductSmartphonePrintEnabledToggle")
                {
                    UpdateProductCardVisualState(SmartphonePrintCard, ProductSmartphonePrintPriceSection, toggle.IsChecked ?? false);
                }

                // Update save button visual feedback
                UpdateSaveButtonState();
            }
        }

        /// <summary>
        /// Handle product price text changes
        /// </summary>
        private void ProductPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            _hasUnsavedChanges = true;
            UpdateSaveButtonState();
        }

        /// <summary>
        /// Handle mode card clicks
        /// </summary>
        private void ModeCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card)
            {
                if (card.Name == "ProductCoinOperatedCard")
                {
                    ProductCoinOperatedRadio.IsChecked = true;
                    ProductFreePlayRadio.IsChecked = false;
                    _currentOperationMode = "Coin";
                    UpdateModeCardStyles(true);
                }
                else if (card.Name == "ProductFreePlayCard")
                {
                    ProductFreePlayRadio.IsChecked = true;
                    ProductCoinOperatedRadio.IsChecked = false;
                    _currentOperationMode = "Free";
                    UpdateModeCardStyles(false);
                }

                _hasUnsavedChanges = true;
                UpdateSaveButtonState();
            }
        }

        /// <summary>
        /// Update save button state based on changes
        /// </summary>
        private void UpdateSaveButtonState()
        {
            try
            {
                // Only update if the save button exists (we're in the Products tab)
                if (SaveProductConfigButton == null)
                    return;

                if (_hasUnsavedChanges)
                {
                    SaveProductConfigButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")); // Green
                    SaveProductConfigButton.Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock { Text = "💾", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) },
                            new TextBlock { Text = "Save Changes" }
                        }
                    };
                }
                else
                {
                    SaveProductConfigButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")); // Blue
                    SaveProductConfigButton.Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock { Text = "💾", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) },
                            new TextBlock { Text = "Save Configuration" }
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating save button state: {ex.Message}");
            }
        }

        /// <summary>
        /// Save product configuration
        /// </summary>
        private async void SaveProductConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveProductConfigButton.IsEnabled = false;
                SaveProductConfigButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "⏳", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) },
                        new TextBlock { Text = "Saving..." }
                    }
                };

                await SaveProductConfiguration();
                
                _hasUnsavedChanges = false;
                UpdateSaveButtonState();

                // Show success feedback
                SaveProductConfigButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
                SaveProductConfigButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "✅", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) },
                        new TextBlock { Text = "Saved!" }
                    }
                };

                // Reset after 2 seconds
                await Task.Delay(2000);
                UpdateSaveButtonState();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving product configuration: {ex.Message}");
                
                // Show error feedback
                SaveProductConfigButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                SaveProductConfigButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "❌", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) },
                        new TextBlock { Text = "Error saving" }
                    }
                };

                await Task.Delay(2000);
                UpdateSaveButtonState();
            }
            finally
            {
                SaveProductConfigButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Save product configuration to database
        /// </summary>
        private async Task SaveProductConfiguration()
        {
            try
            {
                Console.WriteLine($"SaveProductConfiguration: Starting save. Products count: {_products.Count}");
                
                // Update products in database
                foreach (var product in _products)
                {
                    Console.WriteLine($"SaveProductConfiguration: Processing product: {product.Name} (ID: {product.Id})");
                    
                    bool newStatus = false;
                    decimal newPrice = 0;

                    if (product.Name.Contains("Strip"))
                    {
                        newStatus = ProductPhotoStripsEnabledToggle?.IsChecked ?? false;
                        decimal.TryParse(ProductPhotoStripsPriceTextBox?.Text ?? "0", out newPrice);
                        Console.WriteLine($"SaveProductConfiguration: Strip product - Status: {newStatus}, Price: {newPrice}");
                    }
                    else if (product.Name.Contains("4x6"))
                    {
                        newStatus = ProductPhoto4x6EnabledToggle?.IsChecked ?? false;
                        decimal.TryParse(ProductPhoto4x6PriceTextBox?.Text ?? "0", out newPrice);
                        Console.WriteLine($"SaveProductConfiguration: 4x6 product - Status: {newStatus}, Price: {newPrice}");
                    }
                    else if (product.Name.Contains("Phone") || product.Name.Contains("Smartphone"))
                    {
                        newStatus = ProductSmartphonePrintEnabledToggle?.IsChecked ?? false;
                        decimal.TryParse(ProductSmartphonePrintPriceTextBox?.Text ?? "0", out newPrice);
                        Console.WriteLine($"SaveProductConfiguration: Phone product - Status: {newStatus}, Price: {newPrice}");
                    }

                    // Update status if changed
                    if (product.IsActive != newStatus)
                    {
                        Console.WriteLine($"SaveProductConfiguration: Updating product {product.Name} status from {product.IsActive} to {newStatus}");
                        await _databaseService.UpdateProductStatusAsync(product.Id, newStatus);
                        product.IsActive = newStatus;
                    }

                    // Update price if changed
                    if (product.Price != newPrice && newPrice > 0)
                    {
                        Console.WriteLine($"SaveProductConfiguration: Updating product {product.Name} price from {product.Price} to {newPrice}");
                        await _databaseService.UpdateProductPriceAsync(product.Id, newPrice);
                        product.Price = newPrice;
                    }
                }

                // Save operation mode
                await _databaseService.SetSettingValueAsync("System", "Mode", _currentOperationMode, _currentUserId);

                Console.WriteLine("Product configuration saved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving product configuration: {ex.Message}");
                throw;
            }
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// Sales data container
    /// </summary>
    public class SalesData
    {
        public SalesPeriod Today { get; set; } = new();
        public SalesPeriod Week { get; set; } = new();
        public SalesPeriod Month { get; set; } = new();
        public SalesPeriod Year { get; set; } = new();
    }

    /// <summary>
    /// Sales period data
    /// </summary>
    public class SalesPeriod
    {
        public decimal Amount { get; set; }
        public int Transactions { get; set; }
    }

    #endregion
}