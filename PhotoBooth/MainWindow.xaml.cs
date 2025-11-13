using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Main window that serves as the application shell and navigation container
    /// Handles switching between different screens (Welcome, Product Selection, Template Selection, Camera, Admin, etc.)
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private Fields

        // Screen instances - created once and reused for performance
        private WelcomeScreen? welcomeScreen;
        private ProductSelectionScreen? productSelectionScreen;
        private CategorySelectionScreen? categorySelectionScreen;
        private TemplateSelectionScreen? templateSelectionScreen;

        private TemplateCustomizationScreen? templateCustomizationScreen;
        private CameraCaptureScreen? cameraCaptureScreen;
        private PhotoPreviewScreen? photoPreviewScreen;
        private UpsellScreen? upsellScreen;
        private PrintingScreen? printingScreen;
        private AdminLoginScreen? adminLoginScreen;
        private AdminDashboardScreen? adminDashboardScreen;
        private ForcedPasswordChangeScreen? forcedPasswordChangeScreen;
        private PINSetupScreen? pinSetupScreen;
        private PINRecoveryScreen? pinRecoveryScreen;
        private PasswordResetScreen? passwordResetScreen;

        // Current state tracking
        private ProductInfo? currentProduct;
        private TemplateCategory? currentCategory;
        private TemplateInfo? currentTemplate;
        
        // Temporary storage for PIN setup navigation
        private AdminAccessLevel _tempAccessLevel;
        private string? _tempUserId;
        private AdminAccessLevel currentAdminAccess = AdminAccessLevel.None;
        private string currentOperationMode = "Coin"; // Default to Coin Operated

        // Upsell timeout context - stored to handle timeouts gracefully
        private Template? _currentUpsellTemplate;
        private ProductInfo? _currentUpsellOriginalProduct;
        private string? _currentUpsellComposedImagePath;
        private List<string>? _currentUpsellCapturedPhotosPaths;



        // Database service
        private readonly IDatabaseService _databaseService;
        
        // Template conversion service
        private readonly ITemplateConversionService _templateConversionService;
        
        // Image composition service
        private readonly IImageCompositionService _imageCompositionService;
        
        // Pricing service
        private readonly IPricingService _pricingService;
        
        // Printer service - shared instance for caching printer status across the application
        // This ensures all screens (admin and user-facing) use the same cached status
        private readonly IPrinterService _printerService;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the main window and shows the welcome screen
        /// </summary>
        public MainWindow()
        {
            // Initialize logging system first
            try
            {
                LoggingService.Initialize();
                LoggingService.Application.Information("PhotoBoothX starting up",
                    ("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"),
                    ("Platform", Environment.OSVersion.ToString()),
                    ("MachineName", Environment.MachineName));
            }
            catch (Exception ex)
            {
                // Fallback to debug output if logging fails to initialize
                System.Diagnostics.Debug.WriteLine($"Failed to initialize logging: {ex.Message}");
            }
            
            // Initialize database service
            _databaseService = new DatabaseService();
            
            // Initialize template conversion service
            _templateConversionService = new TemplateConversionService();
            
            // Initialize image composition service
            _imageCompositionService = new ImageCompositionService(_databaseService);
            
            // Initialize pricing service
            _pricingService = new PricingService(_databaseService);
            
            // Initialize printer service - shared instance for status caching
            // This allows all screens to use cached printer status for better performance
            _printerService = new PrinterService();

            InitializeComponent();
            
            // Initialize notification service with the notification container
            NotificationService.Instance.Initialize(NotificationContainer);
            
            // Initialize modal service with the modal overlay containers
            ModalService.Instance.Initialize(ModalOverlayContainer, ModalContentContainer, ModalBackdrop);

            // Database initialization moved to MainWindow_Loaded event for proper async handling
        }

        /// <summary>
        /// Window loaded event handler - performs async initialization after window is fully loaded
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("MainWindow loaded, starting application initialization");
                
                // Ensure database is initialized before using settings/credits
                await InitializeDatabaseAsync();
                // Initialize the application with proper async/await handling
                await InitializeApplication();
                
                LoggingService.Application.Information("Application initialization completed successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Application initialization failed", ex);
                
                // Show error to user
                NotificationService.Quick.Error("Failed to initialize application. Please restart the application.");
                
                // Optionally, you could close the window or show an error dialog
                // this.Close();
            }
        }



        /// <summary>
        /// Initialize the database asynchronously
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            try
            {
                Console.WriteLine("=== MAINWINDOW: DATABASE INITIALIZATION STARTING ===");
                LoggingService.Application.Information("Database initialization starting",
                    ("ConnectionString", "Data Source=[PATH]"));
                
                Console.WriteLine("=== MAINWINDOW: CALLING _databaseService.InitializeAsync() ===");
                var result = await _databaseService.InitializeAsync();
                Console.WriteLine($"=== MAINWINDOW: InitializeAsync() returned. Success: {result.Success} ===");
                if (!result.Success)
                {
                    Console.WriteLine($"[ERROR] Database initialization FAILED!");
                    Console.WriteLine($"[ERROR] Error message: {result.ErrorMessage ?? "Unknown error"}");
                    LoggingService.Application.Error("Database initialization failed", null,
                        ("ErrorMessage", result.ErrorMessage ?? "Unknown error"));
                }
                else
                {
                    LoggingService.Application.Information("Database initialization completed successfully");
                    
                    // SECURITY: Initialize master password config now that database is ready
                    // This ensures config file is loaded and deleted immediately
                    await InitializeMasterPasswordConfigAsync();
                    
                    // Initialize templates from file system on startup
                    // This ensures templates are available immediately without requiring admin login first
                    try
                    {
                        LoggingService.Application.Information("Starting template synchronization on startup");
                        var templateManager = new TemplateManager(_databaseService);
                        var syncResult = await templateManager.SynchronizeWithFileSystemAsync();
                        
                        if (syncResult.Success)
                        {
                            LoggingService.Application.Information("Template synchronization completed successfully",
                                ("SuccessCount", syncResult.SuccessCount),
                                ("FailureCount", syncResult.FailureCount));
                        }
                        else
                        {
                            LoggingService.Application.Warning("Template synchronization completed with issues",
                                ("Message", syncResult.Message ?? "Unknown issues"),
                                ("FailureCount", syncResult.FailureCount));
                        }
                    }
                    catch (Exception syncEx)
                    {
                        LoggingService.Application.Warning("Template synchronization failed during startup", 
                            ("Error", syncEx.Message), ("StackTrace", syncEx.StackTrace ?? ""));
                        // Don't let template sync failure block application startup
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Database initialization error", ex);
            }
        }

        /// <summary>
        /// Sets up the application and navigates to the welcome screen
        /// </summary>
        private async Task InitializeApplication()
        {
            // Load operation mode from settings
            await LoadOperationModeAsync();
            
            // Initialize admin dashboard screen for credit management (even for regular users)
            await InitializeAdminDashboardForCreditsAsync();
            
            // Initialize printer status cache on app startup
            // This ensures printer status is available immediately without lag when navigating to printing screen
            // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue
            try
            {
                _printerService.RefreshCachedStatus();
                LoggingService.Application.Information("Printer status cache initialized on startup");
            }
            catch (Exception ex)
            {
                // Don't block app startup if printer status check fails
                LoggingService.Application.Warning("Failed to initialize printer status cache on startup",
                    ("Exception", ex.Message));
            }
            
            // Start with the welcome screen
            NavigateToWelcome();
        }

        /// <summary>
        /// Load the current operation mode from database settings
        /// </summary>
        private async Task LoadOperationModeAsync()
        {
            try
            {
                var result = await _databaseService.GetSettingValueAsync<string>("System", "Mode");
                
                if (result.Success && !string.IsNullOrEmpty(result.Data))
                {
                    currentOperationMode = result.Data;
                    LoggingService.Application.Information("Operation mode loaded", ("Mode", currentOperationMode));
                }
                else
                {
                    LoggingService.Application.Warning("Failed to load operation mode, using default", ("DefaultMode", currentOperationMode));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error loading operation mode", ex);
            }
        }

        /// <summary>
        /// Check if the application is in free play mode
        /// </summary>
        public bool IsFreePlayMode 
        { 
            get 
            {
                var isFree = currentOperationMode.Equals("Free", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"=== IS_FREE_PLAY_MODE CHECK === Current Mode: '{currentOperationMode}', IsFree: {isFree}");
                return isFree;
            }
        }

        /// <summary>
        /// Get the current operation mode
        /// </summary>
        public string CurrentOperationMode => currentOperationMode;

        /// <summary>
        /// Refresh the operation mode from database settings
        /// </summary>
        public async Task RefreshOperationModeAsync()
        {
            await LoadOperationModeAsync();
            
            // Refresh all credit displays to update the text based on the new operation mode
            RefreshAllCreditDisplays();
            
            LoggingService.Application.Information("Operation mode refreshed and credit displays updated",
                ("NewMode", currentOperationMode),
                ("IsFreePlayMode", IsFreePlayMode));
        }
        
        /// <summary>
        /// Initialize admin dashboard for credit management (used by all users)
        /// </summary>
        private async Task InitializeAdminDashboardForCreditsAsync()
        {
            try
            {
                LoggingService.Application.Debug("Initializing admin dashboard for credit management");
                if (adminDashboardScreen == null)
                {
                    // Pass shared printer service to admin dashboard for cached status access
                    adminDashboardScreen = new AdminDashboardScreen(_databaseService, this, _printerService);
                    LoggingService.Application.Debug("Admin Dashboard created for credit management");
                    
                    // Defensive check after construction
                    if (adminDashboardScreen == null)
                    {
                        LoggingService.Application.Error("AdminDashboardScreen constructor returned null");
                        throw new InvalidOperationException("Failed to create AdminDashboardScreen instance");
                    }
                    
                    // Subscribe to admin dashboard events
                    adminDashboardScreen.ExitAdminRequested += AdminDashboardScreen_ExitAdminRequested;
                    LoggingService.Application.Debug("Admin Dashboard event handlers attached");
                    
                    // CRITICAL: Initialize admin dashboard with minimal access to load credits
                    LoggingService.Application.Debug("Initializing admin dashboard to load credits...");
                    await adminDashboardScreen.SetAccessLevel(AdminAccessLevel.None, "SYSTEM_CREDIT_MANAGER");
                    
                    // Add null check to prevent potential null reference
                    if (adminDashboardScreen != null)
                    {
                        var currentCredits = adminDashboardScreen.GetCurrentCredits();
                        LoggingService.Application.Information("Credits loaded from admin dashboard", ("Credits", currentCredits));
                    }
                }
                else
                {
                    LoggingService.Application.Debug("Admin Dashboard already exists");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to initialize admin dashboard for credit management", ex);
            }
        }

        #endregion

        #region Navigation Methods

        /// <summary>
        /// Navigates to the welcome screen
        /// This is the entry point of the application
        /// </summary>
        public void NavigateToWelcome()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== NavigateToWelcome CALLED ===");
                LoggingService.Application.Information("Navigating to welcome screen");
                
                // Reset virtual keyboard state when navigating away from admin screens
                System.Diagnostics.Debug.WriteLine("Calling VirtualKeyboardService.Instance.ResetState()...");
                VirtualKeyboardService.Instance.ResetState();
                System.Diagnostics.Debug.WriteLine("VirtualKeyboardService.Instance.ResetState() completed");

                // Clear all notifications when returning to welcome screen
                NotificationService.Instance.ClearAll();

                // Clear any admin state
                currentAdminAccess = AdminAccessLevel.None;

                if (welcomeScreen == null)
                {
                    System.Diagnostics.Debug.WriteLine("Creating new WelcomeScreen instance");
                    welcomeScreen = new WelcomeScreen();
                    // Subscribe to the welcome screen's navigation event
                    welcomeScreen.StartButtonClicked += WelcomeScreen_StartButtonClicked;
                    welcomeScreen.AdminAccessRequested += WelcomeScreen_AdminAccessRequested;
                    LoggingService.Application.Debug("Welcome screen initialized and event handlers attached");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WelcomeScreen already exists, reusing");
                }

                System.Diagnostics.Debug.WriteLine("Setting CurrentScreenContainer.Content to welcomeScreen");
                CurrentScreenContainer.Content = welcomeScreen;

                // Reset state when returning to welcome
                currentProduct = null;
                currentCategory = null;
                currentTemplate = null;
                ClearUpsellContext();
                
                LoggingService.Application.Information("Welcome screen loaded successfully");
                System.Diagnostics.Debug.WriteLine("=== NavigateToWelcome COMPLETED ===");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to welcome screen failed", ex);
                System.Diagnostics.Debug.WriteLine($"NavigateToWelcome failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Navigates to the product selection screen
        /// Called when user clicks "Touch to Start" on welcome screen
        /// </summary>
        public async Task NavigateToProductSelection()
        {
            try
            {
                LoggingService.Application.Information("Navigating to product selection");
                
                if (productSelectionScreen == null)
                {
                    productSelectionScreen = new ProductSelectionScreen(_databaseService, this);
                    // Subscribe to product selection events
                    productSelectionScreen.BackButtonClicked += ProductSelectionScreen_BackButtonClicked;
                    productSelectionScreen.ProductSelected += ProductSelectionScreen_ProductSelected;
                    LoggingService.Application.Debug("ProductSelectionScreen created");
                }

                // Refresh product prices from database to ensure we have the latest values
                LoggingService.Application.Debug("Refreshing product prices from database");
                await productSelectionScreen.RefreshProductPricesAsync();
                LoggingService.Application.Debug("Product prices refreshed successfully");

                // Update credit display if admin dashboard is available
                if (adminDashboardScreen != null)
                {
                    // Force refresh credits from database to ensure we have the latest value
                    LoggingService.Application.Debug("Refreshing credits from database");
                    await adminDashboardScreen.RefreshCreditsFromDatabase();
                    
                    var currentCredits = adminDashboardScreen.GetCurrentCredits();
                    LoggingService.Application.Debug("Credits refreshed", ("CurrentCredits", currentCredits));
                    productSelectionScreen.UpdateCredits(currentCredits);
                    LoggingService.Application.Debug("Credits updated on ProductSelectionScreen");
                }
                else
                {
                    LoggingService.Application.Debug("Admin dashboard not available, skipping credit refresh");
                }

                CurrentScreenContainer.Content = productSelectionScreen;
                LoggingService.Application.Information("Product selection navigation completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to product selection failed", ex);
            }
        }

        /// <summary>
        /// Navigates to the category selection screen
        /// Called when user selects a product type
        /// </summary>
        public async Task NavigateToCategorySelection(ProductInfo product)
        {
            try
            {
                LoggingService.Application.Information("Navigating to category selection",
                    ("ProductType", product.Type),
                    ("ProductName", product.Name));

                if (categorySelectionScreen == null)
                {
                    categorySelectionScreen = new CategorySelectionScreen(_databaseService, this);
                    // Subscribe to category selection events
                    categorySelectionScreen.BackButtonClicked += CategorySelectionScreen_BackButtonClicked;
                    categorySelectionScreen.CategorySelected += CategorySelectionScreen_CategorySelected;
                }

                // Set the product type for category filtering
                await categorySelectionScreen.SetProductTypeAsync(product);
                currentProduct = product;

                CurrentScreenContainer.Content = categorySelectionScreen;
                
                LoggingService.Application.Information("Category selection screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to category selection failed", ex);
                System.Diagnostics.Debug.WriteLine($"Navigation to category selection failed: {ex.Message}");
                // Fallback to product selection if category navigation fails
                await NavigateToProductSelection();
            }
        }

        /// <summary>
        /// Navigates to the template selection screen with a pre-selected category
        /// Called when user selects a category
        /// </summary>
        public async Task NavigateToTemplateSelection(ProductInfo product, TemplateCategory category)
        {
            try
            {
                LoggingService.Application.Information("Navigating to template selection with category",
                    ("ProductType", product.Type),
                    ("CategoryId", category.Id),
                    ("CategoryName", category.Name));

                if (templateSelectionScreen == null)
                {
                    templateSelectionScreen = new TemplateSelectionScreen(_databaseService, _templateConversionService, this);
                    // Subscribe to template selection events
                    templateSelectionScreen.BackButtonClicked += TemplateSelectionScreen_BackButtonClicked;
                    templateSelectionScreen.TemplateSelected += TemplateSelectionScreen_TemplateSelected;
                    
                    // Set admin dashboard reference for credit checking (if available)
                    if (adminDashboardScreen != null)
                    {
                        templateSelectionScreen.SetAdminDashboard(adminDashboardScreen);
                    }
                }

                // Set the product type for template filtering
                await templateSelectionScreen.SetProductTypeAsync(product);
                currentProduct = product;
                currentCategory = category;

                // Refresh credits when navigating to template selection
                if (adminDashboardScreen != null)
                {
                    var currentCredits = adminDashboardScreen.GetCurrentCredits();
                    templateSelectionScreen.UpdateCredits(currentCredits);
                }

                CurrentScreenContainer.Content = templateSelectionScreen;
                
                LoggingService.Application.Information("Template selection screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to template selection failed", ex);
                System.Diagnostics.Debug.WriteLine($"Navigation to template selection failed: {ex.Message}");
                // Fallback to category selection if template navigation fails
                await NavigateToCategorySelection(product);
            }
        }

        /// <summary>
        /// Navigate to template selection screen with categorized view
        /// </summary>
        public async Task NavigateToTemplateSelectionWithCategories(ProductInfo product)
        {
            try
            {
                currentProduct = product;

                LoggingService.Application.Information("Navigating to template selection with categorized view",
                    ("ProductType", product.Type));

                if (templateSelectionScreen == null)
                {
                    LoggingService.Application.Debug("Creating new TemplateSelectionScreen");
                    templateSelectionScreen = new TemplateSelectionScreen(_databaseService, _templateConversionService, this);
                    
                    // Subscribe to events
                    LoggingService.Application.Debug("Subscribing to TemplateSelectionScreen events");
                    templateSelectionScreen.BackButtonClicked += TemplateSelectionScreen_BackButtonClicked;
                    templateSelectionScreen.TemplateSelected += TemplateSelectionScreen_TemplateSelected;

                    // Set admin dashboard reference for credit checking (if available)
                    if (adminDashboardScreen != null)
                    {
                        templateSelectionScreen.SetAdminDashboard(adminDashboardScreen);
                    }

                    LoggingService.Application.Debug("TemplateSelectionScreen events subscribed successfully");
                }
                else
                {
                    LoggingService.Application.Debug("Using existing TemplateSelectionScreen");
                }

                // Set the product type for template filtering
                await templateSelectionScreen.SetProductTypeAsync(product);

                // Refresh product prices from database to ensure we have the latest values
                await templateSelectionScreen.RefreshProductPricesAsync();

                // Refresh credits when navigating back to template selection
                if (adminDashboardScreen != null)
                {
                    var currentCredits = adminDashboardScreen.GetCurrentCredits();
                    templateSelectionScreen.UpdateCredits(currentCredits);
                }

                // Update UI
                CurrentScreenContainer.Content = templateSelectionScreen;
                
                LoggingService.Application.Information("Template selection screen with categories loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template selection navigation failed", ex,
                    ("ProductType", product.Type ?? "Unknown"));
                System.Diagnostics.Debug.WriteLine($"Template selection navigation failed: {ex.Message}");
                // Fallback to product selection if template navigation fails
                await NavigateToProductSelection();
            }
        }



        /// <summary>
        /// Navigate to template customization screen with specific template
        /// </summary>
        public async Task NavigateToTemplateCustomizationWithTemplate(TemplateInfo? template)
        {
            try
            {
                if (template == null || currentProduct == null)
                {
                    LoggingService.Application.Warning("Attempted to navigate to template customization with null template or product");
                    await NavigateToProductSelection();
                    return;
                }
                
                currentTemplate = template;

                LoggingService.Application.Information("Navigating to template customization with specific template",
                    ("TemplateName", template.TemplateName ?? "Unknown"),
                    ("Category", template.Category ?? "Unknown"));

                if (templateCustomizationScreen == null)
                {
                                    LoggingService.Application.Debug("Creating new TemplateCustomizationScreen");
                templateCustomizationScreen = new TemplateCustomizationScreen(_databaseService, this);
                    
                    // Subscribe to events
                    templateCustomizationScreen.BackButtonClicked += TemplateCustomizationScreen_BackButtonClicked;
                    templateCustomizationScreen.TemplateSelected += TemplateCustomizationScreen_TemplateSelected;
                    templateCustomizationScreen.PhotoSessionStartRequested += TemplateCustomizationScreen_PhotoSessionStartRequested;
                    LoggingService.Application.Debug("TemplateCustomizationScreen created and events subscribed");
                }
                else
                {
                    LoggingService.Application.Debug("Using existing TemplateCustomizationScreen");
                }

                // Convert TemplateInfo to Template for customization screen
                LoggingService.Application.Debug("Converting TemplateInfo to Template");
                var dbTemplate = await ConvertTemplateInfoToTemplateAsync(template);
                LoggingService.Application.Debug("Template converted successfully",
                    ("ConvertedName", dbTemplate.Name),
                    ("ConvertedId", dbTemplate.Id));
                
                LoggingService.Application.Debug("Setting template on customization screen");
                templateCustomizationScreen.SetTemplate(dbTemplate, currentProduct);

                // Update UI
                LoggingService.Application.Debug("Updating UI container");
                CurrentScreenContainer.Content = templateCustomizationScreen;
                LoggingService.Application.Debug("UI updated successfully");
                
                LoggingService.Application.Information("Template customization screen with template loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template customization navigation failed", ex,
                    ("TemplateName", template?.TemplateName ?? "Unknown"));
                // Fallback to template selection if customization navigation fails
                if (currentProduct != null)
                {
                    await NavigateToTemplateSelectionWithCategories(currentProduct);
                }
                else
                {
                    await NavigateToProductSelection();
                }
            }
        }

        /// <summary>
        /// Navigate to template customization screen
        /// </summary>
        public async Task NavigateToTemplateCustomization(ProductInfo product, TemplateCategory category)
        {
            try
            {
                currentProduct = product;
                currentCategory = category;

                LoggingService.Application.Information("Navigating to template customization",
                    ("ProductType", product.Type),
                    ("CategoryId", category.Id),
                    ("CategoryName", category.Name));

                if (templateCustomizationScreen == null)
                {
                    templateCustomizationScreen = new TemplateCustomizationScreen(_databaseService, this);
                    
                    // Subscribe to events
                    templateCustomizationScreen.BackButtonClicked += TemplateCustomizationScreen_BackButtonClicked;
                    templateCustomizationScreen.TemplateSelected += TemplateCustomizationScreen_TemplateSelected;
                    templateCustomizationScreen.PhotoSessionStartRequested += TemplateCustomizationScreen_PhotoSessionStartRequested;
                }

                // Set the category to load the first template and show customization options
                await templateCustomizationScreen.SetCategoryAsync(category, product);

                // Update UI
                CurrentScreenContainer.Content = templateCustomizationScreen;
                
                LoggingService.Application.Information("Template customization screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template customization navigation failed", ex,
                    ("ProductType", product.Type ?? "Unknown"),
                    ("CategoryId", category.Id.ToString()));
                System.Diagnostics.Debug.WriteLine($"Template customization navigation failed: {ex.Message}");
                // Fallback to category selection if customization navigation fails
                await NavigateToCategorySelection(product);
            }
        }

        /// <summary>
        /// Navigates to camera capture screen
        /// Called when user selects a template
        /// </summary>
        public async void NavigateToPaymentOrCamera(TemplateInfo template)
        {
            try
            {
                currentTemplate = template;

                // Convert TemplateInfo to Template for camera capture
                var dbTemplate = await ConvertTemplateInfoToTemplateAsync(template);
                if (dbTemplate == null)
                {
                    LoggingService.Application.Error("Failed to convert template for camera capture", null,
                        ("TemplateName", template.TemplateName));
                    NotificationService.Instance.ShowError("Template Error", "Unable to load template for photo capture.");
                    return;
                }

                await NavigateToCameraCapture(dbTemplate);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to camera capture failed", ex);
                System.Diagnostics.Debug.WriteLine($"Navigation to payment/camera failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to camera capture screen and initializes camera session
        /// </summary>
        public async Task NavigateToCameraCapture(Template template)
        {
            try
            {
                LoggingService.Application.Information("Starting camera capture navigation",
                    ("TemplateName", template.Name),
                    ("PhotoCount", template.PhotoCount));

                // Always dispose and recreate camera screen to ensure clean state
                // This is especially important for retakes to avoid camera conflicts
                if (cameraCaptureScreen != null)
                {
                    LoggingService.Application.Debug("Disposing existing camera capture screen for retake");
                    
                    // Unsubscribe from events
                    cameraCaptureScreen.BackButtonClicked -= CameraCaptureScreen_BackButtonClicked;
                    cameraCaptureScreen.PhotosCaptured -= CameraCaptureScreen_PhotosCaptured;
                    LoggingService.Application.Debug("Camera capture event handlers unsubscribed");
                    
                    // Dispose to release camera resources
                    LoggingService.Application.Debug("Disposing camera capture screen");
                    cameraCaptureScreen.Dispose();
                    cameraCaptureScreen = null;
                    LoggingService.Application.Debug("Camera capture screen disposed and cleared");
                    
                    // Add small delay to ensure camera resources are fully released
                    // This is crucial for retakes to prevent camera access conflicts
                    LoggingService.Application.Debug("Waiting for camera resources to be released");
                    await Task.Delay(500);
                    LoggingService.Application.Debug("Camera resources released, proceeding with restart");
                }

                // Create fresh camera capture screen
                LoggingService.Application.Debug("Creating new CameraCaptureScreen");
                cameraCaptureScreen = new CameraCaptureScreen(_databaseService, this);
                LoggingService.Application.Debug("New CameraCaptureScreen created");
                
                // Subscribe to camera capture events
                LoggingService.Application.Debug("Subscribing to camera capture events");
                cameraCaptureScreen.BackButtonClicked += CameraCaptureScreen_BackButtonClicked;
                cameraCaptureScreen.PhotosCaptured += CameraCaptureScreen_PhotosCaptured;
                LoggingService.Application.Debug("Camera capture event handlers subscribed");

                // Initialize camera session
                LoggingService.Application.Debug("Starting camera initialization");
                var initialized = await cameraCaptureScreen.InitializeSessionAsync(template);
                LoggingService.Application.Debug("Camera initialization completed",
                    ("Success", initialized));
                
                if (!initialized)
                {
                    LoggingService.Application.Error("Failed to initialize camera capture session", null);
                    NotificationService.Instance.ShowError("Camera Error", "Unable to start camera. Please check your camera connection and try again.");
                    return;
                }

                LoggingService.Application.Debug("Setting camera capture screen as current content");
                CurrentScreenContainer.Content = cameraCaptureScreen;
                LoggingService.Application.Information("Camera capture navigation completed successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Camera capture navigation failed", ex,
                    ("TemplateName", template?.Name ?? "Unknown"),
                    ("ExceptionType", ex.GetType().Name));
                NotificationService.Instance.ShowError("Navigation Error", $"Failed to restart camera for retake: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigate to photo preview screen with background composition for smooth UX
        /// Composes photos while camera screen is still visible, then transitions smoothly
        /// </summary>
        public async Task NavigateToPhotoPreviewWithComposition(Template template, List<string> capturedPhotosPaths)
        {
            try
            {
                LoggingService.Application.Information("Starting background photo composition", 
                    ("TemplateName", template.Name),
                    ("PhotoCount", capturedPhotosPaths.Count));

                // Compose photos in background while camera screen shows loading
                var compositionResult = await _imageCompositionService.ComposePhotosAsync(template, capturedPhotosPaths);

                if (compositionResult.Success && compositionResult.OutputPath != null)
                {
                    LoggingService.Application.Information("Photo composition completed successfully", 
                        ("OutputPath", compositionResult.OutputPath));

                    // Hide camera loading overlay
                    if (cameraCaptureScreen != null)
                    {
                        cameraCaptureScreen.HideLoading();
                    }

                    // Now navigate to photo preview with pre-composed photos
                    await NavigateToPhotoPreviewWithComposedResult(template, capturedPhotosPaths, compositionResult);
                }
                else
                {
                    // Hide camera loading overlay
                    if (cameraCaptureScreen != null)
                    {
                        cameraCaptureScreen.HideLoading();
                    }

                    LoggingService.Application.Error("Photo composition failed during background processing", null,
                        ("ErrorMessage", compositionResult.Message ?? "Unknown error"));
                    
                    NotificationService.Instance.ShowError("Composition Error", $"Failed to compose photos: {compositionResult.Message}");
                }
            }
            catch (Exception ex)
            {
                // Hide camera loading overlay
                if (cameraCaptureScreen != null)
                {
                    cameraCaptureScreen.HideLoading();
                }

                LoggingService.Application.Error("Background photo composition failed", ex);
                NotificationService.Instance.ShowError("Preview Error", $"Failed to process photos: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigate to photo preview screen with pre-composed results
        /// Used after background composition is complete for smooth transition
        /// </summary>
        private async Task NavigateToPhotoPreviewWithComposedResult(Template template, List<string> capturedPhotosPaths, CompositionResult compositionResult)
        {
            try
            {
                LoggingService.Application.Information("Navigating to photo preview with pre-composed result", 
                    ("TemplateName", template.Name),
                    ("PhotoCount", capturedPhotosPaths.Count));

                if (photoPreviewScreen == null)
                {
                    photoPreviewScreen = new PhotoPreviewScreen(_databaseService, _imageCompositionService, this);
                    
                    // Subscribe to photo preview events
                    photoPreviewScreen.RetakePhotosRequested += PhotoPreviewScreen_RetakePhotosRequested;
                    photoPreviewScreen.PhotosApproved += PhotoPreviewScreen_PhotosApproved;
                    photoPreviewScreen.BackButtonClicked += PhotoPreviewScreen_BackButtonClicked;
                }

                CurrentScreenContainer.Content = photoPreviewScreen;

                // Initialize with pre-composed result (no loading overlay needed)
                await photoPreviewScreen.InitializeWithComposedResultAsync(template, capturedPhotosPaths, compositionResult);
                
                LoggingService.Application.Information("Photo preview screen loaded successfully with pre-composed result");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photo preview navigation with composed result failed", ex);
                NotificationService.Instance.ShowError("Preview Error", $"Failed to show photo preview: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to photo preview screen to show composed images (legacy method)
        /// </summary>
        public async Task NavigateToPhotoPreview(Template template, List<string> capturedPhotosPaths)
        {
            try
            {
                LoggingService.Application.Information("Navigating to photo preview", 
                    ("TemplateName", template.Name),
                    ("PhotoCount", capturedPhotosPaths.Count));

                if (photoPreviewScreen == null)
                {
                    photoPreviewScreen = new PhotoPreviewScreen(_databaseService, _imageCompositionService, this);
                    
                    // Subscribe to photo preview events
                    photoPreviewScreen.RetakePhotosRequested += PhotoPreviewScreen_RetakePhotosRequested;
                    photoPreviewScreen.PhotosApproved += PhotoPreviewScreen_PhotosApproved;
                    photoPreviewScreen.BackButtonClicked += PhotoPreviewScreen_BackButtonClicked;
                }

                CurrentScreenContainer.Content = photoPreviewScreen;

                // Initialize with captured photos (this will trigger composition)
                await photoPreviewScreen.InitializeWithPhotosAsync(template, capturedPhotosPaths);
                
                LoggingService.Application.Information("Photo preview screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photo preview navigation failed", ex);
                NotificationService.Instance.ShowError("Preview Error", $"Failed to show photo preview: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigate to the upselling screen after photos are approved
        /// </summary>
        public async Task NavigateToUpsell(Template template, ProductInfo originalProduct, string composedImagePath, List<string> capturedPhotosPaths)
        {
            try
            {
                LoggingService.Application.Information("Navigating to upsell screen",
                    ("TemplateName", template.Name ?? "Unknown"),
                    ("OriginalProduct", originalProduct.Type),
                    ("ComposedImagePath", composedImagePath));

                // Store context for timeout handling - ensures customer gets original order if they timeout
                _currentUpsellTemplate = template;
                _currentUpsellOriginalProduct = originalProduct;
                _currentUpsellComposedImagePath = composedImagePath;
                _currentUpsellCapturedPhotosPaths = capturedPhotosPaths;

                if (upsellScreen == null)
                {
                    upsellScreen = new UpsellScreen(_databaseService, this);
                    // Subscribe to upsell completion events
                    upsellScreen.UpsellCompleted += UpsellScreen_UpsellCompleted;
                    upsellScreen.UpsellTimeout += UpsellScreen_UpsellTimeout;
                }

                // Initialize the upsell screen with session data
                await upsellScreen.InitializeAsync(template, originalProduct, composedImagePath, capturedPhotosPaths);

                CurrentScreenContainer.Content = upsellScreen;
                
                LoggingService.Application.Information("Upsell screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upsell navigation failed", ex);
                NotificationService.Instance.ShowError("Upsell Error", $"Failed to show upselling options: {ex.Message}");
                
                // Fallback: proceed directly to printing
                await NavigateToPrinting(template, originalProduct, composedImagePath, capturedPhotosPaths, 0, null, 0, false);
            }
        }

        /// <summary>
        /// Navigate to printing after upselling is complete or skipped
        /// </summary>
        private async Task NavigateToPrinting(Template template, ProductInfo originalProduct, string composedImagePath, 
            List<string> capturedPhotosPaths, int extraCopies, ProductInfo? crossSellProduct, decimal totalAdditionalCost, bool crossSellAccepted, decimal? totalOrderCostOverride = null)
        {
            try
            {
                LoggingService.Application.Information("Starting printing process",
                    ("OriginalProduct", originalProduct.Type),
                    ("ExtraCopies", extraCopies),
                    ("CrossSellAccepted", crossSellAccepted),
                    ("CrossSellProduct", crossSellProduct?.Type ?? "None"),
                    ("TotalAdditionalCost", totalAdditionalCost));

                // TODO: Check payment/credits for additional costs
                if (totalAdditionalCost > 0)
                {
                    // Here you would integrate with payment system
                    // For now, assume payment is handled or sufficient credits exist
                    LoggingService.Application.Information("Additional payment required",
                        ("Amount", totalAdditionalCost));
                }

                // Navigate to printing screen
                await NavigateToPrintingScreen(template, originalProduct, composedImagePath, extraCopies, crossSellProduct, totalOrderCostOverride);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Printing process failed", ex);
                NotificationService.Instance.ShowError("Printing Error", "There was an issue with printing. Please contact support.");
                
                // Return to welcome after error
                await Task.Delay(2000);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Navigate to the printing screen with progress tracking
        /// </summary>
        private async Task NavigateToPrintingScreen(Template template, ProductInfo originalProduct, string composedImagePath, 
            int extraCopies, ProductInfo? crossSellProduct, decimal? totalOrderCostOverride = null)
        {
            try
            {
                if (printingScreen == null)
                {
                    // Pass admin dashboard screen for credit management and MainWindow for operation mode check
                    Console.WriteLine($"=== CREATING PRINTING SCREEN ===");
                    Console.WriteLine($"Admin Dashboard Available: {adminDashboardScreen != null}");
                    // Pass shared printer service instance to enable status caching
                    // This ensures PrintingScreen uses the same cached status initialized on app startup
                    printingScreen = new PrintingScreen(_databaseService, adminDashboardScreen, this, _printerService);
                    // Subscribe to printing completion events
                    printingScreen.PrintingCompleted += PrintingScreen_PrintingCompleted;
                    printingScreen.PrintingCancelled += PrintingScreen_PrintingCancelled;
                }

                // Show printing screen
                CurrentScreenContainer.Content = printingScreen;

                // Initialize with print job details
                await printingScreen.InitializePrintJob(template, originalProduct, composedImagePath, extraCopies, crossSellProduct, totalOrderCostOverride);

                LoggingService.Application.Information("Printing screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Printing screen navigation failed", ex);
                NotificationService.Instance.ShowError("Printing Error", $"Failed to start printing: {ex.Message}");
                
                // Fallback to welcome
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Simulate the printing process (placeholder for actual printer integration)
        /// </summary>
        private async Task SimulatePrintingProcess(Template template, ProductInfo originalProduct, string composedImagePath, 
            int extraCopies, ProductInfo? crossSellProduct)
        {
            // TODO: Replace with actual printer integration
            LoggingService.Application.Information("Simulating printing process",
                ("ComposedImagePath", composedImagePath),
                ("OriginalCopies", 1),
                ("ExtraCopies", extraCopies),
                ("CrossSellIncluded", crossSellProduct != null));

            await Task.Delay(1000); // Simulate processing time
        }

        /// <summary>
        /// Build the printing confirmation message
        /// </summary>
        private string BuildPrintingMessage(ProductInfo originalProduct, int extraCopies, ProductInfo? crossSellProduct, decimal totalAdditionalCost)
        {
            var message = $"Printing your {originalProduct.Name}";
            
            if (extraCopies > 0)
            {
                message += $"\n+ {extraCopies} extra cop{(extraCopies == 1 ? "y" : "ies")}";
            }
            
            if (crossSellProduct != null)
            {
                message += $"\n+ {crossSellProduct.Name}";
            }
            
            if (totalAdditionalCost > 0)
            {
                message += $"\n\nAdditional cost: ${totalAdditionalCost:F2}";
            }
            
            message += "\n\nPlease wait for your photos to print!";
            
            return message;
        }

        /// <summary>
        /// Navigates to admin login screen
        /// Called from welcome screen's 5-tap sequence
        /// </summary>
        public void NavigateToAdminLogin()
        {
            try
            {
                LoggingService.Application.Information("Navigating to admin login");
                
                if (adminLoginScreen == null)
                {
                    LoggingService.Application.Debug("Creating new AdminLoginScreen instance");
                    adminLoginScreen = new AdminLoginScreen();
                    // Subscribe to admin login events
                    adminLoginScreen.LoginSuccessful += AdminLoginScreen_LoginSuccessful;
                    adminLoginScreen.LoginCancelled += AdminLoginScreen_LoginCancelled;
                    adminLoginScreen.ForgotPasswordRequested += AdminLoginScreen_ForgotPasswordRequested;
                    LoggingService.Application.Debug("AdminLoginScreen created and events subscribed");
                }
                else
                {
                    LoggingService.Application.Debug("AdminLoginScreen already exists, reusing");
                }

                // Reset the login screen
                LoggingService.Application.Debug("Resetting admin login screen");
                adminLoginScreen.Reset();
                
                CurrentScreenContainer.Content = adminLoginScreen;
                LoggingService.Application.Information("Admin login navigation completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to admin login failed", ex);
            }
        }

        /// <summary>
        /// Navigates to the forced password change screen for setup credentials
        /// </summary>
        public System.Threading.Tasks.Task NavigateToForcedPasswordChange(AdminUser user, AdminAccessLevel accessLevel)
        {
            try
            {
                // Unsubscribe from existing instance to prevent memory leaks
                if (forcedPasswordChangeScreen != null)
                {
                    forcedPasswordChangeScreen.PasswordChangeCompleted -= ForcedPasswordChangeScreen_PasswordChangeCompleted;
                }

                // Create new instance each time to ensure clean state
                forcedPasswordChangeScreen = new ForcedPasswordChangeScreen(user, accessLevel, _databaseService);
                
                // Subscribe to password change events
                forcedPasswordChangeScreen.PasswordChangeCompleted += ForcedPasswordChangeScreen_PasswordChangeCompleted;

                CurrentScreenContainer.Content = forcedPasswordChangeScreen;
                
                LoggingService.Application.Information("Forced password change screen loaded",
                    ("UserId", user.UserId),
                    ("Username", user.Username),
                    ("AccessLevel", accessLevel.ToString()));
                
                return System.Threading.Tasks.Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to forced password change failed", ex,
                    ("UserId", user.UserId),
                    ("Username", user.Username));
                System.Diagnostics.Debug.WriteLine($"Navigation to forced password change failed: {ex.Message}");
                // Fallback to welcome screen
                NavigateToWelcome();
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        /// <summary>
        /// Navigates to PIN setup screen after successful password change
        /// Rationale: Separate PIN setup from password change for cleaner UX
        /// </summary>
        private void NavigateToPINSetup(AdminUser user, AdminAccessLevel accessLevel)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(">>> ENTERING NavigateToPINSetup");
                System.Diagnostics.Debug.WriteLine($">>> User: {user.Username}, AccessLevel: {accessLevel}");
                
                // Unsubscribe from existing instance to prevent memory leaks
                if (pinSetupScreen != null)
                {
                    System.Diagnostics.Debug.WriteLine(">>> Unsubscribing from old PIN setup screen");
                    pinSetupScreen.PINSetupCompleted -= PINSetupScreen_PINSetupCompleted;
                }

                System.Diagnostics.Debug.WriteLine(">>> Creating new PINSetupScreen instance");
                
                // Create new instance
                pinSetupScreen = new PINSetupScreen(_databaseService, user);
                
                System.Diagnostics.Debug.WriteLine(">>> PINSetupScreen created successfully");
                
                // Subscribe to events
                pinSetupScreen.PINSetupCompleted += PINSetupScreen_PINSetupCompleted;

                System.Diagnostics.Debug.WriteLine(">>> Events subscribed");

                // Store access level for later navigation
                _tempAccessLevel = accessLevel;
                _tempUserId = user.UserId;

                System.Diagnostics.Debug.WriteLine($">>> Setting CurrentScreenContainer.Content to pinSetupScreen");
                CurrentScreenContainer.Content = pinSetupScreen;
                System.Diagnostics.Debug.WriteLine($">>> CurrentScreenContainer.Content set successfully");
                
                LoggingService.Application.Information("PIN setup screen loaded",
                    ("UserId", user.UserId),
                    ("Username", user.Username),
                    ("AccessLevel", accessLevel.ToString()));
                    
                System.Diagnostics.Debug.WriteLine(">>> NavigateToPINSetup completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> ERROR in NavigateToPINSetup: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($">>> Stack trace: {ex.StackTrace}");
                
                LoggingService.Application.Error("Navigation to PIN setup failed", ex,
                    ("UserId", user.UserId),
                    ("Username", user.Username));
                System.Diagnostics.Debug.WriteLine($"Navigation to PIN setup failed: {ex.Message}");
                
                // Fallback - go directly to admin dashboard
                _ = NavigateToAdminDashboard(accessLevel, user.UserId);
            }
        }

        /// <summary>
        /// Navigate to PIN recovery screen (forgot password flow)
        /// Rationale: Simple PIN-based password recovery for kiosk users
        /// </summary>
        private void NavigateToPINRecovery()
        {
            try
            {
                LoggingService.Application.Information("Navigating to PIN recovery");
                
                // Unsubscribe from existing instance to prevent memory leaks
                if (pinRecoveryScreen != null)
                {
                    pinRecoveryScreen.RecoverySuccessful -= PINRecoveryScreen_RecoverySuccessful;
                    pinRecoveryScreen.BackToLogin -= PINRecoveryScreen_BackToLogin;
                }

                // Create new instance
                pinRecoveryScreen = new PINRecoveryScreen(_databaseService);
                
                // Subscribe to events
                pinRecoveryScreen.RecoverySuccessful += PINRecoveryScreen_RecoverySuccessful;
                pinRecoveryScreen.BackToLogin += PINRecoveryScreen_BackToLogin;

                CurrentScreenContainer.Content = pinRecoveryScreen;
                LoggingService.Application.Information("PIN recovery screen loaded");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to PIN recovery failed", ex);
                
                // Fallback - go back to login
                NavigateToAdminLogin();
            }
        }

        /// <summary>
        /// Navigate to password reset screen after successful PIN verification
        /// </summary>
        private void NavigateToPasswordReset(AdminUser user)
        {
            try
            {
                LoggingService.Application.Information("Navigating to password reset",
                    ("UserId", user.UserId),
                    ("Username", user.Username));
                
                // Unsubscribe from existing instance to prevent memory leaks
                if (passwordResetScreen != null)
                {
                    passwordResetScreen.PasswordResetSuccessful -= PasswordResetScreen_PasswordResetSuccessful;
                    passwordResetScreen.PasswordResetCancelled -= PasswordResetScreen_PasswordResetCancelled;
                }

                // Create new instance
                passwordResetScreen = new PasswordResetScreen(_databaseService, user);
                
                // Subscribe to events
                passwordResetScreen.PasswordResetSuccessful += PasswordResetScreen_PasswordResetSuccessful;
                passwordResetScreen.PasswordResetCancelled += PasswordResetScreen_PasswordResetCancelled;

                CurrentScreenContainer.Content = passwordResetScreen;
                LoggingService.Application.Information("Password reset screen loaded",
                    ("UserId", user.UserId),
                    ("Username", user.Username));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to password reset failed", ex,
                    ("UserId", user.UserId),
                    ("Username", user.Username));
                
                // Fallback - go back to login
                NavigateToAdminLogin();
            }
        }

        /// <summary>
        /// Navigate to admin dashboard with specified access level
        /// Called after successful admin login
        /// </summary>
        public async System.Threading.Tasks.Task NavigateToAdminDashboard(AdminAccessLevel accessLevel, string userId)
        {
            try
            {
                LoggingService.Application.Information("Navigating to admin dashboard", 
                    ("AccessLevel", accessLevel.ToString()),
                    ("UserId", userId));
                
                // AdminDashboardScreen should already exist from initialization
                if (adminDashboardScreen == null)
                {
                    LoggingService.Application.Warning("Admin Dashboard was null, creating new instance");
                    await InitializeAdminDashboardForCreditsAsync();
                }

                // Guard against null adminDashboardScreen after initialization attempt
                if (adminDashboardScreen == null)
                {
                    var errorMsg = "Failed to initialize AdminDashboard after initialization attempt";
                    LoggingService.Application.Error(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                // Set access level and configure UI accordingly
                currentAdminAccess = accessLevel;
                LoggingService.Application.Debug("Setting admin access level");
                await adminDashboardScreen.SetAccessLevel(accessLevel, userId);
                LoggingService.Application.Debug("Access level set successfully");
                
                LoggingService.Application.Debug("Refreshing sales data");
                adminDashboardScreen.RefreshSalesData();
                LoggingService.Application.Debug("Sales data refreshed");

                CurrentScreenContainer.Content = adminDashboardScreen;
                LoggingService.Application.Information("Admin dashboard navigation completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to admin dashboard failed", ex,
                    ("AccessLevel", accessLevel.ToString()),
                    ("UserId", userId));
                
                // Show error message instead of silently falling back
                try
                {
                    MessageBox.Show($"Failed to load admin dashboard: {ex.Message}\n\nPlease check the logs for details.", 
                                  "Admin Dashboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    // If MessageBox fails, at least we have logging
                }
                
                // Fallback to welcome screen
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Legacy method - redirects to new admin login
        /// </summary>
        [Obsolete("Use NavigateToAdminLogin() instead")]
        public void NavigateToAdminBackend()
        {
            NavigateToAdminLogin();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles welcome screen start button click
        /// </summary>
        private async void WelcomeScreen_StartButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked 'Touch to Start' button");
                LoggingService.Transaction.Information("USER_INTERACTION", "Customer session started",
                    ("Action", "TouchToStart"),
                    ("Timestamp", DateTime.Now));
                await NavigateToProductSelection();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Welcome start navigation failed", ex);
                System.Diagnostics.Debug.WriteLine($"Welcome start navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles admin access request from welcome screen
        /// This is triggered by the 5-tap sequence on the welcome screen
        /// </summary>
        private void WelcomeScreen_AdminAccessRequested(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Warning("Admin access sequence detected - 5-tap completed");
                LoggingService.Transaction.Information("ADMIN_ACCESS", "Admin access sequence triggered",
                    ("TriggerMethod", "5-tap sequence"),
                    ("SourceScreen", "Welcome"));
                System.Diagnostics.Debug.WriteLine("Admin access requested from welcome screen");
                NavigateToAdminLogin();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Admin access navigation failed", ex);
                System.Diagnostics.Debug.WriteLine($"Admin access navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles product selection back button click
        /// </summary>
        private void ProductSelectionScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Product selection back navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles product selection
        /// </summary>
        private async void ProductSelectionScreen_ProductSelected(object? sender, ProductSelectedEventArgs e)
        {
            try
            {
                // Skip category selection and go directly to template selection with categorized view
                await NavigateToTemplateSelectionWithCategories(e.ProductInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Product selection navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles category selection back button click
        /// </summary>
        private async void CategorySelectionScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                await NavigateToProductSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Category selection back navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles category selection
        /// </summary>
        private async void CategorySelectionScreen_CategorySelected(object? sender, CategorySelectedEventArgs e)
        {
            try
            {
                if (e.Product != null)
                {
                    await NavigateToTemplateCustomization(e.Product, e.Category);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Category selected but no product information available");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Category selection navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles template selection back button click
        /// </summary>
        private async void TemplateSelectionScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                // Navigate back to product selection since category selection is skipped
                await NavigateToProductSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Template selection back navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles template selection
        /// </summary>
        private async void TemplateSelectionScreen_TemplateSelected(object? sender, TemplateSelectedEventArgs e)
        {
            try
            {
                LoggingService.Application.Debug("Template selected event received",
                    ("Sender", sender?.GetType().Name ?? "NULL"),
                    ("TemplateName", e.Template?.TemplateName ?? "NULL"),
                    ("TemplateCategory", e.Template?.Category ?? "NULL"));
                
                // Navigate to customization screen with the selected template
                if (e.Template != null)
                {
                    // Navigate directly to template customization - credits will be deducted when printing completes
                    await NavigateToTemplateCustomizationWithTemplate(e.Template);
                }
                else
                {
                    LoggingService.Application.Warning("Template is null, cannot navigate to customization - falling back");
                    // Fallback to template selection if template is null
                    if (currentProduct != null)
                    {
                        await NavigateToTemplateSelectionWithCategories(currentProduct);
                    }
                    else
                    {
                        await NavigateToProductSelection();
                    }
                }
                
                LoggingService.Application.Debug("Template selection navigation completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template selection navigation failed", ex);
            }
        }

        /// <summary>
        /// Get the price for a template based on product type
        /// </summary>
        private decimal GetTemplatePrice(TemplateInfo template)
        {
            try
            {
                if (currentProduct != null)
                {
                    // Let overload resolution pick the right method:
                    // - if Type is ProductType (enum), enum overload will be chosen
                    // - if Type is string, string overload will be chosen  
                    return _pricingService.GetTemplateBasePrice(currentProduct.Type);
                }
                
                // Fallback: infer via template metadata if possible (category/type alias)
                var alias = template?.Category ?? template?.TemplateName ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    return _pricingService.GetTemplateBasePrice(alias);
                }
                
                // Final fallback
                return _pricingService.GetTemplateBasePrice(string.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error getting template price, using default fallback", ex);
                return 3.00m; // Default fallback price
            }
        }



        /// <summary>
        /// Converts TemplateInfo to Template for compatibility
        /// </summary>
        private async Task<Template> ConvertTemplateInfoToTemplateAsync(TemplateInfo templateInfo)
        {
            LoggingService.Application.Debug("Converting TemplateInfo to Template",
                ("TemplateName", templateInfo.TemplateName),
                ("TemplateImagePath", templateInfo.TemplateImagePath),
                ("ConfigIsNull", templateInfo.Config == null));
            
            if (templateInfo.Config != null)
            {
                LoggingService.Application.Debug("Template config details",
                    ("PhotoCount", templateInfo.Config.PhotoCount),
                    ("Dimensions", $"{templateInfo.Config.Dimensions?.Width}x{templateInfo.Config.Dimensions?.Height}"),
                    ("PhotoAreasCount", templateInfo.Config.PhotoAreas?.Count ?? 0));
            }
            
            // FIRST: Try to find existing template in database
            LoggingService.Application.Debug("Checking database for existing template");
            try
            {
                var existingTemplatesResult = await _databaseService.GetAllTemplatesAsync();
                
                if (existingTemplatesResult.Success && existingTemplatesResult.Data != null)
                {
                    var existingTemplate = existingTemplatesResult.Data
                        .FirstOrDefault(t => t.Name == templateInfo.TemplateName || 
                                           t.TemplatePath == templateInfo.TemplateImagePath);
                    
                    if (existingTemplate != null)
                    {
                        LoggingService.Application.Debug("Found existing template in database",
                            ("DatabaseTemplateId", existingTemplate.Id),
                            ("DatabaseTemplateLayoutId", existingTemplate.LayoutId),
                            ("DatabaseTemplatePhotoCount", existingTemplate.PhotoCount));
                        
                        // Load the layout and photo areas from database
                        var layoutResult = await _databaseService.GetTemplateLayoutAsync(existingTemplate.LayoutId);
                        
                        if (layoutResult.Success && layoutResult.Data != null)
                        {
                            existingTemplate.Layout = layoutResult.Data;
                            
                            // Load photo areas for the layout
                            var photoAreasResult = await _databaseService.GetTemplatePhotoAreasAsync(existingTemplate.LayoutId);
                            if (photoAreasResult.Success && photoAreasResult.Data != null)
                            {
                                existingTemplate.Layout.PhotoAreas = photoAreasResult.Data.ToList();
                                
                                LoggingService.Application.Debug("Loaded template with complete layout data",
                                    ("TemplateName", existingTemplate.Name),
                                    ("LayoutPhotoCount", existingTemplate.Layout.PhotoCount),
                                    ("PhotoAreasCount", existingTemplate.Layout.PhotoAreas.Count));
                                
                                return existingTemplate;
                            }
                        }
                    }
                    else
                    {
                        LoggingService.Application.Debug("No existing template found in database",
                            ("SearchedName", templateInfo.TemplateName),
                            ("SearchedPath", templateInfo.TemplateImagePath),
                            ("AvailableTemplatesCount", existingTemplatesResult.Data.Count));
                    }
                }
                else
                {
                    LoggingService.Application.Warning("Failed to get templates from database",
                        ("ErrorMessage", existingTemplatesResult.ErrorMessage ?? "Unknown error"));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error checking database for existing template", ex);
            }
            
            LoggingService.Application.Debug("Creating new template with generated photo areas");
            
            // Create a default layout with proper photo count for the template
            var defaultLayout = new TemplateLayout
            {
                Id = Guid.NewGuid().ToString(),
                LayoutKey = $"generated-{templateInfo.TemplateName}",
                Name = $"Generated Layout for {templateInfo.TemplateName}",
                Width = templateInfo.Config?.Dimensions?.Width ?? 1864,
                Height = templateInfo.Config?.Dimensions?.Height ?? 1240,
                PhotoCount = templateInfo.Config?.PhotoCount ?? 1,
                ProductCategoryId = 1,
                IsActive = true,
                SortOrder = 0,
                CreatedAt = DateTime.Now
            };

            // Generate photo areas for the layout
            defaultLayout.PhotoAreas = CreateDefaultPhotoAreas(
                defaultLayout.PhotoCount,
                defaultLayout.Width,
                defaultLayout.Height);

            // Create template object
            var template = new Template
            {
                Id = int.TryParse(templateInfo.Config?.TemplateId ?? "0", out var id) ? id : 0,
                Name = templateInfo.TemplateName,
                TemplatePath = templateInfo.TemplateImagePath,
                PreviewPath = templateInfo.PreviewImagePath,
                CategoryId = 1,
                LayoutId = defaultLayout.Id,
                Layout = defaultLayout,
                IsActive = true,
                Price = 0,
                SortOrder = 0,
                Description = templateInfo.Description ?? "Generated from template info",
                TemplateType = TemplateType.Strip
            };

            LoggingService.Application.Debug("Final template created",
                ("TemplateId", template.Id),
                ("TemplateName", template.Name),
                ("TemplatePath", template.TemplatePath),
                ("PhotoCount", template.PhotoCount),
                ("LayoutIsNull", template.Layout == null),
                ("LayoutPhotoCount", template.Layout?.PhotoCount ?? 0),
                ("LayoutPhotoAreasCount", template.Layout?.PhotoAreas?.Count ?? 0));

            return template;
        }

        /// <summary>
        /// Create default photo areas for a template layout
        /// </summary>
        private List<TemplatePhotoArea> CreateDefaultPhotoAreas(int photoCount, int templateWidth, int templateHeight)
        {
            LoggingService.Application.Debug("Creating photo areas",
                ("PhotoCount", photoCount),
                ("TemplateWidth", templateWidth),
                ("TemplateHeight", templateHeight));
            
            var photoAreas = new List<TemplatePhotoArea>();
            
            // Balanced dimensions - good size but fits in preview
            var horizontalMargin = 25; // Increased from 15px to 25px for better balance  
            var verticalMargin = 30;   // Increased from 20px to 30px for better spacing
            var photoSpacing = 12;     // Slightly increased spacing between photos from 10px to 12px
            
            var photoWidth = templateWidth - (2 * horizontalMargin); // Still wide but more balanced
            var availableHeight = templateHeight - (2 * verticalMargin);
            var photoHeight = (availableHeight - ((photoCount - 1) * photoSpacing)) / photoCount;
            
            LoggingService.Application.Debug("Calculated photo dimensions",
                ("PhotoWidth", photoWidth),
                ("PhotoHeight", photoHeight),
                ("HorizontalMargin", horizontalMargin),
                ("VerticalMargin", verticalMargin),
                ("PhotoSpacing", photoSpacing));
            
            for (int i = 0; i < photoCount; i++)
            {
                var photoArea = new TemplatePhotoArea
                {
                    LayoutId = "", // Will be set when layout is saved
                    PhotoIndex = i + 1,
                    X = horizontalMargin,
                    Y = verticalMargin + (i * (photoHeight + photoSpacing)),
                    Width = photoWidth,
                    Height = photoHeight,
                    Rotation = 0
                };
                
                LoggingService.Application.Debug("Created photo area {PhotoIndex}",
                    ("PhotoIndex", i + 1),
                    ("X", photoArea.X),
                    ("Y", photoArea.Y),
                    ("Width", photoArea.Width),
                    ("Height", photoArea.Height));
                photoAreas.Add(photoArea);
            }
            
            LoggingService.Application.Debug("Photo areas creation completed",
                ("TotalAreasCreated", photoAreas.Count));
            return photoAreas;
        }

        /// <summary>
        /// Handles template customization back button click
        /// </summary>
        private async void TemplateCustomizationScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                // Navigate back to template selection if we have current product
                if (currentProduct != null)
                {
                    await NavigateToTemplateSelectionWithCategories(currentProduct);
                }
                else
                {
                    // Fallback to product selection if no current product
                    await NavigateToProductSelection();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Template customization back navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles template customization completion
        /// </summary>
        private void TemplateCustomizationScreen_TemplateSelected(object? sender, Photobooth.TemplateCustomizedEventArgs e)
        {
            try
            {
                // Create a TemplateInfo from the selected Template for compatibility with existing flow
                var templateInfo = new TemplateInfo
                {
                    TemplateName = e.Template.Name ?? "Custom Template",
                    TemplateImagePath = e.Template.FilePath ?? "",
                    PreviewImagePath = e.Template.PreviewPath ?? "",
                    Category = currentCategory?.Name ?? "Unknown",
                    // Store customizations in a way that can be used later
                    Description = string.Join(", ", e.Customizations)
                };

                NavigateToPaymentOrCamera(templateInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Template customization completion failed: {ex.Message}");
            }
        }



        /// <summary>
        /// Handles photo session start request from template customization screen
        /// </summary>
        private async void TemplateCustomizationScreen_PhotoSessionStartRequested(object? sender, PhotoSessionStartEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Photo session start requested from template customization", 
                    ("TemplateName", e.Template.Name),
                    ("PhotoCount", e.PhotoCount));

                // Navigate to camera capture
                await NavigateToCameraCapture(e.Template);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photo session start failed", ex);
                System.Diagnostics.Debug.WriteLine($"Photo session start failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles camera capture screen back button click
        /// </summary>
        private async void CameraCaptureScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked back from camera capture");
                
                // Dispose camera resources
                cameraCaptureScreen?.Dispose();
                
                // Navigate back to template customization
                if (currentProduct != null && currentCategory != null)
                {
                    await NavigateToTemplateCustomization(currentProduct, currentCategory);
                }
                else
                {
                    NavigateToWelcome();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Camera capture back navigation failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles photos captured event from camera capture screen
        /// </summary>
        private async void CameraCaptureScreen_PhotosCaptured(object? sender, PhotosCapturedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Photos captured successfully", 
                    ("TemplateName", e.Template.Name),
                    ("PhotoCount", e.CapturedPhotosPaths.Count));

                // Compose photos in background while keeping camera screen visible
                await NavigateToPhotoPreviewWithComposition(e.Template, e.CapturedPhotosPaths);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photos captured navigation failed", ex);
                NotificationService.Instance.ShowError("Navigation Error", "Failed to show photo preview.");
                
                // Hide camera loading overlay on error
                if (cameraCaptureScreen != null)
                {
                    cameraCaptureScreen.HideLoading();
                }
            }
        }

        /// <summary>
        /// Handles retake photos request from photo preview screen
        /// </summary>
        private async void PhotoPreviewScreen_RetakePhotosRequested(object? sender, RetakePhotosEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Retake photos requested");
                
                // Add null checks to prevent NullReferenceException
                if (e == null)
                {
                    LoggingService.Application.Error("RetakePhotosEventArgs is null", null);
                    NotificationService.Instance.ShowError("Navigation Error", "Invalid retake request - missing event data.");
                    return;
                }
                
                if (e.Template == null)
                {
                    LoggingService.Application.Error("Template is null in RetakePhotosEventArgs", null);
                    NotificationService.Instance.ShowError("Navigation Error", "Invalid retake request - missing template data.");
                    return;
                }
                
                LoggingService.Application.Debug("Retake navigation details", 
                    ("TemplateName", e.Template.Name ?? "NULL"),
                    ("PhotoCount", e.Template.PhotoCount),
                    ("CurrentScreen", CurrentScreenContainer.Content?.GetType().Name ?? "None"),
                    ("CameraCaptureScreenExists", cameraCaptureScreen != null));

                // Navigate back to camera capture for retake
                LoggingService.Application.Debug("Starting camera capture navigation for retake");
                await NavigateToCameraCapture(e.Template);
                
                LoggingService.Application.Information("Retake navigation completed successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Retake navigation failed", ex);
                NotificationService.Instance.ShowError("Navigation Error", $"Failed to restart camera for retake: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles photos approved event from photo preview screen
        /// </summary>
        private async void PhotoPreviewScreen_PhotosApproved(object? sender, PhotosApprovedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User approved photos", 
                    ("TemplateName", e.Template.Name),
                    ("ComposedImagePath", e.ComposedImagePath));

                // Navigate to upselling screen
                if (currentProduct != null)
                {
                    await NavigateToUpsell(e.Template, currentProduct, e.ComposedImagePath, e.OriginalPhotosPaths);
                }
                else
                {
                    LoggingService.Application.Error("Current product is null - cannot proceed to upselling", null);
                    NotificationService.Instance.ShowError("Navigation Error", "Unable to proceed with upselling - missing product information.");
                    NavigateToWelcome();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photo approval handling failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles back button click from photo preview screen
        /// </summary>
        private async void PhotoPreviewScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked back from photo preview");
                
                // Navigate back to template customization
                if (currentProduct != null && currentCategory != null)
                {
                    await NavigateToTemplateCustomization(currentProduct, currentCategory);
                }
                else
                {
                    // Fallback to welcome screen if no context available
                    NavigateToWelcome();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photo preview back navigation failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles upsell completion
        /// </summary>
        private async void UpsellScreen_UpsellCompleted(object? sender, UpsellCompletedEventArgs e)
        {
            try
            {
                Console.WriteLine($"=== MAINWINDOW UPSELL COMPLETED DEBUG ===");
                Console.WriteLine($"RECEIVED UPSELL RESULT:");
                Console.WriteLine($"  Original Product: {e.Result.OriginalProduct?.Name} (${e.Result.OriginalProduct?.Price:F2})");
                Console.WriteLine($"  Extra Copies: {e.Result.ExtraCopies}");
                Console.WriteLine($"  Extra Copies Price: ${e.Result.ExtraCopiesPrice:F2}");
                Console.WriteLine($"  Cross-sell Accepted: {e.Result.CrossSellAccepted}");
                Console.WriteLine($"  Cross-sell Price: ${e.Result.CrossSellPrice:F2}");
                Console.WriteLine($"  Total Additional Cost: ${e.Result.TotalAdditionalCost:F2}");
                
                LoggingService.Application.Information("Upsell completed",
                    ("ExtraCopies", e.Result.ExtraCopies),
                    ("CrossSellAccepted", e.Result.CrossSellAccepted),
                    ("TotalAdditionalCost", e.Result.TotalAdditionalCost));

                // Clear upsell context since we're completing successfully
                ClearUpsellContext();

                Console.WriteLine("Navigating to printing...");
                // Navigate to printing with upsell results
                if (e.Result.OriginalProduct != null)
                {
                await NavigateToPrinting(
                    e.Result.OriginalTemplate,
                    e.Result.OriginalProduct,
                    e.Result.ComposedImagePath,
                    e.Result.CapturedPhotosPaths,
                    e.Result.ExtraCopies,
                    e.Result.CrossSellProduct,
                    e.Result.TotalAdditionalCost,
                    e.Result.CrossSellAccepted,
                    e.Result.TotalOrderCost  // Pass the precomputed total from UpsellScreen
                );
                }
                else
                {
                    LoggingService.Application.Error("Upsell result missing original product");
                    NavigateToWelcome();
                }
                Console.WriteLine($"=== MAINWINDOW UPSELL PROCESSING COMPLETE ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Upsell completion handling failed: {ex.Message}");
                LoggingService.Application.Error("Upsell completion handling failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles upsell timeout
        /// </summary>
        private async void UpsellScreen_UpsellTimeout(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Warning("Upsell timeout - proceeding to print original order only");
                
                // Use stored context to proceed with printing the original order
                if (_currentUpsellTemplate != null && 
                    _currentUpsellOriginalProduct != null && 
                    _currentUpsellComposedImagePath != null && 
                    _currentUpsellCapturedPhotosPaths != null)
                {
                    LoggingService.Application.Information("Processing original order after timeout",
                        ("TemplateName", _currentUpsellTemplate.Name ?? "Unknown"),
                        ("OriginalProduct", _currentUpsellOriginalProduct.Type));

                    // Proceed to print original order with no upsells
                    await NavigateToPrinting(
                        _currentUpsellTemplate,
                        _currentUpsellOriginalProduct,
                        _currentUpsellComposedImagePath,
                        _currentUpsellCapturedPhotosPaths,
                        0, // No extra copies
                        null, // No cross-sell
                        0, // No additional cost
                        false // Cross-sell not accepted
                    );

                    // Clear context after successful processing
                    ClearUpsellContext();
                }
                else
                {
                    LoggingService.Application.Error("Upsell timeout context missing - cannot process original order");
                    NavigateToWelcome();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upsell timeout handling failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Clear stored upsell context to prevent memory leaks and state issues
        /// </summary>
        private void ClearUpsellContext()
        {
            _currentUpsellTemplate = null;
            _currentUpsellOriginalProduct = null;
            _currentUpsellComposedImagePath = null;
            _currentUpsellCapturedPhotosPaths = null;
        }

        /// <summary>
        /// Handle printing completion
        /// </summary>
        private void PrintingScreen_PrintingCompleted(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Printing completed - returning to welcome screen");

                // Refresh credit displays across all screens after successful transaction
                RefreshAllCreditDisplays();

                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Print completion handling failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Refresh credit displays across all active screens
        /// </summary>
        private void RefreshAllCreditDisplays()
        {
            try
            {
                // Get current credits from admin dashboard
                var currentCredits = adminDashboardScreen?.GetCurrentCredits() ?? 0;

                LoggingService.Application.Information("Refreshing credit displays across all screens",
                    ("CurrentCredits", currentCredits),
                    ("OperationMode", currentOperationMode),
                    ("IsFreePlayMode", IsFreePlayMode));

                // Update all instantiated screens that have UpdateCredits methods
                // This will also trigger UpdateCreditsDisplay() which checks the operation mode
                productSelectionScreen?.UpdateCredits(currentCredits);
                templateSelectionScreen?.UpdateCredits(currentCredits);
                categorySelectionScreen?.UpdateCredits(currentCredits);
                templateCustomizationScreen?.UpdateCredits(currentCredits);
                photoPreviewScreen?.UpdateCredits(currentCredits);
                upsellScreen?.UpdateCredits(currentCredits);
                printingScreen?.UpdateCredits(currentCredits);
                cameraCaptureScreen?.UpdateCredits(currentCredits);
                
                LoggingService.Application.Information("Credit displays refreshed across all screens");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to refresh credit displays", ex);
            }
        }

        /// <summary>
        /// Handle printing cancellation
        /// </summary>
        private void PrintingScreen_PrintingCancelled(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Printing cancelled - returning to welcome screen");
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Print cancellation handling failed", ex);
                NavigateToWelcome();
            }
        }



        /// <summary>
        /// Handles successful admin login
        /// </summary>
        private async void AdminLoginScreen_LoginSuccessful(object? sender, AdminLoginEventArgs e)
        {
            try
            {
                if (e.IsUsingSetupCredentials)
                {
                    LoggingService.Application.Warning("Setup credentials detected - redirecting to forced password change",
                        ("UserId", e.UserId),
                        ("Username", e.Username),
                        ("AccessLevel", e.AccessLevel.ToString()));
                    
                    // Get user data for forced password change screen
                    var userData = await _databaseService.GetByUserIdAsync<AdminUser>(e.UserId);
                    if (userData.Success && userData.Data != null)
                    {
                        await NavigateToForcedPasswordChange(userData.Data, e.AccessLevel);
                    }
                    else
                    {
                        // Fallback - go to normal dashboard if user data retrieval fails
                        LoggingService.Application.Error("Failed to retrieve user data for forced password change", null,
                            ("UserId", e.UserId));
                        await NavigateToAdminDashboard(e.AccessLevel, e.UserId);
                    }
                }
                else
                {
                    // Normal login - check if PIN setup is required
                    var userData = await _databaseService.GetByUserIdAsync<AdminUser>(e.UserId);
                    if (userData.Success && userData.Data != null)
                    {
                        if (userData.Data.PINSetupRequired)
                        {
                            LoggingService.Application.Information("PIN setup required - redirecting to PIN setup screen",
                                ("UserId", e.UserId),
                                ("Username", e.Username),
                                ("AccessLevel", e.AccessLevel.ToString()));
                            
                            // Navigate to PIN setup screen
                            NavigateToPINSetup(userData.Data, e.AccessLevel);
                        }
                        else
                        {
                            // PIN already set up - go to dashboard
                            await NavigateToAdminDashboard(e.AccessLevel, e.UserId);
                        }
                    }
                    else
                    {
                        // Fallback - go to dashboard if user data retrieval fails
                        LoggingService.Application.Error("Failed to retrieve user data for PIN setup check", null,
                            ("UserId", e.UserId));
                        await NavigateToAdminDashboard(e.AccessLevel, e.UserId);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Admin login success navigation failed", ex,
                    ("UserId", e.UserId),
                    ("Username", e.Username));
                System.Diagnostics.Debug.WriteLine($"Admin login success navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles admin login cancellation or failure
        /// </summary>
        private void AdminLoginScreen_LoginCancelled(object? sender, EventArgs e)
        {
            try
            {
                // Always return to welcome screen when cancelling admin login
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin login cancel navigation failed: {ex.Message}");
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles forgot password request from login screen - navigate to PIN recovery
        /// </summary>
        private void AdminLoginScreen_ForgotPasswordRequested(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Forgot password requested - navigating to PIN recovery");
                NavigateToPINRecovery();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Forgot password navigation failed", ex);
                NavigateToAdminLogin();
            }
        }

        /// <summary>
        /// Handles PIN setup completion - navigate to admin dashboard
        /// </summary>
        private async void PINSetupScreen_PINSetupCompleted(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("PIN setup completed - proceeding to admin dashboard",
                    ("UserId", _tempUserId ?? "Unknown"),
                    ("AccessLevel", _tempAccessLevel.ToString()));

                // Navigate to admin dashboard
                await NavigateToAdminDashboard(_tempAccessLevel, _tempUserId ?? string.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Post-PIN-setup navigation failed", ex,
                    ("UserId", _tempUserId ?? "Unknown"));
                System.Diagnostics.Debug.WriteLine($"Post-PIN-setup navigation failed: {ex.Message}");
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles successful PIN verification - navigate to password reset
        /// </summary>
        private void PINRecoveryScreen_RecoverySuccessful(object? sender, AdminUser user)
        {
            try
            {
                LoggingService.Application.Information("PIN recovery successful - proceeding to password reset",
                    ("UserId", user.UserId),
                    ("Username", user.Username));

                // Navigate to password reset screen
                NavigateToPasswordReset(user);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Post-PIN-recovery navigation failed", ex,
                    ("UserId", user?.UserId ?? "Unknown"));
                NavigateToAdminLogin();
            }
        }

        /// <summary>
        /// Handles PIN recovery back to login request
        /// </summary>
        private void PINRecoveryScreen_BackToLogin(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User returned to login from PIN recovery");
                NavigateToAdminLogin();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation back to login failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles successful password reset - return to login
        /// </summary>
        private void PasswordResetScreen_PasswordResetSuccessful(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Password reset successful - returning to login");
                NavigateToAdminLogin();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Post-password-reset navigation failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handle password reset cancellation - user clicked Back button
        /// </summary>
        private void PasswordResetScreen_PasswordResetCancelled(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Password reset cancelled by user - returning to login");
                NavigateToAdminLogin();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Post-password-reset-cancellation navigation failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles admin dashboard exit request
        /// </summary>
        private void AdminDashboardScreen_ExitAdminRequested(object? sender, EventArgs e)
        {
            try
            {
                // Reset admin access and return to welcome
                currentAdminAccess = AdminAccessLevel.None;
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin exit navigation failed: {ex.Message}");
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles successful password change completion
        /// Rationale: After password change, show PIN setup screen before going to admin dashboard
        /// </summary>
        private void ForcedPasswordChangeScreen_PasswordChangeCompleted(object? sender, PasswordChangeCompletedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== PASSWORD CHANGE COMPLETED EVENT FIRED ===");
                System.Diagnostics.Debug.WriteLine($"User: {e.User.Username}, AccessLevel: {e.AccessLevel}");
                
                LoggingService.Application.Information("Setup password changed successfully - proceeding to PIN setup",
                    ("UserId", e.User.UserId),
                    ("Username", e.User.Username),
                    ("AccessLevel", e.AccessLevel.ToString()));

                // Clean up setup credentials folder now that password has been changed
                try
                {
                    DatabaseService.CleanupSetupCredentials();
                    LoggingService.Application.Information("Setup credentials folder cleaned up after password change");
                }
                catch (Exception cleanupEx)
                {
                    LoggingService.Application.Warning("Setup credentials cleanup failed after password change",
                        ("Error", cleanupEx.Message));
                }

                System.Diagnostics.Debug.WriteLine("=== CALLING NavigateToPINSetup ===");
                
                // Navigate to PIN setup screen
                NavigateToPINSetup(e.User, e.AccessLevel);
                
                System.Diagnostics.Debug.WriteLine("=== NavigateToPINSetup COMPLETED ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== ERROR IN PASSWORD CHANGE HANDLER: {ex.Message} ===");
                LoggingService.Application.Error("Post-password-change navigation failed", ex,
                    ("UserId", e.User.UserId),
                    ("Username", e.User.Username));
                System.Diagnostics.Debug.WriteLine($"Post-password-change navigation failed: {ex.Message}");
                // Fallback to welcome screen
                NavigateToWelcome();
            }
        }



        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Gets the currently selected product
        /// </summary>
        public ProductInfo? CurrentProduct => currentProduct;

        /// <summary>
        /// Gets the currently selected template
        /// </summary>
        public TemplateInfo? CurrentTemplate => currentTemplate;

        /// <summary>
        /// Gets the current admin access level
        /// </summary>
        public AdminAccessLevel CurrentAdminAccess => currentAdminAccess;

        /// <summary>
        /// Forces navigation to welcome screen (for external use)
        /// </summary>
        public void ForceNavigateToWelcome()
        {
            try
            {
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force navigation to welcome failed: {ex.Message}");
            }
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Cleanup resources on window close
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("PhotoBoothX shutting down");
                
                // Reset virtual keyboard state on application close
                VirtualKeyboardService.Instance.ResetState();
                
                // Cleanup screens that have Cleanup methods
                welcomeScreen?.Cleanup();
                productSelectionScreen?.Cleanup();
                templateSelectionScreen?.Cleanup();

                // Dispose camera and photo preview screens
                cameraCaptureScreen?.Dispose();
                photoPreviewScreen?.Dispose();
                
                // Dispose printing screen
                printingScreen?.Dispose();

                // Unsubscribe from welcome screen events
                if (welcomeScreen != null)
                {
                    welcomeScreen.StartButtonClicked -= WelcomeScreen_StartButtonClicked;
                    welcomeScreen.AdminAccessRequested -= WelcomeScreen_AdminAccessRequested;
                }

                // Unsubscribe from camera capture screen events
                if (cameraCaptureScreen != null)
                {
                    cameraCaptureScreen.BackButtonClicked -= CameraCaptureScreen_BackButtonClicked;
                    cameraCaptureScreen.PhotosCaptured -= CameraCaptureScreen_PhotosCaptured;
                }

                // Unsubscribe from photo preview screen events
                if (photoPreviewScreen != null)
                {
                    photoPreviewScreen.RetakePhotosRequested -= PhotoPreviewScreen_RetakePhotosRequested;
                    photoPreviewScreen.PhotosApproved -= PhotoPreviewScreen_PhotosApproved;
                }

                // Unsubscribe from admin screen events
                if (adminLoginScreen != null)
                {
                    adminLoginScreen.LoginSuccessful -= AdminLoginScreen_LoginSuccessful;
                    adminLoginScreen.LoginCancelled -= AdminLoginScreen_LoginCancelled;
                    adminLoginScreen.ForgotPasswordRequested -= AdminLoginScreen_ForgotPasswordRequested;
                }

                if (adminDashboardScreen != null)
                {
                    adminDashboardScreen.ExitAdminRequested -= AdminDashboardScreen_ExitAdminRequested;
                }

                if (forcedPasswordChangeScreen != null)
                {
                    forcedPasswordChangeScreen.PasswordChangeCompleted -= ForcedPasswordChangeScreen_PasswordChangeCompleted;
                }

                if (pinSetupScreen != null)
                {
                    pinSetupScreen.PINSetupCompleted -= PINSetupScreen_PINSetupCompleted;
                }

                if (pinRecoveryScreen != null)
                {
                    pinRecoveryScreen.RecoverySuccessful -= PINRecoveryScreen_RecoverySuccessful;
                    pinRecoveryScreen.BackToLogin -= PINRecoveryScreen_BackToLogin;
                }

                if (passwordResetScreen != null)
                {
                    passwordResetScreen.PasswordResetSuccessful -= PasswordResetScreen_PasswordResetSuccessful;
                    passwordResetScreen.PasswordResetCancelled -= PasswordResetScreen_PasswordResetCancelled;
                }

                // Shutdown logging system
                LoggingService.Shutdown();

                base.OnClosed(e);
            }
            catch (Exception ex)
            {
                // Fallback to console if logging system is already shut down
                System.Diagnostics.Debug.WriteLine($"Window cleanup failed: {ex.Message}");
                base.OnClosed(e);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Converts a database Template object to a TemplateInfo object for UI display
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private TemplateInfo? ConvertDatabaseTemplateToTemplateInfo(Template dbTemplate)
        {
            return _templateConversionService.ConvertDatabaseTemplateToTemplateInfo(dbTemplate);
        }

        /// <summary>
        /// Gets standard display size based on aspect ratio
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private (double width, double height) GetStandardDisplaySize(int actualWidth, int actualHeight)
        {
            return _templateConversionService.GetStandardDisplaySize(actualWidth, actualHeight);
        }

        /// <summary>
        /// Gets aspect ratio text for display
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private string GetAspectRatioText(double aspectRatio)
        {
            return _templateConversionService.GetAspectRatioText(aspectRatio);
        }

        /// <summary>
        /// Gets template size category
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private string GetTemplateSizeCategory(double aspectRatio)
        {
            return _templateConversionService.GetTemplateSizeCategory(aspectRatio);
        }

        /// <summary>
        /// Validates if template is valid for the selected product
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private bool IsTemplateValidForProduct(TemplateInfo template, ProductInfo product)
        {
            return _templateConversionService.IsTemplateValidForProduct(template, product);
        }

        #endregion

        #region Master Password Initialization

        /// <summary>
        /// Initialize master password config after database is ready
        /// This ensures config file is loaded into encrypted database and deleted immediately
        /// </summary>
        private async System.Threading.Tasks.Task InitializeMasterPasswordConfigAsync()
        {
            try
            {
                Console.WriteLine("[SECURITY] Initializing master password config (database is now ready)...");
                
                var masterPasswordService = new Photobooth.Services.MasterPasswordService();
                var masterPasswordConfigService = new Photobooth.Services.MasterPasswordConfigService(_databaseService, masterPasswordService);
                
                // Try to get base secret - this will load from config file if needed and delete it
                try
                {
                    await masterPasswordConfigService.GetBaseSecretAsync();
                    Console.WriteLine("[SECURITY] Master password config initialized and secured.");
                }
                catch (InvalidOperationException)
                {
                    // Expected if master password feature is not configured
                    Console.WriteLine("[INFO] Master password feature not configured (this is normal for self-hosted builds)");
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash the app
                Console.WriteLine($"[WARNING] Failed to initialize master password config: {ex.Message}");
                LoggingService.Application.Warning($"Failed to initialize master password config: {ex.Message}");
            }
        }

        #endregion


    }
}
