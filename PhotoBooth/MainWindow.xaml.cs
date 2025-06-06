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

        // Current state tracking
        private ProductInfo? currentProduct;
        private TemplateInfo? currentTemplate;
        private AdminAccessLevel currentAdminAccess = AdminAccessLevel.None;

        // Admin access detection (5-tap sequence)
        private int adminTapCount = 0;
        private const int ADMIN_TAP_SEQUENCE_COUNT = 5;
        private const double ADMIN_TAP_TIME_WINDOW = 3.0; // seconds
        private DispatcherTimer? adminTapTimer;

        // Database service
        private readonly IDatabaseService _databaseService;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the main window and shows the welcome screen
        /// </summary>
        public MainWindow()
        {
            Console.WriteLine("MainWindow: Constructor called - Debug console is working!");
            
            // Initialize database service
            _databaseService = new DatabaseService();
            
            InitializeComponent();
            
            // Initialize notification service with the notification container
            NotificationService.Instance.Initialize(NotificationContainer);
            
            InitializeAdminTapDetection();
            InitializeDatabaseAsync();
            InitializeApplication();
        }

        /// <summary>
        /// Initialize the admin tap detection system
        /// </summary>
        private void InitializeAdminTapDetection()
        {
            adminTapTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ADMIN_TAP_TIME_WINDOW)
            };
            adminTapTimer.Tick += AdminTapTimer_Tick;
        }

        /// <summary>
        /// Initialize the database asynchronously
        /// </summary>
        private async void InitializeDatabaseAsync()
        {
            try
            {
                Console.WriteLine("MainWindow: Starting database initialization...");
                var result = await _databaseService.InitializeAsync();
                if (!result.Success)
                {
                    Console.WriteLine($"Database initialization failed: {result.ErrorMessage}");
                }
                else
                {
                    Console.WriteLine("MainWindow: Database initialization completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization error: {ex.Message}");
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
                if (welcomeScreen == null)
                {
                    welcomeScreen = new WelcomeScreen();
                    // Subscribe to the welcome screen's navigation event
                    welcomeScreen.StartButtonClicked += WelcomeScreen_StartButtonClicked;
                }

                CurrentScreenContainer.Content = welcomeScreen;

                // Reset state when returning to welcome
                currentProduct = null;
                currentTemplate = null;
                currentAdminAccess = AdminAccessLevel.None;
            }
            catch (Exception ex)
            {
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
                    templateSelectionScreen.AdminAccessRequested += TemplateSelectionScreen_AdminAccessRequested;
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
                NavigateToProductSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Welcome start navigation failed: {ex.Message}");
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
        /// Handles admin access request from template selection screen
        /// This is triggered by the 5-tap sequence
        /// </summary>
        private void TemplateSelectionScreen_AdminAccessRequested(object? sender, EventArgs e)
        {
            try
            {
                NavigateToAdminLogin();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin access navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles successful admin login
        /// </summary>
        private async void AdminLoginScreen_LoginSuccessful(object? sender, AdminLoginEventArgs e)
        {
            try
            {
                await NavigateToAdminDashboard(e.AccessLevel, e.UserId);
            }
            catch (Exception ex)
            {
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
                // Cleanup screens that have Cleanup methods
                welcomeScreen?.Cleanup();
                productSelectionScreen?.Cleanup();
                templateSelectionScreen?.Cleanup();

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

                // Cleanup admin tap timer
                if (adminTapTimer != null)
                {
                    adminTapTimer.Stop();
                    adminTapTimer.Tick -= AdminTapTimer_Tick;
                    adminTapTimer = null;
                }

                base.OnClosed(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Window cleanup failed: {ex.Message}");
                base.OnClosed(e);
            }
        }

        #endregion

        #region Admin Access Detection

        /// <summary>
        /// Handles taps on the admin access zone (top-left corner)
        /// </summary>
        private void AdminAccessZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                adminTapCount++;
                System.Diagnostics.Debug.WriteLine($"Admin tap {adminTapCount}/{ADMIN_TAP_SEQUENCE_COUNT}");

                // Start or restart the timer
                adminTapTimer?.Stop();
                adminTapTimer?.Start();

                // Check if we've reached the required tap count
                if (adminTapCount >= ADMIN_TAP_SEQUENCE_COUNT)
                {
                    TriggerAdminAccess();
                }

                // Visual feedback (brief flash)
                AdminAccessZone.Opacity = 0.3;
                var flashTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(150)
                };
                flashTimer.Tick += (s, args) =>
                {
                    AdminAccessZone.Opacity = 0;
                    flashTimer.Stop();
                };
                flashTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin tap detection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset admin tap count when timer expires
        /// </summary>
        private void AdminTapTimer_Tick(object? sender, EventArgs e)
        {
            adminTapTimer?.Stop();
            adminTapCount = 0;
            System.Diagnostics.Debug.WriteLine("Admin tap sequence reset (timeout)");
        }

        /// <summary>
        /// Trigger admin access sequence
        /// </summary>
        private void TriggerAdminAccess()
        {
            try
            {
                adminTapTimer?.Stop();
                adminTapCount = 0;

                System.Diagnostics.Debug.WriteLine("Admin access sequence triggered!");

                // Navigate to admin login
                NavigateToAdminLogin();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin access trigger error: {ex.Message}");
            }
        }

        #endregion
    }
}