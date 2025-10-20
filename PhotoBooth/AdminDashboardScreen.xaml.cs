using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using PhotoBooth.Controls;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Photobooth.Controls;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// ViewModel for product configuration cards
    /// </summary>
    public class ProductViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private decimal _price;
        private bool _hasUnsavedChanges;
        private string? _validationError;

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string IconBackground { get; set; } = string.Empty;
        public string PriceLabel { get; set; } = string.Empty;
        public string ProductKey { get; set; } = string.Empty; // Used for identification in events

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    HasUnsavedChanges = true;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(CardOpacity));
                    OnPropertyChanged(nameof(PriceSectionEnabled));
                }
            }
        }

        public decimal Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    _price = value;
                    HasUnsavedChanges = true;
                    OnPropertyChanged(nameof(Price));
                    OnPropertyChanged(nameof(PriceText));
                }
            }
        }

        public string PriceText
        {
            get => _price.ToString("F2", CultureInfo.InvariantCulture);
            set
            {
#if DEBUG
                Console.WriteLine($"=== ProductViewModel.PriceText setter called ===");
                Console.WriteLine($"Product: {Name}, Current Price: {_price}, Input Value: '{value}'");
#endif
                
                // Use specific NumberStyles to prevent comma interpretation as thousands separator
                // Allow decimal point, leading sign, and whitespace, but not thousands separators
                var allowedStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | 
                                   NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
                if (decimal.TryParse(value, allowedStyles, CultureInfo.InvariantCulture, out decimal newPrice))
                {
#if DEBUG
                    Console.WriteLine($"Parsed successfully: {newPrice}");
#endif
                    
                    // Allow zero values for free products, but not negative values
                    if (newPrice >= 0)
                    {
#if DEBUG
                        Console.WriteLine($"Setting Price from {_price} to {newPrice}");
#endif
                        Price = newPrice;
                        ValidationError = null; // Clear any previous validation error
#if DEBUG
                        Console.WriteLine($"Price set successfully, new Price: {_price}");
#endif
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine($"Price rejected: negative value");
#endif
                        ValidationError = "Price cannot be negative";
                    }
                }
                else
                {
#if DEBUG
                    Console.WriteLine($"Parse failed for value: '{value}'");
#endif
                    ValidationError = "Invalid price format. Please enter a valid number (e.g., 5.00)";
                }
#if DEBUG
                Console.WriteLine($"=== ProductViewModel.PriceText setter completed ===");
#endif
            }
        }

        public double CardOpacity => _isEnabled ? 1.0 : 0.6;
        public bool PriceSectionEnabled => _isEnabled;

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                _hasUnsavedChanges = value;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }

        public string? ValidationError
        {
            get => _validationError;
            set
            {
                if (_validationError != value)
                {
                    _validationError = value;
                    OnPropertyChanged(nameof(ValidationError));
                    OnPropertyChanged(nameof(HasValidationError));
                }
            }
        }

        public bool HasValidationError => !string.IsNullOrEmpty(_validationError);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Reset the unsaved changes flag - used when loading data from database
        /// </summary>
        public void ResetUnsavedChanges()
        {
            _hasUnsavedChanges = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>
        /// Set price from database without triggering unsaved changes flag
        /// </summary>
        public void SetPriceFromDatabase(decimal price)
        {
            _price = price;
            // Don't trigger HasUnsavedChanges when loading from database
            OnPropertyChanged(nameof(Price));
            OnPropertyChanged(nameof(PriceText));
        }

        /// <summary>
        /// Set enabled status from database without triggering unsaved changes flag
        /// </summary>
        public void SetIsEnabledFromDatabase(bool isEnabled)
        {
            _isEnabled = isEnabled;
            // Don't trigger HasUnsavedChanges when loading from database
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(CardOpacity));
            OnPropertyChanged(nameof(PriceSectionEnabled));
        }
    }

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
        private readonly MainWindow? _mainWindow; // Reference to MainWindow for operation mode refresh

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
        private bool _isSaving = false;
        private bool _isValidatingInput = false;

        // Product ViewModels for new templated approach
        public ObservableCollection<ProductViewModel> ProductViewModels { get; set; } = new();

        // Templates tab control instance
        private Views.TemplatesTabControl? TemplatesTabControlInstance;

        // Credit management
        private decimal _currentCredits = 0;
        private List<CreditTransaction> _creditHistory = new();
        
        // Transaction history
        private List<Transaction> _recentTransactions = new();

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the admin dashboard screen
        /// </summary>
        public AdminDashboardScreen(IDatabaseService databaseService, MainWindow? mainWindow = null)
        {

            try
            {


                InitializeComponent();


                _databaseService = databaseService;
                _mainWindow = mainWindow;


                InitializeTabMapping();


                InitializeSalesData();


                InitializeProductViewModels();


                InitializeTemplatesTab();


                // Set up virtual keyboard callback for price input completion
                SetupVirtualKeyboardCallback();


            }
            catch (Exception ex)
            {


                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                throw; // Re-throw to prevent silent failures
            }
        }

        /// <summary>
        /// Set up the virtual keyboard callback for price input completion
        /// </summary>
        private void SetupVirtualKeyboardCallback()
        {
            try
            {
                // Set up callback for when price input is completed via virtual keyboard
                VirtualKeyboardService.Instance.OnPriceInputComplete = (context) =>
                {
                    // Trigger the save functionality when price input is completed
                    LoggingService.Application.Information("Price input completed via virtual keyboard", ("Context", context));
                    
                    // Use Dispatcher to ensure we're on the UI thread with proper async handling
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Use InvokeAsync for proper async/await handling
                            await Dispatcher.InvokeAsync(async () =>
                            {
                                // Trigger the save functionality
                                await SaveProductConfiguration();
                                
                                // Show success feedback
                                NotificationService.Quick.Success("Product settings saved successfully!");
                            });
                        }
                        catch (Exception ex)
                        {
                            LoggingService.Application.Error("Failed to save product configuration from virtual keyboard callback", ex);
                            
                            // Ensure error notification is shown on UI thread
                            await Dispatcher.InvokeAsync(() =>
                            {
                                NotificationService.Quick.Error("Failed to save product settings");
                            });
                        }
                    });
                };
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to set up virtual keyboard callback", ex);
            }
        }

        /// <summary>
        /// Initialize the Templates tab with the database service
        /// </summary>
        private void InitializeTemplatesTab()
        {
            try
            {
                // Create the TemplatesTabControl programmatically with proper dependency injection
                var templatesControl = new Views.TemplatesTabControl(_databaseService);
                
                // Add it to the TemplatesTabContent grid
                TemplatesTabContent.Children.Clear(); // Clear any existing content
                TemplatesTabContent.Children.Add(templatesControl);
                
                // Store reference for later access if needed
                TemplatesTabControlInstance = templatesControl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing Templates tab: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                // Don't throw here - let the admin screen continue without templates tab if needed
            }
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

        /// <summary>
        /// Initialize product view models for templated approach
        /// </summary>
        private void InitializeProductViewModels()
        {
            // Unsubscribe from existing view models before clearing to prevent memory leaks
            UnsubscribeFromProductViewModels();
            ProductViewModels.Clear();

            // Photo Strips Product - Initialize with defaults, database values will override during LoadProductsData
            var photoStrips = new ProductViewModel
            {
                Name = "Photo Strips",
                Description = "Classic 4-photo strips with templates",
                Icon = "📷",
                IconBackground = "#DBEAFE",
                PriceLabel = "Price per Strip",
                ProductKey = "PhotoStrips",
                IsEnabled = true,
                // Don't set Price here - let it default to 0, database values will override
            };

            // 4x6 Photos Product - Initialize with defaults, database values will override during LoadProductsData
            var photo4x6 = new ProductViewModel
            {
                Name = "4x6 Photos",
                Description = "Single high-quality 4x6 prints",
                Icon = "🖼️",
                IconBackground = "#FEF3C7",
                PriceLabel = "Price per Photo",
                ProductKey = "Photo4x6",
                IsEnabled = true,
                // Don't set Price here - let it default to 0, database values will override
            };

            // Smartphone Print Product - Initialize with defaults, database values will override during LoadProductsData
            var smartphonePrint = new ProductViewModel
            {
                Name = "Smartphone Print",
                Description = "Print photos from customer phones",
                Icon = "📱",
                IconBackground = "#ECFDF5",
                PriceLabel = "Price per Print",
                ProductKey = "SmartphonePrint",
                IsEnabled = true,
                // Don't set Price here - let it default to 0, database values will override
            };

            ProductViewModels.Add(photoStrips);
            ProductViewModels.Add(photo4x6);
            ProductViewModels.Add(smartphonePrint);

            // Subscribe to property change events for unsaved changes tracking
            foreach (var product in ProductViewModels)
            {
                product.PropertyChanged += ProductViewModel_PropertyChanged;
                // Reset unsaved changes flag so database values can load properly during LoadProductsData
                product.ResetUnsavedChanges();
            }

            // The ItemsSource will be set when the control is loaded
            this.Loaded += AdminDashboardScreen_Loaded;
            this.Unloaded += AdminDashboardScreen_Unloaded;
        }

        /// <summary>
        /// Handle property changes in product view models
        /// </summary>
        private void ProductViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProductViewModel.IsEnabled) || e.PropertyName == nameof(ProductViewModel.Price))
            {
                UpdateSaveButtonState();
            }
        }

        /// <summary>
        /// Unsubscribe from PropertyChanged events for all ProductViewModels to prevent memory leaks
        /// </summary>
        private void UnsubscribeFromProductViewModels()
        {
            foreach (var product in ProductViewModels)
            {
                product.PropertyChanged -= ProductViewModel_PropertyChanged;
            }
        }

        /// <summary>
        /// Handle the Loaded event to set up data binding
        /// </summary>
        private void AdminDashboardScreen_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the ItemsSource for the product ItemsControl
            if (ProductsItemsControl != null)
            {
                ProductsItemsControl.ItemsSource = ProductViewModels;
            }
        }

        /// <summary>
        /// Handle the Unloaded event to clean up event subscriptions and prevent memory leaks
        /// </summary>
        private void AdminDashboardScreen_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from PropertyChanged events to prevent memory leaks
            UnsubscribeFromProductViewModels();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the access level and configure UI accordingly
        /// </summary>
        public async System.Threading.Tasks.Task SetAccessLevel(AdminAccessLevel accessLevel, string? userId = null)
        {

            _currentAccessLevel = accessLevel;
            _currentUserId = userId;

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
                await LoadTransactionHistory();
            }
            catch
            {

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
            try
            {
                Console.WriteLine("AdminDashboard: Exit admin button clicked - cleaning up diagnostic camera");
                
                // Stop diagnostic camera when exiting admin
                StopDiagnosticCamera();
                LogToDiagnostics("Diagnostic camera stopped - exiting admin dashboard");
                
                // Invoke the exit event
                ExitAdminRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AdminDashboard: Error during admin exit - {ex.Message}");
                LoggingService.Application.Error("Failed to cleanup during admin exit", ex);
                
                // Still try to exit even if cleanup fails
                ExitAdminRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Handle tab button clicks
        /// </summary>
        private async void TabButton_Click(object sender, RoutedEventArgs e)
        {
            try
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

                    // Auto-stop diagnostic camera when leaving diagnostics tab
                    if (DiagnosticsTabContent.Visibility == Visibility.Visible && clickedTab.Name != "DiagnosticsTab")
                    {
                        StopDiagnosticCamera();
                        LogToDiagnostics("Diagnostic camera auto-stopped when leaving tab");
                    }
                    
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
                            LoggingService.Application.Information("Templates tab clicked");
                            TemplatesTabContent.Visibility = Visibility.Visible;
                            BreadcrumbText.Text = "Templates";
                            
                            // Manually trigger optimized template loading for faster tab switching
                            LoggingService.Application.Debug("Looking for TemplatesTabControl in TemplatesTabContent");
                            var templatesControl = FindTemplatesTabControl(TemplatesTabContent);
                            if (templatesControl != null)
                            {
                                LoggingService.Application.Debug("TemplatesTabControl found, triggering optimized load");
                                await templatesControl.ManualLoadTemplatesAsync();
                            }
                            else
                            {
                                LoggingService.Application.Warning("TemplatesTabControl not found in TemplatesTabContent");
                            }
                            break;
                        case "DiagnosticsTab":
                            DiagnosticsTabContent.Visibility = Visibility.Visible;
                            BreadcrumbText.Text = "Diagnostics";
                            InitializeDiagnosticsTab();
                            break;
                        case "SystemTab":
                            SystemTabContent.Visibility = Visibility.Visible;
                            BreadcrumbText.Text = "System";
                            await LoadSystemTabSettings();
                            break;
                        case "CreditsTab":
                            CreditsTabContent.Visibility = Visibility.Visible;
                            BreadcrumbText.Text = "Credits";
                            break;
                    }
                }
            }
            catch
            {
                // Log the error for debugging

                // Show user-friendly error message
                try
                {
                    NotificationService.Quick.Error("Failed to switch tabs. Please try again.");
                }
                catch (Exception ex)
                {
                    // Fallback if notification service fails
                    System.Diagnostics.Debug.WriteLine($"Critical error in TabButton_Click: {ex}");
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
                        new TextBlock { Text = "?", FontSize = 14, Margin = new Thickness(0,0,8,0) },
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

                // Get business name and location from UI elements
                var businessName = BusinessNameTextBox?.Text ?? "Downtown Event Center";
                var location = LocationTextBox?.Text ?? "Main Street, Downtown";
                var showLogo = ShowLogoToggle?.IsChecked == true;

                // Check if business info already exists
                var existingBusinessInfo = await _databaseService.GetAllAsync<BusinessInfo>();
                
                if (existingBusinessInfo.Success && existingBusinessInfo.Data?.Count > 0)
                {

                    // Update existing
                    var businessInfo = existingBusinessInfo.Data[0];
                    businessInfo.BusinessName = businessName;
                    businessInfo.Address = location;
                    businessInfo.ShowLogoOnPrints = showLogo;
                    businessInfo.LogoPath = _currentLogoPath;
                    businessInfo.UpdatedAt = DateTime.Now;
                    businessInfo.UpdatedBy = _currentUserId; // Use string directly

                    var updateResult = await _databaseService.UpdateAsync(businessInfo);

                    if (!updateResult.Success)
                    {
                        throw new Exception(updateResult.ErrorMessage ?? "Unknown error updating business info");
                    }
                }
                else
                {

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

                    if (!insertResult.Success)
                    {
                        throw new Exception(insertResult.ErrorMessage ?? "Unknown error creating business info");
                    }
                }

            }
            catch
            {

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

                Console.WriteLine($"=== SAVING OPERATION MODE === Mode: '{_currentOperationMode}', UserId: '{_currentUserId}'");

                // NOTE: Product pricing and enabled states are now managed in the Products tab
                // Only save operation mode here (Coin vs Free) - this is system-wide setting
                var result = await _databaseService.SetSettingValueAsync("System", "Mode", _currentOperationMode, _currentUserId);
                Console.WriteLine($"=== SAVE OPERATION MODE RESULT === Success: {result.Success}");

                // Refresh the MainWindow's operation mode to immediately reflect the change
                if (_mainWindow != null)
                {
                    Console.WriteLine("=== REFRESHING MAINWINDOW OPERATION MODE ===");
                    await _mainWindow.RefreshOperationModeAsync();
                    LoggingService.Application.Information("Operation mode refreshed in MainWindow", ("NewMode", _currentOperationMode));
                }
                else
                {
                    Console.WriteLine("=== MAINWINDOW IS NULL - CANNOT REFRESH OPERATION MODE ===");
                }

            }
            catch (Exception ex)
            {

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

                // Get values from UI elements
                var volume = (int)(VolumeSlider?.Value ?? 75);
                var cameraFlash = CameraFlashToggle?.IsChecked == true;
                var maintenanceMode = MaintenanceModeToggle?.IsChecked == true;
                var rfidEnabled = RFIDDetectionToggle?.IsChecked == true;
                var seasonalTemplates = SeasonalTemplatesToggle?.IsChecked == true;

                // Save system preferences
                var result1 = await _databaseService.SetSettingValueAsync("System", "Volume", volume, _currentUserId);

                var result2 = await _databaseService.SetSettingValueAsync("System", "LightsEnabled", cameraFlash, _currentUserId);

                var result3 = await _databaseService.SetSettingValueAsync("System", "MaintenanceMode", maintenanceMode, _currentUserId);

                var result4 = await _databaseService.SetSettingValueAsync("RFID", "Enabled", rfidEnabled, _currentUserId);

                var result5 = await _databaseService.SetSettingValueAsync("Seasonal", "AutoTemplates", seasonalTemplates, _currentUserId);

            }
            catch (Exception ex)
            {

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
            catch
            {

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
                    Content = "???",
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

                }
            }
            catch (Exception ex)
            {

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
                            !textBlock.Text.Contains("??") && !textBlock.Text.Contains("??") &&
                            !textBlock.Text.Contains("??") && !textBlock.Text.Contains("??") &&
                            !textBlock.Text.Contains("??") && !textBlock.Text.Contains("??") &&
                            !textBlock.Text.Contains("??"))
                        {
                            return textBlock.Text;
                        }
                    }
                }

                // Fallback to button name
                return button.Name?.Replace("Tab", "") ?? string.Empty;
            }
            catch
            {

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
            catch
            {

            }

            // Refresh data when showing specific tabs
            if (tabName == "Credits")
            {
                // Refresh credits display and history when tab is shown
                UpdateCreditsDisplay();
                UpdateCreditHistoryDisplay();
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
            catch
            {

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
            catch
            {

            }
        }

        private async System.Threading.Tasks.Task LoadSalesData()
        {
            try
            {
                // Load today's sales and calculate change from yesterday
                var todayResult = await _databaseService.GetDailySalesAsync(DateTime.Today);
                var todaySales = todayResult.Success && todayResult.Data != null ? todayResult.Data.TotalRevenue : 0;
                var yesterdaySales = await CalculatePeriodSales(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1));
                
                TodaySales.Text = $"${todaySales:F2}";
                UpdatePercentageDisplay(TodayChange, todaySales, yesterdaySales, "yesterday");

                // Load week sales (current week vs last week)
                var currentWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var currentWeekSales = await CalculatePeriodSales(currentWeekStart, DateTime.Today);
                var lastWeekStart = currentWeekStart.AddDays(-7);
                var lastWeekSales = await CalculatePeriodSales(lastWeekStart, lastWeekStart.AddDays(6));
                
                WeekSales.Text = $"${currentWeekSales:F2}";
                UpdatePercentageDisplay(WeekChange, currentWeekSales, lastWeekSales, "last week");

                // Load month sales (current month vs last month)
                var currentMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var currentMonthSales = await CalculatePeriodSales(currentMonthStart, DateTime.Today);
                var lastMonthStart = currentMonthStart.AddMonths(-1);
                var lastMonthEnd = currentMonthStart.AddDays(-1);
                var lastMonthSales = await CalculatePeriodSales(lastMonthStart, lastMonthEnd);
                
                MonthSales.Text = $"${currentMonthSales:F2}";
                UpdatePercentageDisplay(MonthChange, currentMonthSales, lastMonthSales, "last month");

                // Load year sales (current year vs last year)
                var currentYearStart = new DateTime(DateTime.Today.Year, 1, 1);
                var currentYearSales = await CalculatePeriodSales(currentYearStart, DateTime.Today);
                var lastYearStart = new DateTime(DateTime.Today.Year - 1, 1, 1);
                var lastYearEnd = new DateTime(DateTime.Today.Year - 1, 12, 31);
                var lastYearSales = await CalculatePeriodSales(lastYearStart, lastYearEnd);
                
                YearSales.Text = $"${currentYearSales:F2}";
                UpdatePercentageDisplay(YearChange, currentYearSales, lastYearSales, "last year");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load sales data", ex);

                // Set default values on error
                TodaySales.Text = "$0.00";
                WeekSales.Text = "$0.00";
                MonthSales.Text = "$0.00";
                YearSales.Text = "$0.00";
                
                // Set default percentage messages
                TodayChange.Text = "No data available";
                WeekChange.Text = "No data available";
                MonthChange.Text = "No data available";
                YearChange.Text = "No data available";
            }
        }

        /// <summary>
        /// Update percentage display with calculated change and appropriate formatting
        /// </summary>
        private void UpdatePercentageDisplay(TextBlock textBlock, decimal currentValue, decimal previousValue, string periodName)
        {
            try
            {
                if (textBlock == null) return;

                if (previousValue == 0)
                {
                    if (currentValue > 0)
                    {
                        // New sales with no previous data
                        textBlock.Text = $"New sales vs {periodName}";
                        textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")); // Green
                    }
                    else
                    {
                        // No sales in either period
                        textBlock.Text = $"No change from {periodName}";
                        textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")); // Gray
                    }
                    return;
                }

                // Calculate percentage change
                var percentageChange = ((currentValue - previousValue) / previousValue) * 100;
                var roundedPercentage = Math.Round(percentageChange, 1);

                if (roundedPercentage > 0)
                {
                    textBlock.Text = $"+{roundedPercentage}% from {periodName}";
                    textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")); // Green
                }
                else if (roundedPercentage < 0)
                {
                    textBlock.Text = $"{roundedPercentage}% from {periodName}"; // Negative sign already included
                    textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // Red
                }
                else
                {
                    textBlock.Text = $"No change from {periodName}";
                    textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")); // Gray
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update percentage display", ex);
                textBlock.Text = $"Error calculating vs {periodName}";
                textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")); // Gray
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
            catch
            {

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
            catch
            {

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
            catch
            {

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
            await LoadBusinessInformation();
            await LoadOperationModeFromSettings();
            await LoadSystemPreferences();
            await LoadProductsData(); // Load the products data for Products tab
            await LoadUsersList(); // Load the users list
            await LoadCreditsAsync(); // Load the credit system data
            await LoadTransactionHistory(); // Load transaction history for sales tab
            await LoadSystemTabSettings(); // Load system configuration settings
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
                            catch
                            {

                            }
                        }
                    }
                }
            }
            catch
            {

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
            catch
            {

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
            catch
            {

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

                var result = await _databaseService.GetProductsAsync();
                if (result.Success && result.Data != null)
                {
                    _products = result.Data;

                    foreach (var product in _products)
                    {

                    }
                    UpdateProductsUI();
                }
                else
                {

                }

                // Load operation mode
                await LoadOperationMode();
            }
            catch
            {

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
                // Find products by ProductType enum (robust and future-proof)
                var photoStrips = _products.FirstOrDefault(p => p.ProductType == ProductType.PhotoStrips);
                var photo4x6 = _products.FirstOrDefault(p => p.ProductType == ProductType.Photo4x6);
                var smartphonePrint = _products.FirstOrDefault(p => p.ProductType == ProductType.SmartphonePrint);

                // Update ViewModels with database data, but preserve unsaved changes
                var photoStripsVM = ProductViewModels.FirstOrDefault(vm => vm.ProductKey == "PhotoStrips");
                if (photoStripsVM != null && photoStrips != null)
                {
                    // Only update if there are no unsaved changes to preserve user input
                    if (!photoStripsVM.HasUnsavedChanges)
                    {
                        photoStripsVM.SetIsEnabledFromDatabase(photoStrips.IsActive);
                        photoStripsVM.SetPriceFromDatabase(photoStrips.Price);
                    }
                    else
                    {
        
                    }
                }

                var photo4x6VM = ProductViewModels.FirstOrDefault(vm => vm.ProductKey == "Photo4x6");
                if (photo4x6VM != null && photo4x6 != null)
                {
                    // Only update if there are no unsaved changes to preserve user input
                    if (!photo4x6VM.HasUnsavedChanges)
                    {
                        photo4x6VM.SetIsEnabledFromDatabase(photo4x6.IsActive);
                        photo4x6VM.SetPriceFromDatabase(photo4x6.Price);
                    }
                    else
                    {
        
                    }
                }

                var smartphonePrintVM = ProductViewModels.FirstOrDefault(vm => vm.ProductKey == "SmartphonePrint");
                if (smartphonePrintVM != null && smartphonePrint != null)
                {
                    // Only update if there are no unsaved changes to preserve user input
                    if (!smartphonePrintVM.HasUnsavedChanges)
                    {
                        smartphonePrintVM.SetIsEnabledFromDatabase(smartphonePrint.IsActive);
                        smartphonePrintVM.SetPriceFromDatabase(smartphonePrint.Price);
                    }
                    else
                    {
        
                    }
                }

                // Only clear unsaved changes flag if no ViewModels have unsaved changes
                var hasAnyUnsavedChanges = ProductViewModels.Any(vm => vm.HasUnsavedChanges);
                if (!hasAnyUnsavedChanges)
                {
            }

                // Load extra copy pricing (use PhotoStrips as reference product)
                if (photoStrips != null)
            {
                    LoadExtraCopyPricingUI(photoStrips);
                }

                // Update base price displays to show current database values
                UpdateBasePriceDisplays();

                UpdateSaveButtonState();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update products UI", ex);
            }
        }

        /// <summary>
        /// Update visual state of product card based on enabled status (Legacy method - no longer needed with ViewModels)
        /// </summary>
        [Obsolete("This method is no longer needed as visual state is handled by data binding in the ViewModels")]
        private void UpdateProductCardVisualState(Border card, StackPanel priceSection, bool isEnabled)
        {
            // This method is obsolete - visual states are now handled by ViewModel property binding
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
            catch
            {

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
            catch
            {

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
            catch
            {

            }
        }

        /// <summary>
        /// Handle product toggle changes (Legacy method - ViewModels now handle changes automatically)
        /// </summary>
        [Obsolete("This method is no longer needed as changes are handled by ViewModel property binding")]
        private void ProductToggle_Click(object sender, RoutedEventArgs e)
        {
            // This method is obsolete - toggle changes are now handled by ViewModel data binding
            // The ProductViewModel_PropertyChanged method handles unsaved changes tracking
        }

        /// <summary>
        /// Handle product price text changes (Legacy method - ViewModels now handle changes automatically)
        /// </summary>
        [Obsolete("This method is no longer needed as changes are handled by ViewModel property binding")]
        private void ProductPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            // This method is obsolete - price changes are now handled by ViewModel data binding
            // The ProductViewModel_PropertyChanged method handles unsaved changes tracking
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
                if (SaveProductConfigButton == null || SaveButtonText == null || UnsavedChangesIndicator == null || SaveButtonBorder == null)
                    return;

                // Check if any ProductViewModel has unsaved changes
                var hasAnyUnsavedChanges = ProductViewModels.Any(vm => vm.HasUnsavedChanges);

                // Check if there are any validation errors in pricing inputs
                var hasValidationErrors = HasAnyValidationErrors();

                if (hasValidationErrors)
                {
                    // Show validation error state - disable save button
                    SaveButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // Red
                    SaveButtonText.Text = "Fix Errors";
                    UnsavedChangesIndicator.Text = "Validation errors detected";
                    UnsavedChangesIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // Red
                    UnsavedChangesIndicator.Visibility = Visibility.Visible;
                    SaveProductConfigButton.IsEnabled = false;
                }
                else if (hasAnyUnsavedChanges)
                {
                    // Show unsaved changes state
                    SaveButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")); // Green
                    SaveButtonText.Text = "Save Changes";
                    UnsavedChangesIndicator.Text = "Unsaved changes";
                    UnsavedChangesIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Amber
                    UnsavedChangesIndicator.Visibility = Visibility.Visible;
                    SaveProductConfigButton.IsEnabled = true;
                }
                else
                {
                    // Show normal state
                    SaveButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")); // Blue
                    SaveButtonText.Text = "Save Configuration";
                    UnsavedChangesIndicator.Visibility = Visibility.Collapsed;
                    SaveProductConfigButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error updating save button state", ex);
            }
        }

        /// <summary>
        /// Save product configuration
        /// </summary>
        private async void SaveProductConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
#if DEBUG
                Console.WriteLine("=== SaveProductConfig_Click CALLED ===");
#endif
                
                // Prevent multiple simultaneous saves
                if (_isSaving)
                {
#if DEBUG
                    Console.WriteLine("Save already in progress, ignoring duplicate request");
#endif
                    return;
                }
                
                _isSaving = true;
                SaveProductConfigButton.IsEnabled = false;
                SaveButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")); // Purple for saving
                SaveButtonText.Text = "Saving...";
                UnsavedChangesIndicator.Text = "Please wait...";
                UnsavedChangesIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")); // Purple
                UnsavedChangesIndicator.Visibility = Visibility.Visible;

#if DEBUG
                Console.WriteLine("About to call SaveProductConfiguration()...");
#endif
                await SaveProductConfiguration();
#if DEBUG
                Console.WriteLine("SaveProductConfiguration() completed successfully");
#endif
                
                // Show success feedback
                SaveButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
                SaveButtonText.Text = "Saved!";
                UnsavedChangesIndicator.Text = "Settings saved successfully";
                UnsavedChangesIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")); // Green
                UnsavedChangesIndicator.Visibility = Visibility.Visible;

                // Show notification toast
                NotificationService.Quick.Success("Product settings have been saved successfully!");

                // Reset after 2 seconds
                await Task.Delay(2000);
                UnsavedChangesIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Back to amber
                UpdateSaveButtonState();
                
#if DEBUG
                Console.WriteLine("=== SaveProductConfig_Click COMPLETED SUCCESSFULLY ===");
#endif
            }
            catch (Exception)
            {
                // Show error feedback
                SaveButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                SaveButtonText.Text = "Error!";
                UnsavedChangesIndicator.Text = "Failed to save settings";
                UnsavedChangesIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // Red
                UnsavedChangesIndicator.Visibility = Visibility.Visible;

                await Task.Delay(2000);
                UnsavedChangesIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Back to amber
                UpdateSaveButtonState();
            }
            finally
            {
                _isSaving = false;
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
#if DEBUG
                Console.WriteLine("=== SaveProductConfiguration CALLED ===");
                Console.WriteLine($"Total ProductViewModels: {ProductViewModels.Count}");
                
                foreach (var vm in ProductViewModels)
                {
                    Console.WriteLine($"Product: {vm.Name}, Price: {vm.Price}, HasUnsavedChanges: {vm.HasUnsavedChanges}");
                }
#endif
                
                // Check for validation errors before saving
                var validationErrors = ProductViewModels.Where(vm => vm.HasValidationError).ToList();
#if DEBUG
                Console.WriteLine($"Validation errors found: {validationErrors.Count}");
#endif
                
                if (validationErrors.Count > 0)
                {
                    // Create detailed error message listing each product and its specific error
                    var errorDetails = new List<string>();
                    errorDetails.Add($"Cannot save: {validationErrors.Count} product(s) have validation errors:");
                    errorDetails.Add(""); // Empty line for readability
                    
                    foreach (var errorViewModel in validationErrors)
                    {
                        errorDetails.Add($"• {errorViewModel.Name}: {errorViewModel.ValidationError}");
                    }
                    
                    errorDetails.Add(""); // Empty line for readability
                    errorDetails.Add("Please fix these errors and try again.");
                    
                    var detailedErrorMessage = string.Join(Environment.NewLine, errorDetails);

                    throw new InvalidOperationException(detailedErrorMessage);
                }

                // Update products in database using ViewModels
#if DEBUG
                Console.WriteLine($"Updating {_products.Count} products in database...");
#endif
                
                foreach (var product in _products)
                {
#if DEBUG
                    Console.WriteLine($"Processing product: {product.Name} (Type: {product.ProductType}, ID: {product.Id})");
#endif

                    bool newStatus = false;
                    decimal newPrice = 0;

                    // Find corresponding ViewModel using ProductType enum
                    ProductViewModel? viewModel = null;
                    switch (product.ProductType)
                    {
                        case ProductType.PhotoStrips:
                            viewModel = ProductViewModels.FirstOrDefault(vm => vm.ProductKey == "PhotoStrips");
                            break;
                        case ProductType.Photo4x6:
                            viewModel = ProductViewModels.FirstOrDefault(vm => vm.ProductKey == "Photo4x6");
                            break;
                        case ProductType.SmartphonePrint:
                            viewModel = ProductViewModels.FirstOrDefault(vm => vm.ProductKey == "SmartphonePrint");
                            break;
                    }

                    if (viewModel != null)
                    {
                        newStatus = viewModel.IsEnabled;
                        newPrice = viewModel.Price;

#if DEBUG
                        Console.WriteLine($"Found ViewModel: {viewModel.Name}, Current DB Price: {product.Price}, New Price: {newPrice}, HasUnsavedChanges: {viewModel.HasUnsavedChanges}");
#endif

                        // Check what needs to be updated
                        bool statusChanged = product.IsActive != newStatus;
                        bool priceChanged = product.Price != newPrice && newPrice >= 0;
                        
#if DEBUG
                        Console.WriteLine($"Status changed: {statusChanged} ({product.IsActive} -> {newStatus}), Price changed: {priceChanged} ({product.Price} -> {newPrice})");
#endif
                        
                        if (statusChanged || priceChanged)
                        {
#if DEBUG
                            Console.WriteLine($"Updating product {product.Name} in database...");
#endif
                            
                            // Collect all changes for atomic update
                            bool? statusUpdate = statusChanged ? newStatus : null;
                            decimal? priceUpdate = priceChanged ? newPrice : null;

                            // Perform atomic update
                            var updateResult = await _databaseService.UpdateProductAsync(product.Id, statusUpdate, priceUpdate);
                            
#if DEBUG
                            Console.WriteLine($"Database update result for {product.Name}: Success={updateResult.Success}, Error={updateResult.ErrorMessage}");
#endif
                            
                            if (!updateResult.Success)
                            {
                                throw new InvalidOperationException($"Failed to update product {product.Name}: {updateResult.ErrorMessage}");
                            }
                            
                            // Update local product model only after successful database update
                            if (statusChanged)
                            {
                                product.IsActive = newStatus;
#if DEBUG
                                Console.WriteLine($"Updated local product {product.Name} IsActive to {newStatus}");
#endif
                            }
                            if (priceChanged)
                            {
                                product.Price = newPrice;
#if DEBUG
                                Console.WriteLine($"Updated local product {product.Name} Price to {newPrice}");
#endif
                            }

                        }
                        else
                        {
#if DEBUG
                            Console.WriteLine($"No changes needed for product {product.Name}");
#endif
                        }

                        // Mark ViewModel as saved
                        viewModel.ResetUnsavedChanges();
#if DEBUG
                        Console.WriteLine($"Marked ViewModel {viewModel.Name} as saved (HasUnsavedChanges = false)");
#endif
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine($"No ViewModel found for product {product.Name}");
#endif
                    }
                }
                
#if DEBUG
                Console.WriteLine("=== SaveProductConfiguration COMPLETED ===");
#endif

                // Save extra copy pricing for all products
                await SaveExtraCopyPricing();

                // Refresh products from database to ensure local _products list is up to date
                var refreshResult = await _databaseService.GetProductsAsync();
                if (refreshResult.Success && refreshResult.Data != null)
                {
                    _products = refreshResult.Data;
#if DEBUG
                    Console.WriteLine("=== Refreshed _products list from database after save ===");
                    foreach (var product in _products)
                    {
                        Console.WriteLine($"  {product.Name}: Price=${product.Price}, IsActive={product.IsActive}");
                    }
#endif
                }

                // Save operation mode and refresh MainWindow
                await SaveOperationModeSettings();

            }
            catch
            {

                throw;
            }
        }

        /// <summary>
        /// Save extra copy pricing for all products
        /// </summary>
        private async Task SaveExtraCopyPricing()
        {
            try
            {
                // Get custom pricing toggle state
                bool useCustomPricing = UseCustomExtraCopyPricingCheckBox?.IsChecked == true;

                // Simplified product-specific extra copy pricing
                decimal? stripsExtraCopyPrice = null;
                decimal? stripsMultipleCopyDiscount = null;
                decimal? photo4x6ExtraCopyPrice = null;
                decimal? photo4x6MultipleCopyDiscount = null;
                decimal? smartphoneExtraCopyPrice = null;
                decimal? smartphoneMultipleCopyDiscount = null;

                if (useCustomPricing)
                {
                    // Get Photo Strips pricing from UI
                    if (StripsExtraCopyPriceInput != null && int.TryParse(StripsExtraCopyPriceInput.Text, out int stripsPrice))
                    {
                        stripsExtraCopyPrice = stripsPrice;
                    }
                    if (StripsMultipleCopyDiscountInput != null && int.TryParse(StripsMultipleCopyDiscountInput.Text, out int stripsDiscount))
                    {
                        stripsMultipleCopyDiscount = stripsDiscount;
                    }

                    // Get 4x6 Photos pricing from UI
                    if (Photo4x6ExtraCopyPriceInput != null && int.TryParse(Photo4x6ExtraCopyPriceInput.Text, out int photo4x6Price))
                    {
                        photo4x6ExtraCopyPrice = photo4x6Price;
                    }
                    if (Photo4x6MultipleCopyDiscountInput != null && int.TryParse(Photo4x6MultipleCopyDiscountInput.Text, out int photo4x6Discount))
                    {
                        photo4x6MultipleCopyDiscount = photo4x6Discount;
                    }

                    // Get Smartphone Print pricing from UI
                    if (SmartphoneExtraCopyPriceInput != null && int.TryParse(SmartphoneExtraCopyPriceInput.Text, out int smartphonePrice))
                    {
                        smartphoneExtraCopyPrice = smartphonePrice;
                    }
                    if (SmartphoneMultipleCopyDiscountInput != null && int.TryParse(SmartphoneMultipleCopyDiscountInput.Text, out int smartphoneDiscount))
                    {
                        smartphoneMultipleCopyDiscount = smartphoneDiscount;
                    }
                }
                // If not using custom pricing, leave values as null (will use base product price)

                // Use the product-specific pricing values from UI

                // Save for all products
                foreach (var product in _products)
                {
                    var updateResult = await _databaseService.UpdateProductAsync(
                        product.Id,
                        isActive: null,
                        price: null,
                        useCustomExtraCopyPricing: useCustomPricing,
                        // Legacy pricing (keep for backward compatibility)
                        extraCopy1Price: null,
                        extraCopy2Price: null,
                        extraCopy4BasePrice: null,
                        extraCopyAdditionalPrice: null,
                        // Simplified product-specific extra copy pricing
                        stripsExtraCopyPrice: stripsExtraCopyPrice,
                        stripsMultipleCopyDiscount: stripsMultipleCopyDiscount,
                        photo4x6ExtraCopyPrice: photo4x6ExtraCopyPrice,
                        photo4x6MultipleCopyDiscount: photo4x6MultipleCopyDiscount,
                        smartphoneExtraCopyPrice: smartphoneExtraCopyPrice,
                        smartphoneMultipleCopyDiscount: smartphoneMultipleCopyDiscount
                    );

                    if (!updateResult.Success)
                    {
                        throw new InvalidOperationException($"Failed to update extra copy pricing for {product.Name}: {updateResult.ErrorMessage}");
                    }

                    // Update local product model
                    product.UseCustomExtraCopyPricing = useCustomPricing;
                    
                    // Update simplified product-specific pricing
                    product.StripsExtraCopyPrice = stripsExtraCopyPrice;
                    product.StripsMultipleCopyDiscount = stripsMultipleCopyDiscount;
                    product.Photo4x6ExtraCopyPrice = photo4x6ExtraCopyPrice;
                    product.Photo4x6MultipleCopyDiscount = photo4x6MultipleCopyDiscount;
                    product.SmartphoneExtraCopyPrice = smartphoneExtraCopyPrice;
                    product.SmartphoneMultipleCopyDiscount = smartphoneMultipleCopyDiscount;
                }

            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to save extra copy pricing", ex);
                throw;
            }
        }

        /// <summary>
        /// Event handler for extra copy price changes - updates pricing examples
        /// </summary>
        private void ExtraCopyPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Only validate if we're not loading from database
                if (!_isLoadingProducts)
                {
                    ValidatePricingInput(textBox);
                    
                    // Provide immediate feedback for better UX
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        ClearValidationError(textBox);
                    }
                }
            }
            
            UpdatePricingExamples();
            
            // Mark as having unsaved changes (unless we're loading from database)
            if (!_isLoadingProducts)
            {
                UpdateSaveButtonState();
            }
        }

        /// <summary>
        /// Event handler for multiple copy discount changes
        /// </summary>
        private void MultipleCopyDiscount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Only validate if we're not loading from database
                if (!_isLoadingProducts)
                {
                    ValidateDiscountInput(textBox);
                    
                    // Provide immediate feedback for better UX
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        ClearValidationError(textBox);
                    }
                }
            }
            
            UpdatePricingExamples();
            
            // Mark as having unsaved changes (unless we're loading from database)
            if (!_isLoadingProducts)
            {
                UpdateSaveButtonState();
            }
        }

        /// <summary>
        /// Event handler for smartphone input focus - adds padding for virtual keyboard
        /// </summary>
        private void SmartphoneInput_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // Add extra bottom padding to make room for virtual keyboard
                if (MainScrollViewer != null)
                {
                    MainScrollViewer.Padding = new Thickness(24, 0, 24, 120);
                    
                    // Scroll to the bottom to ensure the input is at the bottom where there's space for the keyboard
                    MainScrollViewer.ScrollToBottom();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error handling smartphone input focus", ex);
            }
        }

        /// <summary>
        /// Event handler for smartphone input blur - removes extra padding
        /// </summary>
        private void SmartphoneInput_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if any smartphone input still has focus
                bool anySmartphoneInputFocused = SmartphoneExtraCopyPriceInput?.IsFocused == true || 
                                                SmartphoneMultipleCopyDiscountInput?.IsFocused == true;
                
                // Only remove padding if no smartphone input has focus
                if (!anySmartphoneInputFocused && MainScrollViewer != null)
                {
                    MainScrollViewer.Padding = new Thickness(24, 0, 24, 24);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error handling smartphone input blur", ex);
            }
        }

        /// <summary>
        /// Event handler for when custom pricing is enabled
        /// </summary>
        private void UseCustomPricing_Checked(object sender, RoutedEventArgs e)
        {
            if (CustomPricingSection != null)
            {
                CustomPricingSection.Visibility = Visibility.Visible;
            }
            
            if (CustomPricingDescription != null)
            {
                CustomPricingDescription.Text = "Configure custom pricing for extra copies";
            }

            // Mark as having unsaved changes
            if (!_isLoadingProducts)
            {
                UpdateSaveButtonState();
            }
        }

        /// <summary>
        /// Event handler for when custom pricing is disabled
        /// </summary>
        private void UseCustomPricing_Unchecked(object sender, RoutedEventArgs e)
        {
            if (CustomPricingSection != null)
            {
                CustomPricingSection.Visibility = Visibility.Collapsed;
            }
            
            if (CustomPricingDescription != null)
            {
                CustomPricingDescription.Text = "Extra copies will cost the same as the base product price";
            }

            // Mark as having unsaved changes
            if (!_isLoadingProducts)
            {
                UpdateSaveButtonState();
            }
        }

        /// <summary>
        /// Update pricing examples based on current input values
        /// </summary>
        private void UpdatePricingExamples()
        {
            // Pricing examples removed - they were causing confusion and didn't match frontend display
            // The frontend now shows rounded prices (Math.Ceiling) to avoid decimal pricing issues
        }

        /// <summary>
        /// Validate pricing input and provide user feedback
        /// </summary>
        private void ValidatePricingInput(TextBox textBox)
        {
            if (_isValidatingInput) return; // Prevent recursive validation
            
            try
            {
                _isValidatingInput = true;
                var inputText = textBox.Text?.Trim() ?? "";
                
                // Allow empty input during typing
                if (string.IsNullOrEmpty(inputText))
                {
                    ClearValidationError(textBox);
                    return;
                }

                // Try to parse the input as integer
                if (int.TryParse(inputText, out int price))
                {
                    // Validate price range (must be >= 0)
                    if (price < 0)
                    {
                        // Clamp negative values to 0
                        textBox.Text = "0";
                        ShowValidationError(textBox, "Price cannot be negative. Set to 0.");
                        LoggingService.Application.Warning("Negative price input corrected", 
                            ("Field", textBox.Name),
                            ("OriginalValue", price),
                            ("CorrectedValue", 0));
                    }
                    else
                    {
                        // Valid price - no formatting needed for integers
                        ClearValidationError(textBox);
                    }
                }
                else
                {
                    // Invalid numeric input - revert to previous valid value or 0
                    var previousValue = GetPreviousValidPrice(textBox);
                    textBox.Text = previousValue.ToString();
                    ShowValidationError(textBox, "Please enter a valid whole number (e.g., 5)");
                    LoggingService.Application.Warning("Invalid price input corrected", 
                        ("Field", textBox.Name),
                        ("InvalidInput", inputText),
                        ("CorrectedValue", previousValue));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error validating pricing input", ex,
                    ("Field", textBox.Name),
                    ("Input", textBox.Text));
                // Fallback to safe value
                textBox.Text = "0";
            }
            finally
            {
                _isValidatingInput = false;
            }
        }

        /// <summary>
        /// Validate discount input and provide user feedback
        /// </summary>
        private void ValidateDiscountInput(TextBox textBox)
        {
            if (_isValidatingInput) return; // Prevent recursive validation
            
            try
            {
                _isValidatingInput = true;
                var inputText = textBox.Text?.Trim() ?? "";
                
                // Allow empty input during typing
                if (string.IsNullOrEmpty(inputText))
                {
                    ClearValidationError(textBox);
                    return;
                }

                // Try to parse the input as decimal
                if (decimal.TryParse(inputText, out decimal discount))
                {
                    // Validate discount range (0-100)
                    if (discount < 0)
                    {
                        // Clamp negative values to 0
                        textBox.Text = "0";
                        ShowValidationError(textBox, "Discount cannot be negative. Set to 0%.");
                        LoggingService.Application.Warning("Negative discount input corrected", 
                            ("Field", textBox.Name),
                            ("OriginalValue", discount),
                            ("CorrectedValue", 0));
                    }
                    else if (discount > 100)
                    {
                        // Clamp values over 100 to 100
                        textBox.Text = "100";
                        ShowValidationError(textBox, "Discount cannot exceed 100%. Set to 100%.");
                        LoggingService.Application.Warning("Excessive discount input corrected", 
                            ("Field", textBox.Name),
                            ("OriginalValue", discount),
                            ("CorrectedValue", 100));
                    }
                    else
                    {
                        // Valid discount - format as whole number
                        textBox.Text = ((int)discount).ToString();
                        ClearValidationError(textBox);
                    }
                }
                else
                {
                    // Invalid numeric input - revert to previous valid value or 0
                    var previousValue = GetPreviousValidDiscount(textBox);
                    textBox.Text = previousValue.ToString();
                    ShowValidationError(textBox, "Please enter a valid discount (0-100)");
                    LoggingService.Application.Warning("Invalid discount input corrected", 
                        ("Field", textBox.Name),
                        ("InvalidInput", inputText),
                        ("CorrectedValue", previousValue));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error validating discount input", ex,
                    ("Field", textBox.Name),
                    ("Input", textBox.Text));
                // Fallback to safe value
                textBox.Text = "0";
            }
            finally
            {
                _isValidatingInput = false;
            }
        }

        /// <summary>
        /// Get the previous valid price for a text box (fallback to 0)
        /// </summary>
        private int GetPreviousValidPrice(TextBox textBox)
        {
            // Try to get from current product data if available
            if (_products?.Count > 0)
            {
                var productType = GetProductTypeFromTextBoxName(textBox.Name);
                var product = _products.FirstOrDefault(p => p.ProductType.ToString() == productType);
                if (product != null)
                {
                    return (int)product.Price; // Use base product price as fallback
                }
            }
            return 0; // Default fallback
        }

        /// <summary>
        /// Get the previous valid discount for a text box (fallback to 0)
        /// </summary>
        private int GetPreviousValidDiscount(TextBox textBox)
        {
            // Default discount is 0%
            return 0;
        }

        /// <summary>
        /// Extract product type from text box name
        /// </summary>
        private string GetProductTypeFromTextBoxName(string textBoxName)
        {
            if (textBoxName.Contains("Strips"))
                return "PhotoStrips";
            else if (textBoxName.Contains("Photo4x6"))
                return "Photo4x6";
            else if (textBoxName.Contains("Smartphone"))
                return "SmartphonePrint";
            else
                return "Unknown";
        }

        /// <summary>
        /// Show validation error for a text box
        /// </summary>
        private void ShowValidationError(TextBox textBox, string errorMessage)
        {
            // Set error styling - since TextBoxes have BorderThickness="0", we only set the ToolTip
            textBox.ToolTip = errorMessage;
            
            // Show error indicator if available
            var errorIndicator = FindName("UnsavedChangesIndicator") as TextBlock;
            if (errorIndicator != null)
            {
                errorIndicator.Text = "Validation Error";
                errorIndicator.Foreground = System.Windows.Media.Brushes.Red;
                errorIndicator.Visibility = Visibility.Visible;
            }
            
            // Log the validation error
            LoggingService.Application.Warning("Pricing input validation error", 
                ("Field", textBox.Name),
                ("Error", errorMessage));
        }

        /// <summary>
        /// Clear validation error for a text box
        /// </summary>
        private void ClearValidationError(TextBox textBox)
        {
            // Clear error styling - since TextBoxes have BorderThickness="0", we need to clear the ToolTip
            textBox.ToolTip = null;
            
            // Hide error indicator if no other errors
            if (!HasAnyValidationErrors())
            {
                var errorIndicator = FindName("UnsavedChangesIndicator") as TextBlock;
                if (errorIndicator != null)
                {
                    errorIndicator.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Check if there are any validation errors in pricing inputs
        /// </summary>
        private bool HasAnyValidationErrors()
        {
            var pricingInputs = new[]
            {
                StripsExtraCopyPriceInput,
                Photo4x6ExtraCopyPriceInput,
                SmartphoneExtraCopyPriceInput,
                StripsMultipleCopyDiscountInput,
                Photo4x6MultipleCopyDiscountInput,
                SmartphoneMultipleCopyDiscountInput
            };

            // Since TextBoxes have BorderThickness="0", we check for ToolTip instead of BorderBrush
            return pricingInputs.Any(tb => tb?.ToolTip != null);
        }

        /// <summary>
        /// Clear all validation errors from pricing inputs
        /// </summary>
        private void ClearAllValidationErrors()
        {
            var pricingInputs = new[]
            {
                StripsExtraCopyPriceInput,
                Photo4x6ExtraCopyPriceInput,
                SmartphoneExtraCopyPriceInput,
                StripsMultipleCopyDiscountInput,
                Photo4x6MultipleCopyDiscountInput,
                SmartphoneMultipleCopyDiscountInput
            };

            foreach (var textBox in pricingInputs)
            {
                if (textBox != null)
                {
                    textBox.ToolTip = null;
                }
            }

            // Hide error indicator
            var errorIndicator = FindName("UnsavedChangesIndicator") as TextBlock;
            if (errorIndicator != null)
            {
                errorIndicator.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Load extra copy pricing values from database into UI
        /// </summary>
        private void LoadExtraCopyPricingUI(Product product)
        {
            try
            {
                // Clear any existing validation errors when loading from database
                ClearAllValidationErrors();

                // Set the custom pricing toggle
                if (UseCustomExtraCopyPricingCheckBox != null)
                {
                    UseCustomExtraCopyPricingCheckBox.IsChecked = product.UseCustomExtraCopyPricing;
                }

                // Show/hide custom pricing section based on toggle
                if (CustomPricingSection != null)
                {
                    CustomPricingSection.Visibility = product.UseCustomExtraCopyPricing ? Visibility.Visible : Visibility.Collapsed;
                }

                // Update description
                if (CustomPricingDescription != null)
                {
                    if (product.UseCustomExtraCopyPricing)
                    {
                        CustomPricingDescription.Text = "Configure product-specific pricing for extra copies";
                    }
                    else
                    {
                        CustomPricingDescription.Text = $"Extra copies will cost ${product.Price:F2} each (same as base price)";
                    }
                }

                // Load simplified product-specific pricing values (or defaults)
                // Photo Strips pricing
                if (StripsExtraCopyPriceInput != null)
                {
                    StripsExtraCopyPriceInput.Text = ((int)(product.StripsExtraCopyPrice ?? 6.00m)).ToString();
                }
                if (StripsMultipleCopyDiscountInput != null)
                {
                    StripsMultipleCopyDiscountInput.Text = ((int)(product.StripsMultipleCopyDiscount ?? 0.00m)).ToString();
                }

                // 4x6 Photos pricing
                if (Photo4x6ExtraCopyPriceInput != null)
                {
                    Photo4x6ExtraCopyPriceInput.Text = ((int)(product.Photo4x6ExtraCopyPrice ?? 6.00m)).ToString();
                }
                if (Photo4x6MultipleCopyDiscountInput != null)
                {
                    Photo4x6MultipleCopyDiscountInput.Text = ((int)(product.Photo4x6MultipleCopyDiscount ?? 0.00m)).ToString();
                }

                // Smartphone Print pricing
                if (SmartphoneExtraCopyPriceInput != null)
                {
                    SmartphoneExtraCopyPriceInput.Text = ((int)(product.SmartphoneExtraCopyPrice ?? 6.00m)).ToString();
                }
                if (SmartphoneMultipleCopyDiscountInput != null)
                {
                    SmartphoneMultipleCopyDiscountInput.Text = ((int)(product.SmartphoneMultipleCopyDiscount ?? 0.00m)).ToString();
                }

                // Update pricing examples with loaded values
                UpdatePricingExamples();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load extra copy pricing UI", ex);
            }
        }

        #endregion

        /// <summary>
        /// Update the base price displays in the pricing sections to show current database values
        /// </summary>
        private void UpdateBasePriceDisplays()
        {
            try
            {
                // Find products by ProductType enum
                var photoStrips = _products.FirstOrDefault(p => p.ProductType == ProductType.PhotoStrips);
                var photo4x6 = _products.FirstOrDefault(p => p.ProductType == ProductType.Photo4x6);
                var smartphonePrint = _products.FirstOrDefault(p => p.ProductType == ProductType.SmartphonePrint);

                // Update Photo Strips base price display
                if (photoStrips != null)
                {
                    var photoStripsBasePriceText = this.FindName("PhotoStripsBasePriceText") as TextBlock;
                    if (photoStripsBasePriceText != null)
                    {
                        photoStripsBasePriceText.Text = $"Pricing for Photo Strips extra copies (Base: ${photoStrips.Price:F2})";
                    }
                }

                // Update 4x6 Photos base price display
                if (photo4x6 != null)
                {
                    var photo4x6BasePriceText = this.FindName("Photo4x6BasePriceText") as TextBlock;
                    if (photo4x6BasePriceText != null)
                    {
                        photo4x6BasePriceText.Text = $"Pricing for 4x6 Photos extra copies (Base: ${photo4x6.Price:F2})";
                    }
                }

                // Update Smartphone Print base price display
                if (smartphonePrint != null)
                {
                    var smartphoneBasePriceText = this.FindName("SmartphoneBasePriceText") as TextBlock;
                    if (smartphoneBasePriceText != null)
                    {
                        smartphoneBasePriceText.Text = $"Pricing for Smartphone Print extra copies (Base: ${smartphonePrint.Price:F2})";
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update base price displays", ex);
            }
        }

        /// <summary>
        /// Helper method to find TemplatesTabControl in the content
        /// </summary>
        private Views.TemplatesTabControl? FindTemplatesTabControl(Panel parent)
        {
            foreach (var child in parent.Children)
            {
                if (child is Views.TemplatesTabControl templatesControl)
                {
                    return templatesControl;
                }
                
                if (child is Panel childPanel)
                {
                    var found = FindTemplatesTabControl(childPanel);
                    if (found != null) return found;
                }
            }
            return null;
        }

        #region Credit Management

        // NOTE: This credit management system is production-ready and designed to work with firmware integration.
        // When payment hardware (cash acceptor, card reader) is connected, the firmware will call AddCreditsAsync()
        // to automatically add credits when customers make payments. Manual credit addition is available for
        // administrative purposes, promotions, and testing.

        /// <summary>
        /// Load current credits from database settings
        /// </summary>
        private async Task LoadCreditsAsync()
        {
            try
            {
                var result = await _databaseService.GetSettingValueAsync<decimal>("System", "CurrentCredits");
                
                if (result.Success)
                {
                    _currentCredits = result.Data;
                }
                else
                {
                    _currentCredits = 0;
                }
                
                // Also load credit history and transaction count
                await LoadCreditHistoryAsync();
                await LoadTransactionCountAsync();
                
                UpdateCreditsDisplay();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load credits", ex);
                _currentCredits = 0;
                UpdateCreditsDisplay();
                NotificationService.Instance.ShowError("Error Loading Credits", "Failed to load credit balance from database");
            }
        }

        /// <summary>
        /// Load credit history from database
        /// </summary>
        private async Task LoadCreditHistoryAsync()
        {
            try
            {
                var result = await _databaseService.GetCreditTransactionsAsync(10); // Load last 10 for display
                
                // Ensure UI updates happen on the UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    if (result.Success && result.Data != null)
                    {
                        _creditHistory.Clear();
                        _creditHistory.AddRange(result.Data);
                    }
                    else
                    {
                        _creditHistory.Clear();
                    }
                    
                    UpdateCreditHistoryDisplay();
                });
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load credit history", ex);
                
                // Ensure UI updates happen on the UI thread even on error
                await Dispatcher.InvokeAsync(() =>
                {
                    _creditHistory.Clear();
                    UpdateCreditHistoryDisplay();
                });
            }
        }

        /// <summary>
        /// Save current credits to database settings
        /// </summary>
        private async Task SaveCreditsAsync()
        {
            try
            {
                var result = await _databaseService.SetSettingValueAsync("System", "CurrentCredits", _currentCredits, null);
                
                if (!result.Success)
                {
                    LoggingService.Application.Error("Failed to save credits to database", null, ("Error", result.ErrorMessage ?? "Unknown error"));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to save credits", ex);
            }
        }

        /// <summary>
        /// Save credits to database and return success status
        /// </summary>
        private async Task<bool> SaveCreditsWithResultAsync()
        {
            try
            {
                var result = await _databaseService.SetSettingValueAsync("System", "CurrentCredits", _currentCredits, null);
                
                if (!result.Success)
                {
                    LoggingService.Application.Error("Failed to save credits to database", null, ("Error", result.ErrorMessage ?? "Unknown error"));
                }
                
                return result.Success;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to save credits", ex);
                return false;
            }
        }

        /// <summary>
        /// Update the credits display in the UI
        /// </summary>
        private void UpdateCreditsDisplay()
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(UpdateCreditsDisplay);
                    return;
                }
                if (CurrentCreditsText != null)
                {
                    string displayText;
                    if (_mainWindow?.IsFreePlayMode == true)
                    {
                        displayText = "Free Play Mode";
                    }
                    else
                    {
                        displayText = $"Credits: ${_currentCredits:F2}";
                    }
                    CurrentCreditsText.Text = displayText;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update credits display", ex);
            }
        }

        /// <summary>
        /// Add credits to the system manually
        /// </summary>
        private async Task AddCreditsAsync(decimal amount, string description)
        {
            try
            {
                _currentCredits += amount;
                await SaveCreditsAsync();
                
                // Add to database history
                var transaction = new CreditTransaction
                {
                    Amount = amount,
                    TransactionType = CreditTransactionType.Add,
                    Description = description,
                    BalanceAfter = _currentCredits,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null // Use null for system operations to avoid foreign key issues
                };
                
                var insertResult = await _databaseService.InsertCreditTransactionAsync(transaction);
                if (!insertResult.Success)
                {
                    LoggingService.Application.Warning("Failed to save credit transaction to database", ("Error", insertResult.ErrorMessage ?? "Unknown error"));
                    // Still show in memory for immediate feedback
                    _creditHistory.Insert(0, transaction);
                    UpdateCreditHistoryDisplay();
                }
                else
                {
                    // Successfully saved to database, reload from database to show updated history
                    await LoadCreditHistoryAsync();
                }
                
                // Update credits display
                UpdateCreditsDisplay();
                
                LoggingService.Application.Information("Credits added",
                    ("Amount", amount),
                    ("Description", description),
                    ("NewBalance", _currentCredits));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to add credits", ex);
                NotificationService.Instance.ShowError("Error Adding Credits", "Failed to add credits to the system. Please try again.");
            }
        }

        /// <summary>
        /// Deduct credits from the system (for purchases)
        /// </summary>
        public async Task<bool> DeductCreditsAsync(decimal amount, string description, int? relatedTransactionId = null)
        {
            try
            {
                if (_currentCredits < amount)
                {
                    LoggingService.Application.Warning("Insufficient credits for purchase",
                        ("Required", amount),
                        ("Available", _currentCredits));
                    return false;
                }
                
                var originalCredits = _currentCredits;
                _currentCredits -= amount;
                
                // Try to save to database - if this fails, we need to rollback
                var saveResult = await SaveCreditsWithResultAsync();
                if (!saveResult)
                {
                    // Rollback the in-memory change
                    _currentCredits = originalCredits;
                    UpdateCreditsDisplay();
                    
                    LoggingService.Application.Error("Failed to save credit deduction to database - transaction rolled back");
                    return false;
                }
                
                // Add to database history
                var transaction = new CreditTransaction
                {
                    Amount = -amount,
                    TransactionType = CreditTransactionType.Deduct,
                    Description = description,
                    BalanceAfter = _currentCredits,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null, // Use null for system operations to avoid foreign key issues
                    RelatedTransactionId = relatedTransactionId // Link to sales transaction
                };
                
                var insertResult = await _databaseService.InsertCreditTransactionAsync(transaction);
                if (!insertResult.Success)
                {
                    LoggingService.Application.Warning("Failed to save credit transaction to database", ("Error", insertResult.ErrorMessage ?? "Unknown error"));
                    // Still show in memory for immediate feedback
                    _creditHistory.Insert(0, transaction);
                    UpdateCreditHistoryDisplay();
                }
                else
                {
                    // Successfully saved to database, reload from database to show updated history
                    await LoadCreditHistoryAsync();
                }
                
                // Update credits display
                UpdateCreditsDisplay();
                
                LoggingService.Application.Information("Credits deducted",
                    ("Amount", amount),
                    ("Description", description),
                    ("NewBalance", _currentCredits),
                    ("RelatedTransactionId", relatedTransactionId ?? (object)"None"));
                
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to deduct credits", ex);
                return false;
            }
        }

        /// <summary>
        /// Check if sufficient credits are available
        /// </summary>
        public bool HasSufficientCredits(decimal amount)
        {
            return _currentCredits >= amount;
        }

        /// <summary>
        /// Get current credit balance
        /// </summary>
        public decimal GetCurrentCredits()
        {
            return _currentCredits;
        }

        /// <summary>
        /// Refresh credits from database (public method for external use)
        /// </summary>
        public async Task RefreshCreditsFromDatabase()
        {
            await LoadCreditsAsync();
        }

        /// <summary>
        /// Reset credits to zero
        /// </summary>
        private async Task ResetCreditsAsync()
        {
            try
            {
                var oldBalance = _currentCredits;
                _currentCredits = 0;
                await SaveCreditsAsync();
                
                // Add to database history
                var transaction = new CreditTransaction
                {
                    Amount = -oldBalance,
                    TransactionType = CreditTransactionType.Reset,
                    Description = "Credits reset to $0",
                    BalanceAfter = _currentCredits,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null // Use null for system operations to avoid foreign key issues
                };
                
                var insertResult = await _databaseService.InsertCreditTransactionAsync(transaction);
                if (!insertResult.Success)
                {
                    LoggingService.Application.Warning("Failed to save credit transaction to database", ("Error", insertResult.ErrorMessage ?? "Unknown error"));
                    // Still show in memory for immediate feedback
                    _creditHistory.Insert(0, transaction);
                    UpdateCreditHistoryDisplay();
                }
                else
                {
                    // Successfully saved to database, reload from database to show updated history
                    await LoadCreditHistoryAsync();
                }
                
                // Update credits display
                UpdateCreditsDisplay();
                
                LoggingService.Application.Information("Credits reset to zero",
                    ("PreviousBalance", oldBalance));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to reset credits", ex);
                NotificationService.Instance.ShowError("Error Resetting Credits", "Failed to reset credits. Please try again.");
            }
        }

        /// <summary>
        /// Update credit history display
        /// </summary>
        private void UpdateCreditHistoryDisplay()
        {
            if (CreditHistoryPanel == null) return;

            CreditHistoryPanel.Children.Clear();

            if (_creditHistory.Count == 0)
            {
                // Show empty state
                var emptyBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                
                var emptyGrid = new Grid();
                emptyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                var emptyText = new TextBlock
                {
                    Text = "No credit activity yet",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                emptyGrid.Children.Add(emptyText);
                emptyBorder.Child = emptyGrid;
                CreditHistoryPanel.Children.Add(emptyBorder);
                return;
            }

            foreach (var transaction in _creditHistory.Take(8)) // Show only last 8 for better display
            {
                var border = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = new TextBlock
                {
                    Text = transaction.Type switch
                    {
                        CreditTransactionType.Add => "➕",
                        CreditTransactionType.Deduct => "➖", 
                        CreditTransactionType.Reset => "🔄",
                        _ => "💰"
                    },
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        transaction.Type == CreditTransactionType.Add ? "#10B981" : "#EF4444")),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(icon, 0);

                var description = new TextBlock
                {
                    Text = transaction.Description,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(description, 1);

                var amount = new TextBlock
                {
                    Text = $"${Math.Abs(transaction.Amount):F2}",
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        transaction.Type == CreditTransactionType.Add ? "#10B981" : "#EF4444")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(amount, 2);

                grid.Children.Add(icon);
                grid.Children.Add(description);
                grid.Children.Add(amount);
                border.Child = grid;
                CreditHistoryPanel.Children.Add(border);
            }
        }

        #endregion

        #region Credit UI Event Handlers

        /// <summary>
        /// Handle refresh credits button click
        /// </summary>
        private async void RefreshCredits_Click(object sender, RoutedEventArgs e)
        {
            await LoadCreditsAsync(); // This now loads both credits and history
        }
        
        /// <summary>
        /// Handle refresh transactions button click
        /// </summary>
        private async void RefreshTransactions_Click(object sender, RoutedEventArgs e)
        {
            await LoadTransactionHistory();
        }

        /// <summary>
        /// Handle reset credits button click
        /// </summary>
        private async void ResetCredits_Click(object sender, RoutedEventArgs e)
        {
            var result = ConfirmationDialog.ShowConfirmation(
                "Reset Credits",
                "Are you sure you want to reset all credits to $0?\n\nThis action cannot be undone.",
                "Reset",
                "Cancel",
                Window.GetWindow(this));

            if (result)
            {
                await ResetCreditsAsync();
                NotificationService.Instance.ShowSuccess("Credits Reset", "All credits have been reset to $0.00");
            }
        }

        /// <summary>
        /// Handle quick add credit button clicks for manual credit management
        /// </summary>
        private async void QuickAddCredit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagValue && decimal.TryParse(tagValue, out decimal amount))
            {
                await AddCreditsAsync(amount, $"Quick add ${amount}");
                NotificationService.Instance.ShowSuccess("Credits Added", $"Successfully added ${amount:F2} to credit balance!");
            }
        }

        /// <summary>
        /// Handle custom add credit button click
        /// </summary>
        private async void AddCustomCredit_Click(object sender, RoutedEventArgs e)
        {
            if (CustomCreditNumberAmount != null && decimal.TryParse(CustomCreditNumberAmount.Text, out decimal amount) && amount > 0)
            {
                await AddCreditsAsync(amount, $"Custom add ${amount}");
                CustomCreditNumberAmount.Text = "0";
                NotificationService.Instance.ShowSuccess("Credits Added", $"Successfully added ${amount:F2} to credit balance!");
            }
            else
            {
                NotificationService.Instance.ShowWarning("Invalid Amount", "Please enter a valid amount greater than $0.00");
            }
        }

        /// <summary>
        /// Handle clear credit history button click
        /// </summary>
        private async void ClearCreditHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = ConfirmationDialog.ShowConfirmation(
                "Clear History",
                "Are you sure you want to clear the credit history?\n\nThis will not affect your current credit balance.",
                "Clear",
                "Cancel",
                Window.GetWindow(this));

            if (result)
            {
                try
                {
                    // Delete all credit transactions from database (keep 0)
                    await _databaseService.DeleteOldCreditTransactionsAsync(0);
                    
                    // Reload empty history
                    await LoadCreditHistoryAsync();
                    
                    NotificationService.Instance.ShowSuccess("History Cleared", "Credit transaction history has been cleared");
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Error("Failed to clear credit history", ex);
                    NotificationService.Instance.ShowError("Error Clearing History", "Failed to clear credit history from database");
                }
            }
        }

        /// <summary>
        /// Handle export transactions button click
        /// </summary>
        private async void ExportTransactions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get all credit transactions from database
                var result = await _databaseService.GetCreditTransactionsAsync(10000); // Get all transactions
                if (!result.Success || result.Data == null)
                {
                    NotificationService.Instance.ShowError("Export Failed", "Failed to retrieve transaction data from database");
                    return;
                }

                var transactions = result.Data;
                if (transactions.Count == 0)
                {
                    NotificationService.Instance.ShowWarning("No Data", "No credit transactions found to export");
                    return;
                }

                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = $"CreditTransactions_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "Export Credit Transactions"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Create CSV content
                    var csvContent = new StringBuilder();
                    csvContent.AppendLine("Date,Time,Type,Amount,Description,Balance After,Created By");

                    foreach (var transaction in transactions.OrderByDescending(t => t.CreatedAt))
                    {
                        csvContent.AppendLine($"\"{transaction.CreatedAt:yyyy-MM-dd}\",\"{transaction.CreatedAt:HH:mm:ss}\",\"{transaction.TransactionType}\",\"{transaction.Amount:F2}\",\"{transaction.Description.Replace("\"", "\"\"")}\",\"{transaction.BalanceAfter:F2}\",\"{transaction.CreatedBy ?? "System"}\"");
                    }

                    // Write to file
                    await File.WriteAllTextAsync(saveFileDialog.FileName, csvContent.ToString());
                    
                    NotificationService.Instance.ShowSuccess("Export Complete", $"Exported {transactions.Count} transactions to {Path.GetFileName(saveFileDialog.FileName)}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to export credit transactions", ex);
                NotificationService.Instance.ShowError("Export Failed", "An error occurred while exporting transaction data");
            }
        }

        /// <summary>
        /// Handle cleanup database button click
        /// </summary>
        private async void CleanupDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get current transaction count
                var countResult = await _databaseService.GetCreditTransactionsAsync(10000);
                var currentCount = countResult.Success && countResult.Data != null ? countResult.Data.Count : 0;

                if (currentCount <= 1000)
                {
                    NotificationService.Instance.ShowInfo("No Cleanup Needed", $"Database has {currentCount} transactions. Cleanup only needed when over 1000 records.");
                    return;
                }

                var recordsToDelete = currentCount - 1000;
                var result = ConfirmationDialog.ShowConfirmation(
                    "Cleanup Database",
                    $"This will permanently delete {recordsToDelete} old transactions, keeping the most recent 1000.\n\nRecommend exporting data first for permanent records.\n\nContinue?",
                    "Cleanup",
                    "Cancel",
                    Window.GetWindow(this));

                if (result)
                {
                    var cleanupResult = await _databaseService.DeleteOldCreditTransactionsAsync(1000);
                    if (cleanupResult.Success)
                    {
                        await LoadTransactionCountAsync(); // Refresh the display
                        NotificationService.Instance.ShowSuccess("Cleanup Complete", $"Removed {recordsToDelete} old transactions. Database now has 1000 records.");
                    }
                    else
                    {
                        NotificationService.Instance.ShowError("Cleanup Failed", "Failed to cleanup old transactions from database");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to cleanup credit transactions", ex);
                NotificationService.Instance.ShowError("Cleanup Failed", "An error occurred during database cleanup");
            }
        }

        /// <summary>
        /// Load and display transaction count
        /// </summary>
        private async Task LoadTransactionCountAsync()
        {
            try
            {
                var result = await _databaseService.GetCreditTransactionsAsync(10000); // Get all to count
                var count = result.Success && result.Data != null ? result.Data.Count : 0;
                
                if (TotalTransactionsText != null)
                {
                    TotalTransactionsText.Text = count.ToString();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load transaction count", ex);
                if (TotalTransactionsText != null)
                {
                    TotalTransactionsText.Text = "Error";
                }
            }
        }

        #endregion

        #region Transaction History Management

        /// <summary>
        /// Load transaction history from database
        /// </summary>
        private async Task LoadTransactionHistory()
        {
            try
            {
                var result = await _databaseService.GetRecentTransactionsAsync(20);
                
                await Dispatcher.InvokeAsync(() =>
                {
                    if (result.Success && result.Data != null)
                    {
                        _recentTransactions = result.Data;
                    }
                    else
                    {
                        _recentTransactions.Clear();
                    }
                    
                    UpdateTransactionHistoryDisplay();
                });
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load transaction history", ex);
                
                await Dispatcher.InvokeAsync(() =>
                {
                    _recentTransactions.Clear();
                    UpdateTransactionHistoryDisplay();
                });
            }
        }

        /// <summary>
        /// Update the transaction history display in the UI
        /// </summary>
        private void UpdateTransactionHistoryDisplay()
        {
            try
            {
                if (TransactionHistoryPanel == null) return;
                
                // Clear existing transaction items (keep the no transactions message)
                var itemsToRemove = TransactionHistoryPanel.Children
                    .OfType<Border>()
                    .ToList();
                
                foreach (var item in itemsToRemove)
                {
                    TransactionHistoryPanel.Children.Remove(item);
                }
                
                if (_recentTransactions.Count == 0)
                {
                    // Show no transactions message
                    if (NoTransactionsMessage != null)
                    {
                        NoTransactionsMessage.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    // Hide no transactions message
                    if (NoTransactionsMessage != null)
                    {
                        NoTransactionsMessage.Visibility = Visibility.Collapsed;
                    }
                    
                    // Add transaction items
                    foreach (var transaction in _recentTransactions)
                    {
                        var transactionItem = CreateTransactionListItem(transaction);
                        TransactionHistoryPanel.Children.Add(transactionItem);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update transaction history display", ex);
            }
        }

        /// <summary>
        /// Create a transaction list item for the UI
        /// </summary>
        private Border CreateTransactionListItem(Transaction transaction)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 16, 16, 16),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderThickness = new Thickness(1, 1, 1, 1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left column - Transaction details
            var leftPanel = new StackPanel();
            
            var transactionCode = new TextBlock
            {
                Text = $"Transaction #{transaction.TransactionCode ?? transaction.Id.ToString()}",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")),
                Margin = new Thickness(0, 0, 0, 4)
            };
            leftPanel.Children.Add(transactionCode);

            var dateTime = new TextBlock
            {
                Text = transaction.CreatedAt.ToString("MMM dd, yyyy - hh:mm tt"),
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
            };
            leftPanel.Children.Add(dateTime);

            Grid.SetColumn(leftPanel, 0);
            grid.Children.Add(leftPanel);

            // Middle column - Product details
            var middlePanel = new StackPanel();
            
            var productInfo = new TextBlock
            {
                Text = transaction.Notes ?? "Photo Session",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Margin = new Thickness(0, 0, 0, 4)
            };
            middlePanel.Children.Add(productInfo);

            var quantityInfo = new TextBlock
            {
                Text = $"Quantity: {transaction.Quantity}",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
            };
            middlePanel.Children.Add(quantityInfo);

            Grid.SetColumn(middlePanel, 1);
            grid.Children.Add(middlePanel);

            // Right column - Payment info
            var rightPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var amount = new TextBlock
            {
                Text = $"${transaction.TotalPrice:F2}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")),
                Margin = new Thickness(0, 0, 0, 4)
            };
            rightPanel.Children.Add(amount);

            var paymentStatus = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    transaction.PaymentStatus == PaymentStatus.Completed ? "#D1FAE5" : "#FEF3C7")),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4)
            };

            var statusText = new TextBlock
            {
                Text = transaction.PaymentStatus.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    transaction.PaymentStatus == PaymentStatus.Completed ? "#065F46" : "#92400E"))
            };
            paymentStatus.Child = statusText;
            rightPanel.Children.Add(paymentStatus);

            Grid.SetColumn(rightPanel, 2);
            grid.Children.Add(rightPanel);

            border.Child = grid;
            return border;
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleanup diagnostic resources when admin dashboard is closed
        /// </summary>
        public void CleanupDiagnosticResources()
        {
            try
            {
                StopDiagnosticCamera();
                LogToDiagnostics("Admin dashboard cleanup completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to cleanup diagnostic resources", ex);
            }
        }

        #endregion

        #region Diagnostics Event Handlers

        /// <summary>
        /// Initialize diagnostic system info when diagnostics tab is loaded
        /// </summary>
        private void InitializeDiagnosticsTab()
        {
            try
            {
                Console.WriteLine("AdminDashboard: Initializing diagnostics tab");
                
                // Update system information
                if (OSVersionText != null)
                    OSVersionText.Text = Environment.OSVersion.ToString();
                
                if (AppVersionText != null)
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var version = assembly.GetName().Version;
                    AppVersionText.Text = version?.ToString() ?? "Unknown";
                }
                
                if (UptimeText != null)
                {
                    var uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
                    UptimeText.Text = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
                }
                
                // Load camera settings from database
                _ = LoadCameraSettingsFromDatabaseAsync();
                
                _ = LoadFreeCreditUsageAsync();
                LogToDiagnostics("Diagnostics system initialized - loading camera settings from database");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AdminDashboard: Error initializing diagnostics tab - {ex.Message}");
                LoggingService.Application.Error("Failed to initialize diagnostics tab", ex);
                LogToDiagnostics($"ERROR: Failed to initialize diagnostics - {ex.Message}");
            }
        }

        /// <summary>
        /// Load camera settings from database
        /// </summary>
        private async Task LoadCameraSettingsFromDatabaseAsync()
        {
            try
            {
                Console.WriteLine("AdminDashboard: Loading camera settings from database");
                
                // Load brightness
                var brightnessResult = await _databaseService.GetCameraSettingAsync("Brightness");
                if (brightnessResult.Success && brightnessResult.Data != null)
                {
                    _savedBrightness = brightnessResult.Data.SettingValue;
                    Console.WriteLine($"AdminDashboard: Loaded brightness = {_savedBrightness}");
                }

                // Load zoom
                var zoomResult = await _databaseService.GetCameraSettingAsync("Zoom");
                if (zoomResult.Success && zoomResult.Data != null)
                {
                    _savedZoom = zoomResult.Data.SettingValue;
                    Console.WriteLine($"AdminDashboard: Loaded zoom = {_savedZoom}");
                }

                // Load contrast
                var contrastResult = await _databaseService.GetCameraSettingAsync("Contrast");
                if (contrastResult.Success && contrastResult.Data != null)
                {
                    _savedContrast = contrastResult.Data.SettingValue;
                    Console.WriteLine($"AdminDashboard: Loaded contrast = {_savedContrast}");
                }
                    
                    // Update UI sliders with loaded values
                    Dispatcher.Invoke(() =>
                    {
                        if (BrightnessSlider != null) BrightnessSlider.Value = _savedBrightness;
                        if (ZoomSlider != null) ZoomSlider.Value = _savedZoom;
                        if (ContrastSlider != null) ContrastSlider.Value = _savedContrast;
                    });
                    
                    LogToDiagnostics($"Camera settings loaded from database: Brightness={_savedBrightness}, Zoom={_savedZoom}, Contrast={_savedContrast}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AdminDashboard: Error loading camera settings from database - {ex.Message}");
                LogToDiagnostics($"ERROR: Failed to load camera settings from database - {ex.Message}");
            }
        }

        /// <summary>
        /// Load free credit usage tracking
        /// </summary>
        private async Task LoadFreeCreditUsageAsync()
        {
            try
            {
                // Get total free credits issued from credit transactions
                var result = await _databaseService.GetCreditTransactionsAsync();
                if (result.Success && result.Data != null)
                {
                    var freeCreditsTotal = result.Data
                        .Where(ct => ct.TransactionType == CreditTransactionType.Add && 
                                    (ct.Description?.Contains("Free Credit") == true || ct.CreatedBy == "System"))
                        .Sum(ct => ct.Amount);
                    
                    if (FreeCreditUsedText != null)
                        FreeCreditUsedText.Text = $"${freeCreditsTotal:F2}";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load free credit usage", ex);
                if (FreeCreditUsedText != null)
                    FreeCreditUsedText.Text = "Error";
            }
        }

        /// <summary>
        /// Log message to diagnostic console
        /// </summary>
        private void LogToDiagnostics(string message)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (DiagnosticLogText != null)
                    {
                        var timestamp = DateTime.Now.ToString("HH:mm:ss");
                        var currentText = DiagnosticLogText.Text;
                        
                        if (currentText == "Diagnostic log will appear here...")
                            currentText = "";
                        
                        var newText = $"[{timestamp}] {message}\n{currentText}";
                        
                        // Keep only last 50 lines
                        var lines = newText.Split('\n');
                        if (lines.Length > 50)
                            newText = string.Join("\n", lines.Take(50));
                        
                        DiagnosticLogText.Text = newText;
                        
                        // Auto scroll to top
                        if (DiagnosticLogScrollViewer != null)
                            DiagnosticLogScrollViewer.ScrollToTop();
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to log to diagnostics", ex);
            }
        }

        /// <summary>
        /// Update hardware status indicator
        /// </summary>
        private void UpdateHardwareStatus(string hardware, bool isConnected, string message, string? statusOverride = null)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    Border? indicator = null;
                    TextBlock? statusText = null;
                    TextBlock? statusMessage = null;
                    
                    switch (hardware.ToLower())
                    {
                        case "camera":
                            indicator = CameraStatusIndicator;
                            statusText = CameraStatusText;
                            statusMessage = CameraStatusMessage;
                            break;
                        case "printer":
                            indicator = PrinterStatusIndicator;
                            statusText = PrinterStatusText;
                            statusMessage = PrinterStatusMessage;
                            break;
                        case "arduino":
                            indicator = ArduinoStatusIndicator;
                            statusText = ArduinoStatusText;
                            statusMessage = ArduinoStatusMessage;
                            break;
                    }
                    
                    if (indicator != null && statusText != null && statusMessage != null)
                    {
                        if (isConnected)
                        {
                            // Professional green for connected/active state
                            indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F9FF"));
                            indicator.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                            statusText.Text = statusOverride ?? "Connected";
                            statusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0C4A6E"));
                        }
                        else
                        {
                            // Determine if this is an error or just inactive/stopped
                            bool isError = message.ToLower().Contains("error") || message.ToLower().Contains("failed") || message.ToLower().Contains("not found");
                            
                            if (isError)
                            {
                                // Professional red for actual errors
                                indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2"));
                                indicator.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                                statusText.Text = statusOverride ?? "Error";
                                statusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                            }
                            else
                            {
                                // Professional gray for inactive/stopped state
                                indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                                indicator.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                                statusText.Text = statusOverride ?? "Inactive";
                                statusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
                            }
                        }
                        statusMessage.Text = message;
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error($"Failed to update {hardware} status", ex);
            }
        }

        // Camera Testing Event Handlers
        private async void TestCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Testing camera connection...");
                
                // Use existing camera service
                var cameraService = new CameraService();
                var cameras = cameraService.GetAvailableCameras();
                
                if (cameras.Count > 0)
                {
                    LogToDiagnostics($"Found {cameras.Count} camera(s): {string.Join(", ", cameras.Select(c => c.Name))}");
                    
                    bool started = await cameraService.StartCameraAsync();
                    if (started)
                    {
                        UpdateHardwareStatus("camera", true, "Camera started successfully");
                        LogToDiagnostics("Camera started successfully - preview available");
                        
                        // Enable camera controls
                        if (StopCameraButton != null) StopCameraButton.IsEnabled = true;
                        if (TestCameraButton != null) TestCameraButton.IsEnabled = false;
                        
                        // Subscribe to preview frames
                        cameraService.PreviewFrameReady += CameraService_PreviewFrameReady;
                        
                        // Apply saved camera settings to camera
                        cameraService.SetBrightness(_savedBrightness);
                        cameraService.SetZoom(_savedZoom);
                        cameraService.SetContrast(_savedContrast);
                        LogToDiagnostics($"Applied saved settings: Brightness={_savedBrightness}%, Zoom={_savedZoom}%, Contrast={_savedContrast}%");
                        
                        // Start preview updates
                        _ = StartCameraPreviewUpdates(cameraService);
                    }
                    else
                    {
                        UpdateHardwareStatus("camera", false, "Failed to start camera");
                        LogToDiagnostics("ERROR: Failed to start camera");
                    }
                }
                else
                {
                    UpdateHardwareStatus("camera", false, "No cameras found");
                    LogToDiagnostics("ERROR: No cameras detected");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Camera test failed", ex);
                UpdateHardwareStatus("camera", false, $"Test failed: {ex.Message}");
                LogToDiagnostics($"ERROR: Camera test failed - {ex.Message}");
            }
        }

        private CameraService? _diagnosticCameraService;
        
        // Persistent camera settings
        private int _savedBrightness = 50;
        private int _savedZoom = 100;
        private int _savedContrast = 50;

        private Task StartCameraPreviewUpdates(CameraService cameraService)
        {
            _diagnosticCameraService = cameraService;
            
            // Optimized preview update loop for smooth performance
            _ = Task.Run(async () =>
            {
                while (_diagnosticCameraService != null && _diagnosticCameraService.IsCapturing)
                {
                    try
                    {
                        if (_diagnosticCameraService.IsNewFrameAvailable())
                        {
                            var previewBitmap = _diagnosticCameraService.GetPreviewBitmap();
                            if (previewBitmap != null)
                            {
                                // Use BeginInvoke for better performance (non-blocking)
                                _ = Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (CameraPreviewImage != null)
                                    {
                                        CameraPreviewImage.Source = previewBitmap;
                                        if (CameraPreviewPlaceholder != null)
                                            CameraPreviewPlaceholder.Visibility = Visibility.Collapsed;
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Render);
                            }
                        }
                        await Task.Delay(16); // ~60 FPS for smooth preview matching frontend
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Application.Error("Camera preview update failed", ex);
                        break;
                    }
                }
            });
            
            return Task.CompletedTask;
        }

        private void CameraService_PreviewFrameReady(object? sender, System.Windows.Media.Imaging.WriteableBitmap e)
        {
            // Preview frames are handled in the update loop
        }

        private void StopCameraButton_Click(object sender, RoutedEventArgs e)
        {
            StopDiagnosticCamera();
        }

        /// <summary>
        /// Stop the diagnostic camera and clean up resources
        /// </summary>
        private void StopDiagnosticCamera()
        {
            try
            {
                _diagnosticCameraService?.StopCamera();
                _diagnosticCameraService?.Dispose();
                _diagnosticCameraService = null;
                
                // Reset UI
                if (CameraPreviewImage != null) CameraPreviewImage.Source = null;
                if (CameraPreviewPlaceholder != null) CameraPreviewPlaceholder.Visibility = Visibility.Visible;
                if (StopCameraButton != null) StopCameraButton.IsEnabled = false;
                if (TestCameraButton != null) TestCameraButton.IsEnabled = true;
                
                UpdateHardwareStatus("camera", false, "Camera stopped", "Stopped");
                LogToDiagnostics("Camera stopped and resources cleaned up");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to stop camera", ex);
                LogToDiagnostics($"ERROR: Failed to stop camera - {ex.Message}");
            }
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Console.WriteLine($"AdminDashboard: Brightness slider changed to {(int)e.NewValue}");
            
            if (BrightnessValue != null)
                BrightnessValue.Text = ((int)e.NewValue).ToString();
            
            // Save setting persistently
            _savedBrightness = (int)e.NewValue;
            Console.WriteLine($"AdminDashboard: Saved brightness = {_savedBrightness}");
            
            // Apply brightness setting to camera if active
            if (_diagnosticCameraService != null && _diagnosticCameraService.IsCapturing)
            {
                var success = _diagnosticCameraService.SetBrightness(_savedBrightness);
                Console.WriteLine($"AdminDashboard: Applied brightness to active camera - {(success ? "Success" : "Failed")}");
                LogToDiagnostics($"Brightness adjusted to {_savedBrightness}% - {(success ? "Applied" : "Failed")}");
            }
            else
            {
                Console.WriteLine("AdminDashboard: Camera not active, brightness will apply when camera starts");
                LogToDiagnostics($"Brightness set to {_savedBrightness}% (saved, will apply when camera starts)");
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZoomValue != null)
                ZoomValue.Text = $"{(int)e.NewValue}%";
            
            // Save setting persistently
            _savedZoom = (int)e.NewValue;
            
            // Apply zoom setting to camera if active
            if (_diagnosticCameraService != null && _diagnosticCameraService.IsCapturing)
            {
                var success = _diagnosticCameraService.SetZoom(_savedZoom);
                LogToDiagnostics($"Zoom adjusted to {_savedZoom}% - {(success ? "Applied" : "Failed")}");
            }
            else
            {
                LogToDiagnostics($"Zoom set to {_savedZoom}% (saved, will apply when camera starts)");
            }
        }

        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ContrastValue != null)
                ContrastValue.Text = ((int)e.NewValue).ToString();
            
            // Save setting persistently
            _savedContrast = (int)e.NewValue;
            
            // Apply contrast setting to camera if active
            if (_diagnosticCameraService != null && _diagnosticCameraService.IsCapturing)
            {
                var success = _diagnosticCameraService.SetContrast(_savedContrast);
                LogToDiagnostics($"Contrast adjusted to {_savedContrast}% - {(success ? "Applied" : "Failed")}");
            }
            else
            {
                LogToDiagnostics($"Contrast set to {_savedContrast}% (saved, will apply when camera starts)");
            }
        }

        private async void SaveCameraSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"AdminDashboard: Saving camera settings - Brightness:{_savedBrightness}, Zoom:{_savedZoom}, Contrast:{_savedContrast}");
                
                var result = await _databaseService.SaveAllCameraSettingsAsync(_savedBrightness, _savedZoom, _savedContrast);
                
                if (result.Success)
                {
                    LogToDiagnostics($"✅ Camera settings saved to database: Brightness={_savedBrightness}%, Zoom={_savedZoom}%, Contrast={_savedContrast}%");
                    
                    // Show success notification
                    NotificationService.Quick.Success("Camera settings saved successfully!");
                }
                else
                {
                    LogToDiagnostics($"❌ Failed to save camera settings: {result.ErrorMessage}");
                    NotificationService.Quick.Error("Failed to save camera settings");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AdminDashboard: Error saving camera settings - {ex.Message}");
                LoggingService.Application.Error("Failed to save camera settings", ex);
                LogToDiagnostics($"❌ ERROR: Failed to save camera settings - {ex.Message}");
                NotificationService.Quick.Error("Failed to save camera settings");
            }
        }

        private void ResetCameraSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("AdminDashboard: Resetting camera settings to defaults");
                
                // Reset saved settings
                _savedBrightness = 50;
                _savedZoom = 100;
                _savedContrast = 50;
                
                // Reset camera service settings
                if (_diagnosticCameraService != null)
                {
                    _diagnosticCameraService.ResetCameraSettings();
                }
                
                // Reset UI sliders (this will trigger ValueChanged events and update saved settings)
                if (BrightnessSlider != null) BrightnessSlider.Value = 50;
                if (ZoomSlider != null) ZoomSlider.Value = 100;
                if (ContrastSlider != null) ContrastSlider.Value = 50;
                
                LogToDiagnostics("Camera settings reset to defaults (use Save Settings to persist)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AdminDashboard: Error resetting camera settings - {ex.Message}");
                LoggingService.Application.Error("Failed to reset camera settings", ex);
                LogToDiagnostics($"ERROR: Failed to reset camera settings - {ex.Message}");
            }
        }

        // Printer Testing Event Handlers
        private async void TestPrinterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Testing printer connection...");
                
                // TODO: Implement actual printer service when available
                // For now, simulate printer detection
                await Task.Delay(1000);
                
                // Simulate printer detection logic with robust error handling
                bool printerFound = false;
                try
                {
                    printerFound = System.IO.Ports.SerialPort.GetPortNames().Length > 0;
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Warning("Failed to enumerate serial ports for printer detection", ("Error", ex.Message));
                    printerFound = false; // Assume no printer if serial port access fails
                }
                
                if (printerFound)
                {
                    UpdateHardwareStatus("printer", true, "DNP RX1hs detected and ready");
                    LogToDiagnostics("Printer connection successful");
                    
                    // Update printer details
                    if (PrinterStatusDetailsText != null) PrinterStatusDetailsText.Text = "Ready";
                    if (PrinterPaperLevelText != null) PrinterPaperLevelText.Text = "Good (estimated)";
                    if (PrinterLastErrorText != null) PrinterLastErrorText.Text = "None";
                }
                else
                {
                    UpdateHardwareStatus("printer", false, "No printer detected");
                    LogToDiagnostics("ERROR: No printer found");
                    
                    if (PrinterStatusDetailsText != null) PrinterStatusDetailsText.Text = "Not Found";
                    if (PrinterPaperLevelText != null) PrinterPaperLevelText.Text = "Unknown";
                    if (PrinterLastErrorText != null) PrinterLastErrorText.Text = "Printer not detected";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Printer test failed", ex);
                UpdateHardwareStatus("printer", false, $"Test failed: {ex.Message}");
                LogToDiagnostics($"ERROR: Printer test failed - {ex.Message}");
            }
        }

        private async void PrintLastPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Attempting to print last photo...");
                
                // TODO: Implement actual print last photo when printer service is available
                await Task.Delay(2000); // Simulate printing
                
                LogToDiagnostics("Print last photo completed (simulated)");
                NotificationService.Instance.ShowSuccess("Print Test", "Last photo print completed successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Print last photo failed", ex);
                LogToDiagnostics($"ERROR: Print last photo failed - {ex.Message}");
                NotificationService.Instance.ShowError("Print Test", "Failed to print last photo");
            }
        }

        private async void PrintTestPageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Printing test page...");
                
                // TODO: Implement actual test page printing when printer service is available
                await Task.Delay(3000); // Simulate printing
                
                LogToDiagnostics("Test page printed successfully (simulated)");
                NotificationService.Instance.ShowSuccess("Print Test", "Test page printed successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Print test page failed", ex);
                LogToDiagnostics($"ERROR: Print test page failed - {ex.Message}");
                NotificationService.Instance.ShowError("Print Test", "Failed to print test page");
            }
        }

        private async void CheckPrinterStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Checking printer status...");
                
                // TODO: Implement actual printer status check when printer service is available
                await Task.Delay(500);
                
                // Simulate status update
                if (PrinterStatusDetailsText != null) PrinterStatusDetailsText.Text = "Online";
                if (PrinterPaperLevelText != null) PrinterPaperLevelText.Text = "85% (595 prints remaining)";
                if (PrinterLastErrorText != null) PrinterLastErrorText.Text = "None";
                
                LogToDiagnostics("Printer status updated");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Check printer status failed", ex);
                LogToDiagnostics($"ERROR: Check printer status failed - {ex.Message}");
            }
        }

        // Arduino Testing Event Handlers
        private async void TestArduinoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Testing Arduino connection...");
                
                // TODO: Implement actual Arduino service when available
                // For now, simulate Arduino detection
                await Task.Delay(1000);
                
                string[] availablePorts = Array.Empty<string>();
                try
                {
                    availablePorts = System.IO.Ports.SerialPort.GetPortNames();
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Warning("Failed to enumerate serial ports for Arduino detection", ("Error", ex.Message));
                    LogToDiagnostics($"Arduino test failed: Unable to access serial ports - {ex.Message}");
                    return;
                }
                
                if (availablePorts.Length > 0)
                {
                    var arduinoPort = availablePorts.FirstOrDefault(p => p.StartsWith("COM"));
                    if (arduinoPort != null)
                    {
                        UpdateHardwareStatus("arduino", true, "Arduino Uno detected");
                        LogToDiagnostics($"Arduino found on {arduinoPort}");
                        
                        // Update Arduino details
                        if (ArduinoPortText != null) ArduinoPortText.Text = arduinoPort;
                        if (ArduinoLedStatusText != null) ArduinoLedStatusText.Text = "Ready";
                        if (ArduinoLastPulseText != null) ArduinoLastPulseText.Text = "Ready to receive";
                    }
                    else
                    {
                        UpdateHardwareStatus("arduino", false, "No Arduino ports found");
                        LogToDiagnostics("ERROR: No Arduino-compatible ports found");
                    }
                }
                else
                {
                    UpdateHardwareStatus("arduino", false, "No serial ports available");
                    LogToDiagnostics("ERROR: No serial ports detected");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Arduino test failed", ex);
                UpdateHardwareStatus("arduino", false, $"Test failed: {ex.Message}");
                LogToDiagnostics($"ERROR: Arduino test failed - {ex.Message}");
            }
        }

        private async void TestLEDOnButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Turning LED ON...");
                
                // TODO: Implement actual Arduino LED control when service is available
                await Task.Delay(500);
                
                if (ArduinoLedStatusText != null) ArduinoLedStatusText.Text = "ON";
                LogToDiagnostics("LED turned ON (simulated)");
                NotificationService.Instance.ShowSuccess("Arduino Test", "LED turned ON");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("LED ON test failed", ex);
                LogToDiagnostics($"ERROR: LED ON test failed - {ex.Message}");
            }
        }

        private async void TestLEDOffButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Turning LED OFF...");
                
                // TODO: Implement actual Arduino LED control when service is available
                await Task.Delay(500);
                
                if (ArduinoLedStatusText != null) ArduinoLedStatusText.Text = "OFF";
                LogToDiagnostics("LED turned OFF (simulated)");
                NotificationService.Instance.ShowSuccess("Arduino Test", "LED turned OFF");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("LED OFF test failed", ex);
                LogToDiagnostics($"ERROR: LED OFF test failed - {ex.Message}");
            }
        }

        private async void TestPaymentPulseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Simulating payment pulse...");
                
                // TODO: Implement actual pulse simulation when Arduino service is available
                await Task.Delay(500);
                
                if (ArduinoLastPulseText != null) 
                    ArduinoLastPulseText.Text = DateTime.Now.ToString("HH:mm:ss");
                
                LogToDiagnostics("Payment pulse simulated successfully");
                NotificationService.Instance.ShowSuccess("Arduino Test", "Payment pulse simulated");
                
                // Simulate adding $1 credit
                await AddCreditsAsync(1.00m, "Test pulse simulation");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Payment pulse test failed", ex);
                LogToDiagnostics($"ERROR: Payment pulse test failed - {ex.Message}");
            }
        }

        // System Tools Event Handlers
        private void CalibrateTouchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Starting touch screen calibration...");
                
                var result = ConfirmationDialog.ShowConfirmation(
                    "Touch Screen Calibration",
                    "This will start the Windows touch screen calibration utility.\n\nProceed with calibration?",
                    "Start Calibration",
                    "Cancel");
                
                if (result)
                {
                    // Launch Windows touch calibration utility
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "tabcal.exe",
                        UseShellExecute = true,
                        Verb = "runas" // Run as administrator
                    };
                    
                    System.Diagnostics.Process.Start(startInfo);
                    LogToDiagnostics("Touch calibration utility launched");
                    NotificationService.Instance.ShowSuccess("System Tools", "Touch calibration utility started");
                }
                else
                {
                    LogToDiagnostics("Touch calibration cancelled by user");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Touch calibration failed", ex);
                LogToDiagnostics($"ERROR: Touch calibration failed - {ex.Message}");
                NotificationService.Instance.ShowError("System Tools", "Failed to start touch calibration");
            }
        }

        private async void IssueFreeCreditsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Issuing free credit (+$1.00)...");
                
                await AddCreditsAsync(1.00m, "Free Credit (Diagnostics)");
                await LoadFreeCreditUsageAsync(); // Refresh the counter
                
                LogToDiagnostics("Free credit issued successfully");
                NotificationService.Instance.ShowSuccess("System Tools", "Free credit (+$1.00) added successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Issue free credit failed", ex);
                LogToDiagnostics($"ERROR: Issue free credit failed - {ex.Message}");
                NotificationService.Instance.ShowError("System Tools", "Failed to issue free credit");
            }
        }

        private async void RunDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToDiagnostics("Running comprehensive diagnostics...");
                
                // Run all hardware tests sequentially
                await Task.Delay(500);
                TestCameraButton_Click(sender, e);
                
                await Task.Delay(2000);
                TestPrinterButton_Click(sender, e);
                
                await Task.Delay(2000);
                TestArduinoButton_Click(sender, e);
                
                await Task.Delay(1000);
                LogToDiagnostics("Comprehensive diagnostics completed");
                NotificationService.Instance.ShowSuccess("Diagnostics", "All hardware tests completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Run all diagnostics failed", ex);
                LogToDiagnostics($"ERROR: Comprehensive diagnostics failed - {ex.Message}");
            }
        }

        private async void RestartSystemButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = ConfirmationDialog.ShowConfirmation(
                    "Restart System",
                    "This will restart the computer immediately.\n\nAll unsaved work will be lost.\n\nAre you sure you want to restart now?",
                    "Restart Now",
                    "Cancel");
                
                if (result)
                {
                    LogToDiagnostics("System restart initiated by user");
                    LoggingService.Application.Warning("System restart initiated from diagnostics panel");
                    
                    // Give time for log to be written
                    await Task.Delay(1000);
                    
                    // Restart the computer with proper elevation
                    var startInfo = new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/r /t 0")
                    {
                        UseShellExecute = true, // Required for elevation
                        Verb = "runas", // Prompt for elevation if required
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden // CreateNoWindow equivalent
                    };
                    
                    System.Diagnostics.Process.Start(startInfo);
                }
                else
                {
                    LogToDiagnostics("System restart cancelled by user");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("System restart failed", ex);
                LogToDiagnostics($"ERROR: System restart failed - {ex.Message}");
                NotificationService.Instance.ShowError("System Tools", "Failed to restart system");
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DiagnosticLogText != null)
                {
                    DiagnosticLogText.Text = "Diagnostic log cleared...";
                    LogToDiagnostics("Diagnostic log cleared");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Clear diagnostic log failed", ex);
            }
        }



        #endregion

        #region System Tab Event Handlers



        /// <summary>
        /// Set system date and time
        /// </summary>
        private async void SetSystemDate_Click(object sender, RoutedEventArgs e)
        {
            SetSystemDateButton.IsEnabled = false;
            try
            {
                // Use the built-in async overlay and data-load logic
                var owner = Window.GetWindow(this) ?? Application.Current.MainWindow;
                if (owner == null)
                {
                    NotificationService.Instance.ShowError("System Configuration", "Unable to locate main window for dialog.");
                    return;
                }
                
                await SystemDateDialog.ShowSystemDateDialogAsync(owner, _databaseService);
                
                // Reflect any normalization from the database
                await LoadSystemTabSettings();
                
                LoggingService.Application.Information("System date dialog completed");
                // Success/error feedback is handled within the dialog
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("System date setting failed", ex);
                NotificationService.Instance.ShowError("System Configuration", $"Failed to set system date: {ex.Message}");
            }
            finally
            {
                SetSystemDateButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Restart the photobooth application
        /// </summary>
        private async void RestartApplication_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            button.IsEnabled = false;
            try
            {
                var result = ConfirmationDialog.ShowConfirmation(
                    "Restart Application",
                    "This will restart the PhotoBooth application.\n\nAny unsaved settings will be lost.\n\nContinue?",
                    "Restart App",
                    "Cancel");

                if (result)
                {
                    LoggingService.Application.Warning("Application restart initiated by user");
                    
                    // Save any pending settings first
                    var saved = await SaveSystemSettings();
                    if (!saved)
                    {
                        var proceed = ConfirmationDialog.ShowConfirmation(
                            "Restart Application",
                            "Some settings could not be saved. Do you still want to restart the application?\n\nUnsaved settings will be lost during restart.",
                            "Restart Anyway",
                            "Cancel");
                        
                        if (!proceed)
                        {
                            NotificationService.Instance.ShowWarning("System Configuration", "Application restart cancelled.");
                            return;
                        }
                    }
                    
                    // Give time for save to complete
                    await Task.Delay(1000);
                    
                    // Resolve executable path reliably with fallbacks
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    var exePath = Environment.ProcessPath
                                  ?? currentProcess.MainModule?.FileName
                                  ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
                                  ?? string.Empty;
                    
                    if (string.IsNullOrWhiteSpace(exePath))
                    {
                        NotificationService.Instance.ShowError(
                            "System Configuration",
                            "Unable to determine application path to restart.");
                        return;
                    }
                    
                    // Notify user immediately that restart is in progress
                    NotificationService.Instance.ShowInfo(
                        "System Configuration",
                        "Restarting application...");
                    
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    };
                    
                    System.Diagnostics.Process.Start(startInfo);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Application restart failed", ex);
                NotificationService.Instance.ShowError("System Configuration", $"Failed to restart application: {ex.Message}");
            }
            finally
            {
                // If the app isn't shutting down, restore the button
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// Restart the computer
        /// </summary>
        private async void RestartComputer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            button.IsEnabled = false;
            try
            {
                var result = ConfirmationDialog.ShowConfirmation(
                    "Restart Computer",
                    "This will restart the entire computer.\n\nAll unsaved work will be lost.\n\nAre you sure?",
                    "Restart Computer",
                    "Cancel");

                if (result)
                {
                    LoggingService.Application.Warning("Computer restart initiated by user");
                    
                    // Save any pending settings first
                    var saved = await SaveSystemSettings();
                    if (!saved)
                    {
                        var proceed = ConfirmationDialog.ShowConfirmation(
                            "Restart Computer",
                            "Some settings could not be saved. Do you still want to restart the computer?\n\nUnsaved settings will be lost during restart.",
                            "Restart Anyway",
                            "Cancel");
                        
                        if (!proceed)
                        {
                            NotificationService.Instance.ShowWarning("System Configuration", "Computer restart cancelled.");
                            return;
                        }
                    }
                    
                    // Give time for save to complete
                    await Task.Delay(1000);
                    
                    // Restart the computer with proper elevation
                    var startInfo = new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/r /t 5")
                    {
                        UseShellExecute = true, // Required for elevation
                        Verb = "runas", // Prompt for elevation if required
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden // CreateNoWindow equivalent
                    };
                    
                    NotificationService.Instance.ShowInfo("System Tools", "System is restarting now...");
                    System.Diagnostics.Process.Start(startInfo);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Computer restart failed", ex);
                NotificationService.Instance.ShowError("System Configuration", $"Failed to restart computer: {ex.Message}");
            }
            finally
            {
                // If the restart was cancelled or failed, restore the button
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// Save all system settings
        /// </summary>
        private async void SaveSystemSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saved = await SaveSystemSettings();
                // Success/failure messages are already handled within SaveSystemSettings()
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Save system settings failed", ex);
                NotificationService.Instance.ShowError("System Configuration", $"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Save system settings to database
        /// </summary>
        /// <returns>True if all settings were saved successfully, false otherwise</returns>
        private async Task<bool> SaveSystemSettings()
        {
            var errors = new List<string>();
            
            try
            {
                if (string.IsNullOrEmpty(_currentUserId))
                {
                    NotificationService.Instance.ShowWarning("System Configuration", "User session invalid, please login again.");
                    return false; // Cannot save without valid user session
                }
                
                LoggingService.Application.Information("Saving system settings");
                
                // Validate and save Payment Settings
                if (PulsesPerCreditTextBox?.Text != null && int.TryParse(PulsesPerCreditTextBox.Text.Trim(), out var ppc) && ppc > 0)
                {
                    var result = await _databaseService.SetSettingValueAsync("Payment", "PulsesPerCredit", ppc, _currentUserId);
                    if (!result.Success)
                        errors.Add($"Payment.PulsesPerCredit: {result.ErrorMessage ?? "Unknown error"}");
                }
                else
                {
                    errors.Add("Pulses per credit must be a positive whole number.");
                }
                
                if (CreditValueTextBox?.Text != null && decimal.TryParse(CreditValueTextBox.Text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal creditValue) && creditValue >= 0)
                {
                    var result = await _databaseService.SetSettingValueAsync("Payment", "CreditValue", creditValue, _currentUserId);
                    if (!result.Success)
                        errors.Add($"Payment.CreditValue: {result.ErrorMessage ?? "Unknown error"}");
                }
                else
                {
                    errors.Add("Credit value must be a non-negative decimal number using a dot as the separator.");
                }
                
                // Save Hardware Settings with error checking
                if (BillAcceptorToggle != null)
                {
                    var result = await _databaseService.SetSettingValueAsync("Payment", "BillAcceptorEnabled", BillAcceptorToggle.IsChecked == true, _currentUserId);
                    if (!result.Success)
                        errors.Add($"Payment.BillAcceptorEnabled: {result.ErrorMessage ?? "Unknown error"}");
                }
                
                if (CreditCardReaderToggle != null)
                {
                    var result = await _databaseService.SetSettingValueAsync("Payment", "CreditCardReaderEnabled", CreditCardReaderToggle.IsChecked == true, _currentUserId);
                    if (!result.Success)
                        errors.Add($"Payment.CreditCardReaderEnabled: {result.ErrorMessage ?? "Unknown error"}");
                }
                
                if (PrintsPerRollTextBox?.Text != null && int.TryParse(PrintsPerRollTextBox.Text.Trim(), out var ppr) && ppr > 0)
                {
                    var result = await _databaseService.SetSettingValueAsync("Printer", "PrintsPerRoll", ppr, _currentUserId);
                    if (!result.Success)
                        errors.Add($"Printer.PrintsPerRoll: {result.ErrorMessage ?? "Unknown error"}");
                }
                else
                {
                    errors.Add("Prints per roll must be a positive whole number.");
                }
                
                if (SystemRFIDToggle != null)
                {
                    var result = await _databaseService.SetSettingValueAsync("RFID", "Enabled", SystemRFIDToggle.IsChecked == true, _currentUserId);
                    if (!result.Success)
                        errors.Add($"RFID.Enabled: {result.ErrorMessage ?? "Unknown error"}");
                }
                
                if (SystemFlashToggle != null)
                {
                    var result = await _databaseService.SetSettingValueAsync("System", "LightsEnabled", SystemFlashToggle.IsChecked == true, _currentUserId);
                    if (!result.Success)
                        errors.Add($"System.LightsEnabled: {result.ErrorMessage ?? "Unknown error"}");
                }
                
                // Flash duration validation and save
                if (FlashDurationSlider != null)
                {
                    int flashDuration = (int)FlashDurationSlider.Value;
                    if (flashDuration >= 1 && flashDuration <= 10)
                    {
                        var result = await _databaseService.SetSettingValueAsync("System", "FlashDuration", flashDuration, _currentUserId);
                        if (!result.Success)
                            errors.Add($"System.FlashDuration: {result.ErrorMessage ?? "Unknown error"}");
                    }
                    else
                    {
                        errors.Add("Flash duration must be between 1 and 10 seconds.");
                    }
                }
                
                if (SystemSeasonalToggle != null)
                {
                    var result = await _databaseService.SetSettingValueAsync("Seasonal", "AutoTemplates", SystemSeasonalToggle.IsChecked == true, _currentUserId);
                    if (!result.Success)
                        errors.Add($"Seasonal.AutoTemplates: {result.ErrorMessage ?? "Unknown error"}");
                }
                
                // Report results to user
                if (errors.Count > 0)
                {
                    LoggingService.Application.Warning($"System settings saved with {errors.Count} errors: {string.Join(", ", errors)}");
                    NotificationService.Instance.ShowError("System Configuration", 
                        $"Some settings could not be saved:\n• " + string.Join("\n• ", errors));
                    return false; // Some settings failed to save
                }
                else
                {
                    LoggingService.Application.Information("System settings saved successfully");
                    NotificationService.Instance.ShowSuccess("System Configuration", "All settings saved successfully");
                    
                    // Reflect any normalization from the database
                    await LoadSystemTabSettings();
                    return true; // All settings saved successfully
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to save system settings", ex);
                return false; // Return false instead of throwing to prevent duplicate error notifications
            }
        }

        /// <summary>
        /// Load system settings for the System tab from database
        /// </summary>
        private async Task LoadSystemTabSettings()
        {
            try
            {
                LoggingService.Application.Information("Loading system tab settings");
                
                // Payment Settings
                var pulsesResult = await _databaseService.GetSettingValueAsync<int>("Payment", "PulsesPerCredit");
                if (pulsesResult.Success)
                {
                    PulsesPerCreditTextBox.Text = pulsesResult.Data.ToString();
                }
                
                var creditValueResult = await _databaseService.GetSettingValueAsync<decimal>("Payment", "CreditValue");
                if (creditValueResult.Success)
                {
                    CreditValueTextBox.Text = creditValueResult.Data.ToString("F2", CultureInfo.InvariantCulture);
                }
                
                // Hardware Settings
                var billAcceptorResult = await _databaseService.GetSettingValueAsync<bool>("Payment", "BillAcceptorEnabled");
                if (billAcceptorResult.Success)
                {
                    BillAcceptorToggle.IsChecked = billAcceptorResult.Data;
                }
                
                var creditCardResult = await _databaseService.GetSettingValueAsync<bool>("Payment", "CreditCardReaderEnabled");
                if (creditCardResult.Success)
                {
                    CreditCardReaderToggle.IsChecked = creditCardResult.Data;
                }
                
                var printsPerRollResult = await _databaseService.GetSettingValueAsync<int>("Printer", "PrintsPerRoll");
                if (printsPerRollResult.Success)
                {
                    PrintsPerRollTextBox.Text = printsPerRollResult.Data.ToString();
                }
                
                var rfidResult = await _databaseService.GetSettingValueAsync<bool>("RFID", "Enabled");
                if (rfidResult.Success)
                {
                    SystemRFIDToggle.IsChecked = rfidResult.Data;
                }
                
                var lightsResult = await _databaseService.GetSettingValueAsync<bool>("System", "LightsEnabled");
                if (lightsResult.Success)
                {
                    SystemFlashToggle.IsChecked = lightsResult.Data;
                }
                
                var flashDurationResult = await _databaseService.GetSettingValueAsync<int>("System", "FlashDuration");
                if (flashDurationResult.Success)
                {
                    // Ensure the value is within valid range (1-10 seconds)
                    int flashValue = Math.Max(1, Math.Min(10, flashDurationResult.Data));
                    FlashDurationSlider.Value = flashValue;
                    FlashDurationValueText.Text = flashValue.ToString();
                }
                
                var seasonalResult = await _databaseService.GetSettingValueAsync<bool>("Seasonal", "AutoTemplates");
                if (seasonalResult.Success)
                {
                    SystemSeasonalToggle.IsChecked = seasonalResult.Data;
                }
                
                LoggingService.Application.Information("System tab settings loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load system tab settings", ex);
            }
        }

        #endregion

        #region Slider Event Handlers

        /// <summary>
        /// Update flash duration display when slider value changes
        /// </summary>
        private void FlashDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FlashDurationValueText != null)
            {
                FlashDurationValueText.Text = ((int)e.NewValue).ToString();
            }
        }

        #endregion

        #region Input Validation Event Handlers

        // Regex pattern for numeric-only input (positive integers)
        private static readonly Regex _numericRegex = new Regex("^[0-9]+$");

        /// <summary>
        /// Validates numeric input for TextBoxes that should only accept integers > 0
        /// </summary>
        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_numericRegex.IsMatch(e.Text);
        }

        /// <summary>
        /// Validates decimal input for TextBoxes that should only accept decimals > 0
        /// </summary>
        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow digits and one decimal point (invariant culture uses '.' as decimal separator)
            if (e.Text.Any(ch => !char.IsDigit(ch) && ch != '.'))
            {
                e.Handled = true;
                return;
            }

            if (sender is TextBox textBox)
            {
                // Build the resulting text considering current selection
                var proposedText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                                               .Insert(textBox.SelectionStart, e.Text);
                
                // Reject if more than one decimal point would result
                if (proposedText.Count(ch => ch == '.') > 1)
                {
                    e.Handled = true;
                    return;
                }
                
                // Ensure resulting text parses as non-negative decimal (0 allowed while typing)
                if (!decimal.TryParse(proposedText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
                {
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Handles paste operations for numeric-only TextBoxes
        /// </summary>
        private void NumericOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string? text = e.DataObject.GetData(typeof(string)) as string;
                if (!_numericRegex.IsMatch(text ?? string.Empty))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        /// <summary>
        /// Handles paste operations for decimal-only TextBoxes
        /// </summary>
        private void DecimalOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var pastedText = ((string)e.DataObject.GetData(typeof(string))).Trim();
                if (!decimal.TryParse(pastedText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out decimal value) || value < 0)
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
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
