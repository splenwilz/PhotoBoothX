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
        
        // Image composition service
        private readonly IImageCompositionService _imageCompositionService;

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

                // Clear all notifications when returning to welcome screen
                NotificationService.Instance.ClearAll();

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
                    categorySelectionScreen = new CategorySelectionScreen(_databaseService);
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
                    LoggingService.Application.Debug("Creating new TemplateSelectionScreen");
                    templateSelectionScreen = new TemplateSelectionScreen(_databaseService, _templateConversionService);
                    
                    // Subscribe to events
                    LoggingService.Application.Debug("Subscribing to TemplateSelectionScreen events");
                    templateSelectionScreen.BackButtonClicked += TemplateSelectionScreen_BackButtonClicked;
                    templateSelectionScreen.TemplateSelected += TemplateSelectionScreen_TemplateSelected;

                    LoggingService.Application.Debug("TemplateSelectionScreen events subscribed successfully");
                }
                else
                {
                    LoggingService.Application.Debug("Using existing TemplateSelectionScreen");
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
        /// Navigate to template customization screen with specific template
        /// </summary>
        public async void NavigateToTemplateCustomizationWithTemplate(TemplateInfo? template)
        {
            try
            {
                if (template == null || currentProduct == null)
                {
                    LoggingService.Application.Warning("Attempted to navigate to template customization with null template or product");
                        NavigateToProductSelection();
                    return;
                }
                
                currentTemplate = template;

                LoggingService.Application.Information("Navigating to template customization with specific template",
                    ("TemplateName", template.TemplateName ?? "Unknown"),
                    ("Category", template.Category ?? "Unknown"));

                if (templateCustomizationScreen == null)
                {
                    LoggingService.Application.Debug("Creating new TemplateCustomizationScreen");
                    templateCustomizationScreen = new TemplateCustomizationScreen(_databaseService);
                    
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
                cameraCaptureScreen = new CameraCaptureScreen(_databaseService);
                LoggingService.Application.Debug("New CameraCaptureScreen created");
                
                // Subscribe to camera capture events
                LoggingService.Application.Debug("Subscribing to camera capture events");
                cameraCaptureScreen.BackButtonClicked += CameraCaptureScreen_BackButtonClicked;
                cameraCaptureScreen.PhotosCaptured += CameraCaptureScreen_PhotosCaptured;
                LoggingService.Application.Debug("Camera capture event handlers subscribed");

                // Initialize camera session
                LoggingService.Application.Debug("Starting camera initialization");
                var initialized = cameraCaptureScreen.InitializeSession(template);
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
                    photoPreviewScreen = new PhotoPreviewScreen(_databaseService, _imageCompositionService);
                    
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
                    photoPreviewScreen = new PhotoPreviewScreen(_databaseService, _imageCompositionService);
                    
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

                if (upsellScreen == null)
                {
                    upsellScreen = new UpsellScreen();
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
            List<string> capturedPhotosPaths, int extraCopies, ProductInfo? crossSellProduct, decimal totalAdditionalCost, bool crossSellAccepted)
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

                // TODO: Send to print queue
                // This is where you would integrate with your printer service
                await SimulatePrintingProcess(template, originalProduct, composedImagePath, extraCopies, crossSellProduct);

                // Show printing confirmation and return to welcome
                var message = BuildPrintingMessage(originalProduct, extraCopies, crossSellProduct, totalAdditionalCost);
                NotificationService.Instance.ShowSuccess("Printing Started!", message, 6);

                // Wait a bit then return to welcome
                await Task.Delay(3000);
                NavigateToWelcome();
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
                LoggingService.Application.Debug("Template selected event received",
                    ("Sender", sender?.GetType().Name ?? "NULL"),
                    ("TemplateName", e.Template?.TemplateName ?? "NULL"),
                    ("TemplateCategory", e.Template?.Category ?? "NULL"));
                
                // Navigate to customization screen with the selected template
                if (e.Template != null)
                {
                    NavigateToTemplateCustomizationWithTemplate(e.Template);
                }
                else
                {
                    LoggingService.Application.Warning("Template is null, cannot navigate to customization - falling back");
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
                
                LoggingService.Application.Debug("Template selection navigation completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template selection navigation failed", ex);
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
        private void CameraCaptureScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked back from camera capture");
                
                // Dispose camera resources
                cameraCaptureScreen?.Dispose();
                
                // Navigate back to template customization
                if (currentProduct != null && currentCategory != null)
                {
                    _ = NavigateToTemplateCustomization(currentProduct, currentCategory);
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
        private void PhotoPreviewScreen_BackButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked back from photo preview");
                
                // Navigate back to template customization
                if (currentProduct != null && currentCategory != null)
                {
                    _ = NavigateToTemplateCustomization(currentProduct, currentCategory);
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
                LoggingService.Application.Information("Upsell completed",
                    ("ExtraCopies", e.Result.ExtraCopies),
                    ("CrossSellAccepted", e.Result.CrossSellAccepted),
                    ("TotalAdditionalCost", e.Result.TotalAdditionalCost));

                // Navigate to printing with upsell results
                await NavigateToPrinting(
                    e.Result.OriginalTemplate,
                    e.Result.OriginalProduct,
                    e.Result.ComposedImagePath,
                    e.Result.CapturedPhotosPaths,
                    e.Result.ExtraCopies,
                    e.Result.CrossSellProduct,
                    e.Result.TotalAdditionalCost,
                    e.Result.CrossSellAccepted
                );
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upsell completion handling failed", ex);
                NavigateToWelcome();
            }
        }

        /// <summary>
        /// Handles upsell timeout
        /// </summary>
        private void UpsellScreen_UpsellTimeout(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Warning("Upsell timeout - proceeding to print original order only");
                
                // For timeout, just navigate to welcome since we don't have easy access to the template context
                // In a production system, you might want to store this context or have the upsell screen handle timeouts internally
                NavigateToWelcome();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upsell timeout handling failed", ex);
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

                // Dispose camera and photo preview screens
                cameraCaptureScreen?.Dispose();
                photoPreviewScreen?.Dispose();

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
