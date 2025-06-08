using System;
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
        private TemplateSelectionScreen? templateSelectionScreen;
        private AdminLoginScreen? adminLoginScreen;
        private AdminDashboardScreen? adminDashboardScreen;
        private ForcedPasswordChangeScreen? forcedPasswordChangeScreen;

        // Current state tracking
        private ProductInfo? currentProduct;
        private TemplateInfo? currentTemplate;
        private AdminAccessLevel currentAdminAccess = AdminAccessLevel.None;



        // Database service
        private readonly IDatabaseService _databaseService;

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
            
            InitializeComponent();
            
            // Initialize notification service with the notification container
            NotificationService.Instance.Initialize(NotificationContainer);
            

            InitializeDatabaseAsync();
            InitializeApplication();
        }



        /// <summary>
        /// Initialize the database asynchronously
        /// </summary>
        private async void InitializeDatabaseAsync()
        {
            try
            {
                LoggingService.Application.Information("Database initialization starting",
                    ("ConnectionString", "Data Source=[PATH]"));
                
                var result = await _databaseService.InitializeAsync();
                if (!result.Success)
                {
                    LoggingService.Application.Error("Database initialization failed", null,
                        ("ErrorMessage", result.ErrorMessage ?? "Unknown error"));
                }
                else
                {
                    LoggingService.Application.Information("Database initialization completed successfully");
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
        private void InitializeApplication()
        {
            // Start with the welcome screen
            NavigateToWelcome();
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
                LoggingService.Application.Information("Navigating to welcome screen");
                
                if (welcomeScreen == null)
                {
                    welcomeScreen = new WelcomeScreen();
                    // Subscribe to the welcome screen's navigation event
                    welcomeScreen.StartButtonClicked += WelcomeScreen_StartButtonClicked;
                    welcomeScreen.AdminAccessRequested += WelcomeScreen_AdminAccessRequested;
                    LoggingService.Application.Debug("Welcome screen initialized and event handlers attached");
                }

                CurrentScreenContainer.Content = welcomeScreen;

                // Reset state when returning to welcome
                currentProduct = null;
                currentTemplate = null;
                currentAdminAccess = AdminAccessLevel.None;
                
                LoggingService.Application.Information("Welcome screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to welcome screen failed", ex);
                System.Diagnostics.Debug.WriteLine($"Navigation to welcome failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to the product selection screen
        /// Called when user clicks "Touch to Start" on welcome screen
        /// </summary>
        public void NavigateToProductSelection()
        {
            try
            {
                if (productSelectionScreen == null)
                {
                    productSelectionScreen = new ProductSelectionScreen();
                    // Subscribe to product selection events
                    productSelectionScreen.BackButtonClicked += ProductSelectionScreen_BackButtonClicked;
                    productSelectionScreen.ProductSelected += ProductSelectionScreen_ProductSelected;
                }

                CurrentScreenContainer.Content = productSelectionScreen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation to product selection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to the template selection screen
        /// Called when user selects a product type
        /// </summary>
        public void NavigateToTemplateSelection(ProductInfo product)
        {
            try
            {
                if (templateSelectionScreen == null)
                {
                    templateSelectionScreen = new TemplateSelectionScreen();
                    // Subscribe to template selection events
                    templateSelectionScreen.BackButtonClicked += TemplateSelectionScreen_BackButtonClicked;
                    templateSelectionScreen.TemplateSelected += TemplateSelectionScreen_TemplateSelected;
                }

                // Set the product type for template filtering
                templateSelectionScreen.SetProductType(product);
                currentProduct = product;

                CurrentScreenContainer.Content = templateSelectionScreen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation to template selection failed: {ex.Message}");
                // Fallback to product selection if template navigation fails
                NavigateToProductSelection();
            }
        }

        /// <summary>
        /// Navigates to payment/camera screen (placeholder for now)
        /// Called when user selects a template
        /// </summary>
        public void NavigateToPaymentOrCamera(TemplateInfo template)
        {
            try
            {
                currentTemplate = template;

                // TODO: Implement payment/camera screen navigation
                // For now, show a message with the selection
                var message = $"Selected:\n" +
                             $"Product: {currentProduct?.Name} (${currentProduct?.Price})\n" +
                             $"Template: {template.TemplateName}\n" +
                             $"Category: {template.Category}\n\n" +
                             $"Next: Payment/Camera Screen";

                NotificationService.Instance.ShowInfo("Selection Complete", message, 8);

                // Return to welcome for now
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation to payment/camera failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to admin login screen
        /// Called when admin access sequence is completed (5-tap detection)
        /// </summary>
        public void NavigateToAdminLogin()
        {
            try
            {
                if (adminLoginScreen == null)
                {
                    adminLoginScreen = new AdminLoginScreen();
                    // Subscribe to admin login events
                    adminLoginScreen.LoginSuccessful += AdminLoginScreen_LoginSuccessful;
                    adminLoginScreen.LoginCancelled += AdminLoginScreen_LoginCancelled;
                }

                // Reset the login screen
                adminLoginScreen.Reset();
                CurrentScreenContainer.Content = adminLoginScreen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation to admin login failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to the forced password change screen for setup credentials
        /// </summary>
        public System.Threading.Tasks.Task NavigateToForcedPasswordChange(AdminUser user, AdminAccessLevel accessLevel)
        {
            try
            {
                // Create new instance each time to ensure clean state
                forcedPasswordChangeScreen = new ForcedPasswordChangeScreen(user, accessLevel);
                
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
        /// Navigate to admin dashboard with specified access level
        /// Called after successful admin login
        /// </summary>
        public async System.Threading.Tasks.Task NavigateToAdminDashboard(AdminAccessLevel accessLevel, string userId)
        {
            try
            {
                if (adminDashboardScreen == null)
                {
                    adminDashboardScreen = new AdminDashboardScreen(_databaseService);
                    // Subscribe to admin dashboard events
                    adminDashboardScreen.ExitAdminRequested += AdminDashboardScreen_ExitAdminRequested;
                }

                // Set access level and configure UI accordingly
                currentAdminAccess = accessLevel;
                await adminDashboardScreen.SetAccessLevel(accessLevel, userId);
                adminDashboardScreen.RefreshSalesData();

                CurrentScreenContainer.Content = adminDashboardScreen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation to admin dashboard failed: {ex.Message}");
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
        private void WelcomeScreen_StartButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked 'Touch to Start' button");
                LoggingService.Transaction.Information("USER_INTERACTION", "Customer session started",
                    ("Action", "TouchToStart"),
                    ("Timestamp", DateTime.Now));
                NavigateToProductSelection();
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
        private void ProductSelectionScreen_ProductSelected(object? sender, ProductSelectedEventArgs e)
        {
            try
            {
                NavigateToTemplateSelection(e.ProductInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Product selection navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles template selection back button click
        /// </summary>
        private void TemplateSelectionScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                NavigateToProductSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Template selection back navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles template selection
        /// </summary>
        private void TemplateSelectionScreen_TemplateSelected(object? sender, TemplateSelectedEventArgs e)
        {
            try
            {
                NavigateToPaymentOrCamera(e.Template);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Template selection navigation failed: {ex.Message}");
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
                    // Normal login - go to dashboard
                    await NavigateToAdminDashboard(e.AccessLevel, e.UserId);
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
        /// </summary>
        private async void ForcedPasswordChangeScreen_PasswordChangeCompleted(object? sender, PasswordChangeCompletedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Setup password changed successfully - proceeding to admin dashboard",
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

                // Navigate to admin dashboard with new credentials
                await NavigateToAdminDashboard(e.AccessLevel, e.User.UserId);
            }
            catch (Exception ex)
            {
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
                
                // Cleanup screens that have Cleanup methods
                welcomeScreen?.Cleanup();
                productSelectionScreen?.Cleanup();
                templateSelectionScreen?.Cleanup();

                // Unsubscribe from welcome screen events
                if (welcomeScreen != null)
                {
                    welcomeScreen.StartButtonClicked -= WelcomeScreen_StartButtonClicked;
                    welcomeScreen.AdminAccessRequested -= WelcomeScreen_AdminAccessRequested;
                }

                // Unsubscribe from admin screen events
                if (adminLoginScreen != null)
                {
                    adminLoginScreen.LoginSuccessful -= AdminLoginScreen_LoginSuccessful;
                    adminLoginScreen.LoginCancelled -= AdminLoginScreen_LoginCancelled;
                }

                if (adminDashboardScreen != null)
                {
                    adminDashboardScreen.ExitAdminRequested -= AdminDashboardScreen_ExitAdminRequested;
                }

                if (forcedPasswordChangeScreen != null)
                {
                    forcedPasswordChangeScreen.PasswordChangeCompleted -= ForcedPasswordChangeScreen_PasswordChangeCompleted;
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


    }
}