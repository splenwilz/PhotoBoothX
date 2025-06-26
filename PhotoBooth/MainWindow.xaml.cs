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
        private AdminLoginScreen? adminLoginScreen;
        private AdminDashboardScreen? adminDashboardScreen;
        private ForcedPasswordChangeScreen? forcedPasswordChangeScreen;

        // Current state tracking
        private ProductInfo? currentProduct;
        private TemplateCategory? currentCategory;
        private TemplateInfo? currentTemplate;
        private AdminAccessLevel currentAdminAccess = AdminAccessLevel.None;



        // Database service
        private readonly IDatabaseService _databaseService;
        
        // Template conversion service
        private readonly ITemplateConversionService _templateConversionService;

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
            
            InitializeComponent();
            
            // Initialize notification service with the notification container
            NotificationService.Instance.Initialize(NotificationContainer);
            
            // Initialize modal service with the modal overlay containers
            ModalService.Instance.Initialize(ModalOverlayContainer, ModalContentContainer, ModalBackdrop);

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
                System.Diagnostics.Debug.WriteLine("=== NAVIGATING TO WELCOME ===");
                LoggingService.Application.Information("Navigating to welcome screen");
                
                // Hide virtual keyboard when navigating away from admin screens
                VirtualKeyboardService.Instance.HideKeyboard();

                // Clear any admin state
                currentAdminAccess = AdminAccessLevel.None;

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
                currentCategory = null;
                currentTemplate = null;
                
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
        /// Navigates to the category selection screen
        /// Called when user selects a product type
        /// </summary>
        public void NavigateToCategorySelection(ProductInfo product)
        {
            try
            {
                LoggingService.Application.Information("Navigating to category selection",
                    ("ProductType", product.Type),
                    ("ProductName", product.Name));

                if (categorySelectionScreen == null)
                {
                    categorySelectionScreen = new CategorySelectionScreen();
                    // Subscribe to category selection events
                    categorySelectionScreen.BackButtonClicked += CategorySelectionScreen_BackButtonClicked;
                    categorySelectionScreen.CategorySelected += CategorySelectionScreen_CategorySelected;
                }

                // Set the product type for category filtering
                categorySelectionScreen.SetProductType(product);
                currentProduct = product;

                CurrentScreenContainer.Content = categorySelectionScreen;
                
                LoggingService.Application.Information("Category selection screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to category selection failed", ex);
                System.Diagnostics.Debug.WriteLine($"Navigation to category selection failed: {ex.Message}");
                // Fallback to product selection if category navigation fails
                NavigateToProductSelection();
            }
        }

        /// <summary>
        /// Navigates to the template selection screen with a pre-selected category
        /// Called when user selects a category
        /// </summary>
        public void NavigateToTemplateSelection(ProductInfo product, TemplateCategory category)
        {
            try
            {
                LoggingService.Application.Information("Navigating to template selection with category",
                    ("ProductType", product.Type),
                    ("CategoryId", category.Id),
                    ("CategoryName", category.Name));

                if (templateSelectionScreen == null)
                {
                    templateSelectionScreen = new TemplateSelectionScreen(_databaseService, _templateConversionService);
                    // Subscribe to template selection events
                    templateSelectionScreen.BackButtonClicked += TemplateSelectionScreen_BackButtonClicked;
                    templateSelectionScreen.TemplateSelected += TemplateSelectionScreen_TemplateSelected;
                }

                // Set the product type for template filtering
                templateSelectionScreen.SetProductType(product);
                currentProduct = product;
                currentCategory = category;

                CurrentScreenContainer.Content = templateSelectionScreen;
                
                LoggingService.Application.Information("Template selection screen loaded successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Navigation to template selection failed", ex);
                System.Diagnostics.Debug.WriteLine($"Navigation to template selection failed: {ex.Message}");
                // Fallback to category selection if template navigation fails
                NavigateToCategorySelection(product);
            }
        }

        /// <summary>
        /// Navigate to template selection screen with categorized view
        /// </summary>
        public void NavigateToTemplateSelectionWithCategories(ProductInfo product)
        {
            try
            {
                currentProduct = product;

                LoggingService.Application.Information("Navigating to template selection with categorized view",
                    ("ProductType", product.Type));

                if (templateSelectionScreen == null)
                {
                    Console.WriteLine("Creating new TemplateSelectionScreen...");
                    templateSelectionScreen = new TemplateSelectionScreen(_databaseService, _templateConversionService);
                    
                    // Subscribe to events
                    Console.WriteLine("Subscribing to TemplateSelectionScreen events...");
                    templateSelectionScreen.BackButtonClicked += TemplateSelectionScreen_BackButtonClicked;
                    templateSelectionScreen.TemplateSelected += TemplateSelectionScreen_TemplateSelected;

                    Console.WriteLine("TemplateSelectionScreen events subscribed successfully");
                }
                else
                {
                    Console.WriteLine("Using existing TemplateSelectionScreen");
                }

                // Set the product type for template filtering
                templateSelectionScreen.SetProductType(product);

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
                NavigateToProductSelection();
            }
        }



        /// <summary>
        /// Navigate to template customization with a specific template selected
        /// </summary>
        public void NavigateToTemplateCustomizationWithTemplate(TemplateInfo? template)
        {
            try
            {
                Console.WriteLine("=== NAVIGATE TO TEMPLATE CUSTOMIZATION ===");
                Console.WriteLine($"Template: {template?.TemplateName ?? "NULL"}");
                Console.WriteLine($"Current Product: {currentProduct?.Name ?? "NULL"}");
                
                if (template == null)
                {
                    Console.WriteLine("ERROR: Template is null, cannot navigate to customization");
                    // Fallback to template selection if template is null
                    if (currentProduct != null)
                    {
                        NavigateToTemplateSelectionWithCategories(currentProduct);
                    }
                    else
                    {
                        NavigateToProductSelection();
                    }
                    return;
                }
                
                LoggingService.Application.Information("Navigating to template customization with template",
                    ("TemplateName", template.TemplateName ?? "Unknown"),
                    ("Category", template.Category ?? "Unknown"));

                if (templateCustomizationScreen == null)
                {
                    Console.WriteLine("Creating new TemplateCustomizationScreen...");
                    templateCustomizationScreen = new TemplateCustomizationScreen(_databaseService);
                    
                    // Subscribe to events
                    templateCustomizationScreen.BackButtonClicked += TemplateCustomizationScreen_BackButtonClicked;
                    templateCustomizationScreen.TemplateSelected += TemplateCustomizationScreen_TemplateSelected;
                    templateCustomizationScreen.PhotoSessionStartRequested += TemplateCustomizationScreen_PhotoSessionStartRequested;
                    Console.WriteLine("TemplateCustomizationScreen created and events subscribed");
                }
                else
                {
                    Console.WriteLine("Using existing TemplateCustomizationScreen");
                }

                // Convert TemplateInfo to Template for customization screen
                Console.WriteLine("Converting TemplateInfo to Template...");
                var dbTemplate = ConvertTemplateInfoToTemplate(template);
                Console.WriteLine($"Converted template: Name={dbTemplate.Name}, ID={dbTemplate.Id}");
                
                Console.WriteLine("Setting template on customization screen...");
                templateCustomizationScreen.SetTemplate(dbTemplate, currentProduct);

                // Update UI
                Console.WriteLine("Updating CurrentScreenContainer.Content...");
                CurrentScreenContainer.Content = templateCustomizationScreen;
                Console.WriteLine("UI updated successfully");
                
                LoggingService.Application.Information("Template customization screen with template loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION in NavigateToTemplateCustomizationWithTemplate: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                LoggingService.Application.Error("Template customization navigation failed", ex,
                    ("TemplateName", template?.TemplateName ?? "Unknown"));
                System.Diagnostics.Debug.WriteLine($"Template customization navigation failed: {ex.Message}");
                // Fallback to template selection if customization navigation fails
                if (currentProduct != null)
                {
                    NavigateToTemplateSelectionWithCategories(currentProduct);
                }
                else
                {
                    NavigateToProductSelection();
                }
            }
        }

        /// <summary>
        /// Navigate to template customization screen
        /// </summary>
        public async void NavigateToTemplateCustomization(ProductInfo product, TemplateCategory category)
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
                    templateCustomizationScreen = new TemplateCustomizationScreen(_databaseService);
                    
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
                NavigateToCategorySelection(product);
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
        /// Navigate to admin dashboard with specified access level
        /// Called after successful admin login
        /// </summary>
        public async System.Threading.Tasks.Task NavigateToAdminDashboard(AdminAccessLevel accessLevel, string userId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"NavigateToAdminDashboard called: AccessLevel={accessLevel}, UserId={userId}");
                
                if (adminDashboardScreen == null)
                {
                    System.Diagnostics.Debug.WriteLine("Creating new AdminDashboardScreen instance...");
                    adminDashboardScreen = new AdminDashboardScreen(_databaseService);
                    System.Diagnostics.Debug.WriteLine("AdminDashboardScreen created successfully");
                    
                    // Subscribe to admin dashboard events
                    adminDashboardScreen.ExitAdminRequested += AdminDashboardScreen_ExitAdminRequested;
                    System.Diagnostics.Debug.WriteLine("Event handlers attached");
                }

                // Set access level and configure UI accordingly
                currentAdminAccess = accessLevel;
                System.Diagnostics.Debug.WriteLine("Setting access level...");
                await adminDashboardScreen.SetAccessLevel(accessLevel, userId);
                System.Diagnostics.Debug.WriteLine("Access level set successfully");
                
                System.Diagnostics.Debug.WriteLine("Refreshing sales data...");
                adminDashboardScreen.RefreshSalesData();
                System.Diagnostics.Debug.WriteLine("Sales data refreshed");

                System.Diagnostics.Debug.WriteLine("Setting screen content...");
                CurrentScreenContainer.Content = adminDashboardScreen;
                System.Diagnostics.Debug.WriteLine("Admin dashboard navigation completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR - Navigation to admin dashboard failed:");
                System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                // Show error message instead of silently falling back
                try
                {
                    MessageBox.Show($"Failed to load admin dashboard: {ex.Message}\n\nPlease check the debug output for details.", 
                                  "Admin Dashboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    // If MessageBox fails, at least we have debug output
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
                // Skip category selection and go directly to template selection with categorized view
                NavigateToTemplateSelectionWithCategories(e.ProductInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Product selection navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles category selection back button click
        /// </summary>
        private void CategorySelectionScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                NavigateToProductSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Category selection back navigation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles category selection
        /// </summary>
        private void CategorySelectionScreen_CategorySelected(object? sender, CategorySelectedEventArgs e)
        {
            try
            {
                if (e.Product != null)
                {
                    NavigateToTemplateCustomization(e.Product, e.Category);
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
        private void TemplateSelectionScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                // Navigate back to product selection since category selection is skipped
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
                Console.WriteLine("=== MAINWINDOW TEMPLATE SELECTED EVENT ===");
                Console.WriteLine($"Sender: {sender?.GetType().Name}");
                Console.WriteLine($"Template: {e.Template?.TemplateName ?? "NULL"}");
                Console.WriteLine($"Template Category: {e.Template?.Category ?? "NULL"}");
                Console.WriteLine("Calling NavigateToTemplateCustomizationWithTemplate...");
                
                // Navigate to customization screen with the selected template
                if (e.Template != null)
                {
                    NavigateToTemplateCustomizationWithTemplate(e.Template);
                }
                else
                {
                    Console.WriteLine("ERROR: Template is null, cannot navigate to customization");
                    // Fallback to template selection if template is null
                    if (currentProduct != null)
                    {
                        NavigateToTemplateSelectionWithCategories(currentProduct);
                    }
                    else
                    {
                        NavigateToProductSelection();
                    }
                }
                
                Console.WriteLine("NavigateToTemplateCustomizationWithTemplate completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION in TemplateSelectionScreen_TemplateSelected: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Template selection navigation failed: {ex.Message}");
            }
        }



        /// <summary>
        /// Converts TemplateInfo to Template for compatibility
        /// </summary>
        private Template ConvertTemplateInfoToTemplate(TemplateInfo templateInfo)
        {
            return new Template
            {
                Id = Random.Shared.Next(10000, 99999), // Generate a temporary ID
                Name = templateInfo.TemplateName ?? "Unknown Template",
                Description = templateInfo.Description ?? "",
                FilePath = templateInfo.TemplateImagePath ?? "",
                PreviewPath = templateInfo.PreviewImagePath ?? "",
                IsActive = true
            };
        }

        /// <summary>
        /// Handles template customization back button click
        /// </summary>
        private void TemplateCustomizationScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                // Navigate back to template selection if we have current product
                if (currentProduct != null)
                {
                    NavigateToTemplateSelectionWithCategories(currentProduct);
                }
                else
                {
                    // Fallback to product selection if no current product
                    NavigateToProductSelection();
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
        private void TemplateCustomizationScreen_PhotoSessionStartRequested(object? sender, PhotoSessionStartEventArgs e)
        {
            try
            {
                // For now, create a simple capture screen or show a message
                // TODO: Implement actual photo capture screen
                var message = $"Starting Photo Session!\n\n" +
                             $"Template: {e.Template.Name}\n" +
                             $"Photo Count: {e.PhotoCount}\n" +
                             $"Timer: {e.TimerSeconds} seconds\n" +
                             $"Flash: {(e.FlashEnabled ? "Enabled" : "Disabled")}\n\n" +
                             $"Photo capture functionality will be implemented here.";

                NotificationService.Instance.ShowInfo("Photo Session Starting", message, 10);

                // Return to welcome for now until capture screen is implemented
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Photo session start failed: {ex.Message}");
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
                
                // Hide virtual keyboard on application close
                VirtualKeyboardService.Instance.HideKeyboard();
                
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


    }
}
