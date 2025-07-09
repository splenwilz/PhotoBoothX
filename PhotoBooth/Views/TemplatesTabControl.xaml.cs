using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Photobooth.Models;
using Photobooth.Services;
using Photobooth.Controls;

namespace Photobooth.Views
{
    public partial class TemplatesTabControl : UserControl
    {
        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private readonly TemplateManager _templateManager;
        private ObservableCollection<Template> _allTemplates;
        private ObservableCollection<Template> _filteredTemplates;
        private HashSet<int> _selectedTemplateIds;
        
        // Pagination
        private int _currentPage = 1;
        private int _templatesPerPage = 12;
        private int _totalPages = 0;

        // View state
        private bool _isGridView = true;
        private string _selectedCategory = "All";
        private string _selectedTemplateType = "All";
        private string _sortBy = "database"; // Default to database order (includes seasonal prioritization)
        private string _sortOrder = "asc";
        private bool _showAllSeasons = false; // New field to track seasonal filter bypass

        // Performance optimization fields
        private bool _isDataLoaded = false;
        private DateTime _lastSyncTime = DateTime.MinValue;
        private readonly TimeSpan _syncCooldown = GetCacheTimeout(); // Configurable cache timeout
        private bool _isInitialLoad = true;
        private readonly System.Diagnostics.Stopwatch _performanceStopwatch = new();

        // Modal state fields removed - using CategoryManagementDialog instead

        #endregion

        #region Configuration

        /// <summary>
        /// Get cache timeout from configuration or environment variable, with fallback to default
        /// </summary>
        private static TimeSpan GetCacheTimeout()
        {
            // Allow override via environment variable for testing
            var envTimeout = Environment.GetEnvironmentVariable("PHOTOBOOTH_CACHE_TIMEOUT_MINUTES");
            if (!string.IsNullOrEmpty(envTimeout) && int.TryParse(envTimeout, out var minutes))
            {
                return TimeSpan.FromMinutes(Math.Max(1, minutes)); // Minimum 1 minute
            }
            
            // Default: 5 minutes for production use
            return TimeSpan.FromMinutes(5);
        }

        #endregion

        #region Constructor

        public TemplatesTabControl()
        {
            throw new NotSupportedException("Use the constructor with IDatabaseService parameter for proper dependency injection");
        }

        public TemplatesTabControl(IDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _templateManager = new TemplateManager(_databaseService);
            _allTemplates = new ObservableCollection<Template>();
            _filteredTemplates = new ObservableCollection<Template>();
            _selectedTemplateIds = new HashSet<int>();
            
            InitializeComponent();
            
            // Set default sort selection
            if (SortComboBox.Items.Count > 0)
            {
                SortComboBox.SelectedIndex = 0; // Select "Smart Order"
            }
            
            // Set default page size selection (12 is the default, which is index 2)
            if (PageSizeComboBox.Items.Count > 2)
            {
                PageSizeComboBox.SelectedIndex = 2; // Select "12"
            }
            
            // Load initial data
            Loaded += async (s, e) => {
                await LoadTemplateCategoriesAsync();
                await LoadTemplatesAsync();
                await UpdateSystemDatePreviewAsync();
            };
        }



        #endregion

        #region Event Handlers

        /// <summary>
        /// Optimized template loading for faster tab switching
        /// Only performs synchronization when necessary (cooldown period or forced refresh)
        /// </summary>
        private async Task LoadTemplatesOptimizedAsync(bool forceSync = false)
        {
            _performanceStopwatch.Restart();
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;

                var shouldSync = forceSync || 
                                _isInitialLoad || 
                                !_isDataLoaded || 
                                DateTime.Now - _lastSyncTime > _syncCooldown;

                if (shouldSync)
                {
                    LoggingService.Application.Information("Performing full sync",
                        ("ForceSync", forceSync),
                        ("InitialLoad", _isInitialLoad),
                        ("DataLoaded", _isDataLoaded),
                        ("LastSync", _lastSyncTime),
                        ("CacheTimeoutMinutes", _syncCooldown.TotalMinutes));
                    
                    // Step 1: Synchronize database with file system (file system is source of truth)
                    var syncStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await SynchronizeTemplatesWithFileSystemAsync();
                    syncStopwatch.Stop();
                    LoggingService.Application.Information("File system sync completed",
                        ("SyncTimeMs", syncStopwatch.ElapsedMilliseconds));
                    
                    _lastSyncTime = DateTime.Now;
                    _isInitialLoad = false;
                }
                else
                {
                    LoggingService.Application.Information("Skipping sync - using cached data",
                        ("LastSyncMinutesAgo", (DateTime.Now - _lastSyncTime).TotalMinutes));
                }

                // Step 2: Load all templates from database (always fresh for accurate data)
                var result = await _databaseService.GetAllTemplatesAsync(_showAllSeasons);
                
                if (result.Success && result.Data != null)
                {
                    _allTemplates.Clear();
                    foreach (var template in result.Data)
                    {
                        _allTemplates.Add(template);
                    }
                    _isDataLoaded = true;
                }
                else
                {
                    _allTemplates.Clear();
                }

                // Step 3: Apply filtering and display
                FilterAndSortTemplates();
                UpdateTemplateCount();
                
                _performanceStopwatch.Stop();
                LoggingService.Application.Information("Template loading completed",
                    ("LoadingTimeMs", _performanceStopwatch.ElapsedMilliseconds),
                    ("SyncPerformed", shouldSync ? "Yes" : "No"));
            }
            catch (Exception ex)
            {
                _performanceStopwatch.Stop();
                LoggingService.Application.Error("Error in optimized template loading", ex,
                    ("LoadingTimeMs", _performanceStopwatch.ElapsedMilliseconds));
                MessageBox.Show($"Error loading templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _allTemplates.Clear();
                EmptyStatePanel.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Original full template loading method - forces synchronization
        /// Use this for explicit refresh operations
        /// </summary>
        private async Task LoadTemplatesAsync()
        {
            await LoadTemplatesOptimizedAsync(forceSync: true);
        }

        /// <summary>
        /// Clear cached data to force fresh loading on next access
        /// Use when file system changes occur (upload, delete, etc.)
        /// </summary>
        private void InvalidateCache()
        {
            _isDataLoaded = false;
            _lastSyncTime = DateTime.MinValue;
            LoggingService.Application.Information("Template cache invalidated - next load will perform full sync");
        }

        private async void UploadTemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== UPLOAD TEMPLATES BUTTON CLICKED ===");

            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                
                // Show the new layout-based upload dialog
                var parentWindow = Window.GetWindow(this);
                var (success, uploadResult) = await TemplateUploadDialog.ShowUploadDialogAsync(
                    _databaseService, 
                    _templateManager, 
                    parentWindow);
                
                if (success && uploadResult != null)
                {
                // Show results
                ShowUploadResults(uploadResult);
                
                // Refresh templates if any were uploaded successfully
                if (uploadResult.SuccessCount > 0)
                {
                    InvalidateCache(); // Force fresh sync since files were added
                    await LoadTemplatesAsync();
                }
                }
                // If not success, user cancelled - no need to show anything
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Upload failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Null checks for required parameters
                if (_databaseService == null)
                {
                    NotificationService.Instance.ShowError("Export Error", "Database service is not available. Please try refreshing the page.");
                    return;
                }
                
                if (_allTemplates == null)
                {
                    NotificationService.Instance.ShowError("Export Error", "Template data is not available. Please try refreshing the templates.");
                    return;
                }
                
                if (_selectedTemplateIds == null)
                {
                    NotificationService.Instance.ShowError("Export Error", "Selection data is not available. Please try refreshing the page.");
                    return;
                }
                
                // Show the export dialog
                var parentWindow = Window.GetWindow(this);
                
                var exportCompleted = await Controls.TemplateExportDialog.ShowExportDialogAsync(
                    parentWindow, 
                    _databaseService, 
                    _allTemplates.ToList(), // Convert ObservableCollection to List
                    _selectedTemplateIds);
                
                // The export dialog handles all success/error notifications internally
                if (exportCompleted)
                {
                    // Optionally refresh the template list or perform any cleanup
                    System.Diagnostics.Debug.WriteLine("Template export completed successfully");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error opening export dialog", ex);
                NotificationService.Instance.ShowError("Export Error", "An error occurred while opening the export dialog. Please try again.");
            }
        }

        private async void BulkEnableButton_Click(object sender, RoutedEventArgs e)
        {
            await BulkUpdateTemplateStatusAsync(true);
        }

        private async void BulkDisableButton_Click(object sender, RoutedEventArgs e)
        {
            await BulkUpdateTemplateStatusAsync(false);
        }

        private async void BulkDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplateIds.Count == 0) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete {_selectedTemplateIds.Count} selected templates? This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await BulkDeleteTemplatesAsync();
            }
        }

        private async void BulkAssignCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplateIds.Count == 0) return;

            var selectedCategory = BulkCategoryComboBox.SelectedItem as TemplateCategory;
            if (selectedCategory == null)
            {
                NotificationService.Instance.ShowWarning("No Category Selected", "Please select a category to assign.");
                return;
            }

            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                
                // Store the count before clearing the selection
                var selectedCount = _selectedTemplateIds.Count;
                var selectedTemplateNames = _allTemplates
                    .Where(t => _selectedTemplateIds.Contains(t.Id))
                    .Select(t => t.Name)
                    .ToList();
                
                var result = await _databaseService.BulkUpdateTemplateCategoryAsync(_selectedTemplateIds.ToList(), selectedCategory.Id);
                
                if (result.Success)
                {
                    // Update local template objects
                    foreach (var templateId in _selectedTemplateIds)
                    {
                        var template = _allTemplates.FirstOrDefault(t => t.Id == templateId);
                        if (template != null)
                        {
                            template.CategoryId = selectedCategory.Id;
                            template.CategoryName = selectedCategory.Name;
                            template.Category = selectedCategory;
                        }
                    }

                    _selectedTemplateIds.Clear();
                    RefreshTemplateDisplay();
                    UpdateBulkActionsVisibility();
                    
                    // Show success notification toast with correct count
                    NotificationService.Instance.ShowSuccess(
                        "Category Assignment Complete", 
                        $"Successfully assigned {selectedCount} template{(selectedCount == 1 ? "" : "s")} to '{selectedCategory.Name}' category."
                    );
                }
                else
                {
                    NotificationService.Instance.ShowError("Assignment Failed", $"Failed to assign templates: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                NotificationService.Instance.ShowError("Assignment Error", $"Error assigning templates: {ex.Message}");
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }



        private void ClearAllFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset all filters to default
            if (CategoryFilterComboBox.Items.Count > 0) CategoryFilterComboBox.SelectedIndex = 0; // Select "All"
            if (TemplateTypeFilterComboBox.Items.Count > 0) TemplateTypeFilterComboBox.SelectedIndex = 0; // Select "All Types"
            if (SortComboBox.Items.Count > 0) SortComboBox.SelectedIndex = 0; // Select "Smart Order"
            ShowAllSeasonsToggle.IsChecked = false;
            
            // This will trigger the filter refresh through the changed events
        }

        private void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== CategoryFilterComboBox_SelectionChanged triggered ===");
            
            // Ignore events during initialization
            if (_allTemplates == null || _filteredTemplates == null)
                return;
                
            if (CategoryFilterComboBox.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                var newCategory = item.Content.ToString() ?? "All";
                System.Diagnostics.Debug.WriteLine($"Category changed from '{_selectedCategory}' to '{newCategory}'");
                
                // Debug: Show available templates and their categories
                var templateCategories = _allTemplates.Select(t => t.CategoryName ?? "NULL").Distinct().ToList();
                System.Diagnostics.Debug.WriteLine($"Available template categories: {string.Join(", ", templateCategories)}");
                
                _selectedCategory = newCategory;
                FilterAndSortTemplates();
                UpdateActiveFiltersDisplay();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CategoryFilterComboBox: No valid item selected");
            }
        }

        private void TemplateTypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== TemplateTypeFilterComboBox_SelectionChanged triggered ===");
            
            // Ignore events during initialization
            if (_allTemplates == null || _filteredTemplates == null)
                return;
                
            if (TemplateTypeFilterComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var newTemplateType = item.Tag.ToString() ?? "All";
                System.Diagnostics.Debug.WriteLine($"Template type changed from '{_selectedTemplateType}' to '{newTemplateType}'");
                
                _selectedTemplateType = newTemplateType;
                FilterAndSortTemplates();
                UpdateActiveFiltersDisplay();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("TemplateTypeFilterComboBox: No valid item selected");
            }
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== SortComboBox_SelectionChanged triggered ===");
            
            // Ignore events during initialization
            if (_allTemplates == null || _filteredTemplates == null)
                return;
                
            if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var parts = item.Tag.ToString()?.Split('-');
                if (parts != null && parts.Length >= 2)
                {
                    var newSortBy = parts[0];
                    var newSortOrder = parts[1];
                    System.Diagnostics.Debug.WriteLine($"Sort changed from '{_sortBy}-{_sortOrder}' to '{newSortBy}-{newSortOrder}'");
                    _sortBy = newSortBy;
                    _sortOrder = newSortOrder;
                    FilterAndSortTemplates();
                    UpdateActiveFiltersDisplay();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SortComboBox: No valid item selected");
            }
        }

        private void ViewModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            _isGridView = false;
            ViewModeIcon.Text = "☰"; // List view icon
            GridViewContainer.Visibility = Visibility.Collapsed;
            ListViewContainer.Visibility = Visibility.Visible;
            RefreshTemplateDisplay();
        }

        private void ViewModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _isGridView = true;
            ViewModeIcon.Text = "⊞"; // Grid view icon
            GridViewContainer.Visibility = Visibility.Visible;
            ListViewContainer.Visibility = Visibility.Collapsed;
            RefreshTemplateDisplay();
        }

        private async void ShowAllSeasonsToggle_Checked(object sender, RoutedEventArgs e)
        {
            _showAllSeasons = true;
            System.Diagnostics.Debug.WriteLine("Show All Seasons enabled - showing all seasonal templates regardless of date");
            
            // Reload categories to include out-of-season categories
            await LoadTemplateCategoriesAsync();
            
            // Reload templates with all seasons visible
            await LoadTemplatesAsync();
            
            UpdateActiveFiltersDisplay();
        }

        private async void ShowAllSeasonsToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _showAllSeasons = false;
            System.Diagnostics.Debug.WriteLine("Show All Seasons disabled - filtering templates by current season");
            
            // Reload categories with seasonal filtering
            await LoadTemplateCategoriesAsync();
            
            // Reload templates with seasonal filtering
            await LoadTemplatesAsync();
            
            UpdateActiveFiltersDisplay();
        }

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var pageTemplates = GetCurrentPageTemplates();
            foreach (var template in pageTemplates)
            {
                _selectedTemplateIds.Add(template.Id);
            }
            RefreshTemplateDisplay();
            UpdateBulkActionsVisibility();
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var pageTemplates = GetCurrentPageTemplates();
            foreach (var template in pageTemplates)
            {
                _selectedTemplateIds.Remove(template.Id);
            }
            RefreshTemplateDisplay();
            UpdateBulkActionsVisibility();
        }

        private void FirstPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage = 1;
                RefreshTemplateDisplay();
                UpdatePagination();
            }
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                RefreshTemplateDisplay();
                UpdatePagination();
            }
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                RefreshTemplateDisplay();
                UpdatePagination();
            }
        }

        private void LastPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage = _totalPages;
                RefreshTemplateDisplay();
                UpdatePagination();
            }
        }



        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore events during initialization
            if (_allTemplates == null || _filteredTemplates == null)
                return;
                
            if (PageSizeComboBox.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                var contentString = item.Content.ToString();
                if (int.TryParse(contentString, out var pageSize))
                {
                    _templatesPerPage = pageSize;
                    _currentPage = 1;
                    FilterAndSortTemplates();
                }
            }
        }

        private void AddNewTemplate_Click(object sender, MouseButtonEventArgs e)
        {
            UploadTemplatesButton_Click(sender, new RoutedEventArgs());
        }

        private async void RefreshTemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                await LoadTemplatesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void SystemDateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var parentWindow = Window.GetWindow(this);
                
                // Create callback to handle "Show All Seasons" action from the dialog
                Action<bool> showAllSeasonsCallback = (enableAllSeasons) =>
                {
                    if (enableAllSeasons)
                    {
                        ShowAllSeasonsToggle.IsChecked = true;
                        // The toggle event handler will handle the actual functionality
                    }
                };
                
                // Show the modern system date dialog
                await PhotoBooth.Controls.SystemDateDialog.ShowSystemDateDialogAsync(parentWindow, _databaseService, showAllSeasonsCallback);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing system date information: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Show synchronization results to the user
        /// </summary>
        private void ShowSynchronizationResults(TemplateUploadResult result)
        {
            var icon = result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning;
            var title = result.Success ? "Templates Synchronized" : "Synchronization Issues";
            
            var message = result.Message;
            
            // Add details if there were validation errors
            if (result.Results.Any(r => !r.IsValid))
            {
                var errorTemplates = result.Results.Where(r => !r.IsValid).ToList();
                if (errorTemplates.Count > 0)
                {
                    message += "\n\nTemplates with issues:";
                    foreach (var errorTemplate in errorTemplates.Take(5)) // Show max 5 to avoid long messages
                    {
                        var templateName = Path.GetFileName(errorTemplate.Template?.FolderPath ?? "Unknown");
                        message += $"\n• {templateName}: {string.Join(", ", errorTemplate.Errors)}";
                    }
                    
                    if (errorTemplates.Count > 5)
                    {
                        message += $"\n• ... and {errorTemplates.Count - 5} more";
                    }
                }
            }
            
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        /// <summary>
        /// Debug method to force synchronization and show detailed info
        /// </summary>
        public async Task DebugSynchronizationAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== STARTING DEBUG SYNCHRONIZATION ===");
                
                LoadingPanel.Visibility = Visibility.Visible;
                
                // Get current database count
                var dbResult = await _databaseService.GetAllTemplatesAsync();
                var dbCount = dbResult.Success && dbResult.Data != null ? dbResult.Data.Count() : 0;
                System.Diagnostics.Debug.WriteLine($"Templates in database before sync: {dbCount}");
                
                // Check templates directory
                var templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                var folderCount = Directory.Exists(templatesDir) ? Directory.GetDirectories(templatesDir).Length : 0;
                System.Diagnostics.Debug.WriteLine($"Template folders on disk: {folderCount}");
                
                // Run synchronization
                var result = await _templateManager.SynchronizeWithFileSystemAsync();
                
                // Get database count after sync
                var dbResultAfter = await _databaseService.GetAllTemplatesAsync();
                var dbCountAfter = dbResultAfter.Success && dbResultAfter.Data != null ? dbResultAfter.Data.Count() : 0;
                System.Diagnostics.Debug.WriteLine($"Templates in database after sync: {dbCountAfter}");
                
                System.Diagnostics.Debug.WriteLine("=== DEBUG SYNCHRONIZATION COMPLETE ===");
                
                // Show results
                ShowSynchronizationResults(result);
                
                // Reload templates
                await LoadTemplatesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Debug sync error: {ex.Message}");
                MessageBox.Show($"Debug synchronization failed: {ex.Message}", "Debug Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Debug method to check current state without running sync
        /// </summary>
        public async Task CheckCurrentStateAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== CHECKING CURRENT STATE ===");
                
                // Check templates directory
                var templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                System.Diagnostics.Debug.WriteLine($"Templates directory: {templatesDir}");
                System.Diagnostics.Debug.WriteLine($"Directory exists: {Directory.Exists(templatesDir)}");
                
                if (Directory.Exists(templatesDir))
                {
                    var folders = Directory.GetDirectories(templatesDir);
                    System.Diagnostics.Debug.WriteLine($"Template folders found: {folders.Length}");
                    foreach (var folder in folders)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {Path.GetFileName(folder)}");
                    }
                }
                
                // Check database
                var dbResult = await _databaseService.GetAllTemplatesAsync();
                if (dbResult.Success && dbResult.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Templates in database: {dbResult.Data.Count()}");
                    foreach (var template in dbResult.Data)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {template.Name} (ID: {template.Id})");
                        System.Diagnostics.Debug.WriteLine($"    FolderPath: {template.FolderPath}");
                        System.Diagnostics.Debug.WriteLine($"    Folder exists: {Directory.Exists(template.FolderPath)}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get templates from database: {dbResult.ErrorMessage}");
                }
                
                System.Diagnostics.Debug.WriteLine("=== END CURRENT STATE CHECK ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"State check error: {ex.Message}");
            }
        }

        #endregion

        #region Template Management

        /// <summary>
        /// Synchronize database with file system - treats file system as source of truth
        /// </summary>
        private async Task SynchronizeTemplatesWithFileSystemAsync()
        {
            try
            {
                var result = await _templateManager.SynchronizeWithFileSystemAsync();
                
                if (!result.Success || result.FailureCount > 0)
                {
                    // Only show message if there were actual errors (not just cleanup)
                    if (!string.IsNullOrEmpty(result.Message) && result.FailureCount > 0)
                    {
                        var shouldShowMessage = result.Results.Any(r => !r.IsValid && r.Errors.Count > 0);
                        if (shouldShowMessage)
                        {
                            MessageBox.Show($"Template synchronization completed with some issues:\n{result.Message}", 
                                          "Template Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch
            {
                // Don't block UI loading for sync errors
            }
        }

        private async Task<TemplateUploadResult> UploadFromFoldersAsync()
        {
            // Use WPF folder dialog instead of WinForms
            var dialog = new OpenFolderDialog
            {
                Title = "Select Template Folder",
                Multiselect = false
            };
            
            if (dialog.ShowDialog() != true)
            {
                return new TemplateUploadResult { Message = "Upload cancelled" };
            }
            
            return await _templateManager.UploadFromFoldersAsync(new[] { dialog.FolderName });
        }

        private async Task<TemplateUploadResult> UploadFromZipAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Template ZIP File",
                Filter = "ZIP Files|*.zip|All Files|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return new TemplateUploadResult { Message = "Upload cancelled" };
            }
            
            return await _templateManager.UploadFromZipAsync(openFileDialog.FileName);
        }

        private void ShowUploadResults(TemplateUploadResult result)
        {
            // Use the new custom upload results dialog
            Photobooth.Controls.UploadResultsDialog.ShowResults(result, Application.Current.MainWindow);
        }

        private async Task BulkUpdateTemplateStatusAsync(bool isActive)
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                
                // Store the count before clearing
                var updatedCount = _selectedTemplateIds.Count;
                
                foreach (var templateId in _selectedTemplateIds)
                {
                    var template = _allTemplates.FirstOrDefault(t => t.Id == templateId);
                    if (template != null)
                    {
                        await _databaseService.UpdateTemplateAsync(templateId, isActive: isActive);
                        template.IsActive = isActive;
                    }
                }

                _selectedTemplateIds.Clear();
                RefreshTemplateDisplay();
                UpdateBulkActionsVisibility();
                
                MessageBox.Show($"Successfully updated {updatedCount} templates.", 
                              "Bulk Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating templates: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task BulkDeleteTemplatesAsync()
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                
                var deleteCount = 0;
                var failureCount = 0;
                
                foreach (var templateId in _selectedTemplateIds.ToList()) // ToList() to avoid collection modification
                {
                    var deleteResult = await _templateManager.DeleteTemplateCompletelyAsync(templateId);
                    if (deleteResult.IsValid)
                    {
                        deleteCount++;
                    }
                    else
                    {
                        failureCount++;
                        var errorMessage = deleteResult.Errors.Count > 0 ? string.Join(", ", deleteResult.Errors) : "Unknown error";
                        System.Diagnostics.Debug.WriteLine($"Failed to delete template ID {templateId}: {errorMessage}");
                    }
                }

                _selectedTemplateIds.Clear();
                InvalidateCache(); // Force fresh sync since templates were deleted
                await LoadTemplatesAsync();
                UpdateBulkActionsVisibility();
                
                // Show results using notification toasts
                if (failureCount == 0)
                {
                    NotificationService.Instance.ShowSuccess("Bulk Delete Complete", $"Successfully deleted {deleteCount} templates.");
                }
                else if (deleteCount > 0)
                {
                    NotificationService.Instance.ShowWarning("Bulk Delete Partial", $"Deleted {deleteCount} templates successfully. {failureCount} templates failed to delete.");
                }
                else
                {
                    NotificationService.Instance.ShowError("Bulk Delete Failed", $"Failed to delete all {failureCount} selected templates.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting templates: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task ToggleTemplateStatusAsync(int templateId)
        {
            try
            {
                var template = _allTemplates.FirstOrDefault(t => t.Id == templateId);
                if (template != null)
                {
                    var newStatus = !template.IsActive;
                    var result = await _databaseService.UpdateTemplateAsync(templateId, isActive: newStatus);
                    
                    if (result.Success)
                    {
                        template.IsActive = newStatus;
                        RefreshTemplateDisplay();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to update template: {result.ErrorMessage}", 
                                      "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating template: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Filtering and Sorting

        private void FilterAndSortTemplates()
        {
            var filtered = _allTemplates.AsEnumerable();

            // Don't filter by IsActive - show all templates but disabled ones will be visually grayed out



            // Apply category filter
            if (_selectedCategory != "All Categories" && _selectedCategory != "All")
            {
                System.Diagnostics.Debug.WriteLine($"Filtering by category: '{_selectedCategory}'");
                var beforeCount = filtered.Count();
                filtered = filtered.Where(t => t.CategoryName == _selectedCategory);
                var afterCount = filtered.Count();
                System.Diagnostics.Debug.WriteLine($"Templates before filter: {beforeCount}, after filter: {afterCount}");
            }

            // Apply template type filter
            if (_selectedTemplateType != "All")
            {
                System.Diagnostics.Debug.WriteLine($"Filtering by template type: '{_selectedTemplateType}'");
                var beforeCount = filtered.Count();
                filtered = filtered.Where(t => t.TemplateType.ToString() == _selectedTemplateType);
                var afterCount = filtered.Count();
                System.Diagnostics.Debug.WriteLine($"Templates before type filter: {beforeCount}, after type filter: {afterCount}");
            }

            // Apply sorting - PRESERVE DATABASE ORDER when no specific sort is applied
            // Database order includes seasonal prioritization, so only override when user explicitly sorts
            if (!string.IsNullOrEmpty(_sortBy) && _sortBy != "database")
            {
            filtered = _sortBy switch
            {
                "name" => _sortOrder == "asc" ? filtered.OrderBy(t => t.Name) : filtered.OrderByDescending(t => t.Name),
                "date" => _sortOrder == "asc" ? filtered.OrderBy(t => t.UploadedAt) : filtered.OrderByDescending(t => t.UploadedAt),
                "category" => _sortOrder == "asc" ? filtered.OrderBy(t => t.CategoryId) : filtered.OrderByDescending(t => t.CategoryId),
                "price" => _sortOrder == "asc" ? filtered.OrderBy(t => t.Price) : filtered.OrderByDescending(t => t.Price),
                    _ => filtered // Preserve original database order (with seasonal prioritization)
            };
            }
            // If _sortBy is empty, null, or "database", preserve the original database order

            _filteredTemplates.Clear();
            foreach (var template in filtered)
            {
                _filteredTemplates.Add(template);
            }

            _currentPage = 1;
            CalculatePagination();
            RefreshTemplateDisplay();
            UpdatePagination();
        }

        #endregion

        #region UI Updates

        private void RefreshTemplateDisplay()
        {
            if (_filteredTemplates.Count == 0)
            {
                // Clear existing templates before showing empty state
                TemplatesUniformGrid.Children.Clear();
                TemplatesListPanel.Children.Clear();
                
                EmptyStatePanel.Visibility = Visibility.Visible;
                SelectAllSection.Visibility = Visibility.Collapsed;
                PaginationSection.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            SelectAllSection.Visibility = Visibility.Visible;
            PaginationSection.Visibility = Visibility.Visible;

            var pageTemplates = GetCurrentPageTemplates();

            if (_isGridView)
            {
                DisplayGridView(pageTemplates);
            }
            else
            {
                DisplayListView(pageTemplates);
            }

            UpdateTemplateCount();
            UpdateSelectAllState();
        }

        private void DisplayGridView(List<Template> templates)
        {
            TemplatesUniformGrid.Children.Clear();

            // Calculate responsive columns based on available width
            var availableWidth = this.ActualWidth > 0 ? this.ActualWidth - 100 : 1200; // fallback width
            var cardWidth = 280; // minimum card width
            var columns = Math.Max(1, (int)(availableWidth / cardWidth));
            TemplatesUniformGrid.Columns = columns;

            foreach (var template in templates)
            {
                var templateCard = CreateTemplateCard(template);
                TemplatesUniformGrid.Children.Add(templateCard);
            }

            // Add the "Add New Template" card at the end
            var addCard = CreateAddNewTemplateCard();
            TemplatesUniformGrid.Children.Add(addCard);
        }

        private void DisplayListView(List<Template> templates)
        {
            TemplatesListPanel.Children.Clear();

            foreach (var template in templates)
            {
                var templateListItem = CreateTemplateListItem(template);
                TemplatesListPanel.Children.Add(templateListItem);
            }
        }

        private Border CreateTemplateCard(Template template)
        {
            var card = new Border
            {
                Style = (Style)FindResource("TemplateCardStyle"),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Remove card-level opacity - we'll apply effects only to the image area

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Auto height to accommodate full preview
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Template preview container - this will hold both the image and overlay
            var previewContainer = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                ClipToBounds = true,
                MinHeight = 300,
                MaxHeight = 500,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var previewBorder = new Border
            {
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = previewContainer
            };

            var previewImage = new Image
            {
                Source = LoadTemplateImage(template.PreviewPath),
                Stretch = Stretch.UniformToFill, // Fill full width and height while maintaining aspect ratio
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Cursor = template.IsActive ? Cursors.Hand : Cursors.Arrow // Only show hand cursor for active templates
            };

            // Apply visual effects only to the image for inactive templates
            if (!template.IsActive)
            {
                previewImage.Opacity = 0.4; // Make the image semi-transparent
            }

            previewContainer.Children.Add(previewImage);

            // Add "DISABLED" overlay only to the image area for inactive templates
            if (!template.IsActive)
            {
                var disabledOverlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), // Semi-transparent black overlay
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = new TextBlock
                    {
                        Text = "DISABLED",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.Black,
                            Direction = 270,
                            ShadowDepth = 2,
                            BlurRadius = 4,
                            Opacity = 0.8
                        }
                    }
                };
                previewContainer.Children.Add(disabledOverlay);
            }

            Grid.SetRow(previewBorder, 0);

            // Template info
            var infoPanel = new StackPanel
            {
                Margin = new Thickness(16)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = template.Name,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)), // Keep normal color for readability
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 16
            };
            Grid.SetColumn(nameText, 0);

            var categoryBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(243, 244, 246)), // Keep normal styling
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)), // Keep normal styling
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock
                {
                    Text = template.CategoryName ?? "Classic",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)) // Keep normal color for readability
                }
            };
            Grid.SetColumn(categoryBadge, 1);

            // Template Type Badge
            var templateTypeBadge = new Border
            {
                Background = template.TemplateType == TemplateType.Photo4x6 ? 
                    new SolidColorBrush(Color.FromRgb(219, 234, 254)) : // Blue for 4x6
                    new SolidColorBrush(Color.FromRgb(254, 243, 199)), // Yellow for Strip
                BorderBrush = template.TemplateType == TemplateType.Photo4x6 ? 
                    new SolidColorBrush(Color.FromRgb(59, 130, 246)) : // Blue border for 4x6
                    new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Yellow border for Strip
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Child = new TextBlock
                {
                    Text = template.TemplateType == TemplateType.Photo4x6 ? "4x6" : "Strip",
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = template.TemplateType == TemplateType.Photo4x6 ? 
                        new SolidColorBrush(Color.FromRgb(30, 58, 138)) : // Blue text for 4x6
                        new SolidColorBrush(Color.FromRgb(146, 64, 14)) // Yellow text for Strip
                }
            };
            Grid.SetColumn(templateTypeBadge, 2);

            headerGrid.Children.Add(nameText);
            headerGrid.Children.Add(categoryBadge);
            headerGrid.Children.Add(templateTypeBadge);

            var detailsGrid = new Grid { Margin = new Thickness(0, 8, 0, 12) };
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var priceText = new TextBlock
            {
                Text = $"${template.Price:F2}",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)) // Keep normal green color
            };
            Grid.SetColumn(priceText, 0);

            var sizeText = new TextBlock
            {
                Text = FormatFileSize(template.FileSize),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Keep normal color
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(sizeText, 1);

            detailsGrid.Children.Add(priceText);
            detailsGrid.Children.Add(sizeText);

            var actionsGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Selection checkbox
            var checkBox = new CheckBox
            {
                IsChecked = _selectedTemplateIds.Contains(template.Id),
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = template.IsActive // Disable checkbox for inactive templates
            };
            checkBox.Checked += (s, e) => TemplateCheckBox_Changed(template.Id, true);
            checkBox.Unchecked += (s, e) => TemplateCheckBox_Changed(template.Id, false);
            Grid.SetColumn(checkBox, 0);
            
            // Make preview image clickable to toggle selection only for active templates
            if (template.IsActive)
            {
                previewImage.MouseLeftButtonDown += (s, e) => 
                {
                    checkBox.IsChecked = !checkBox.IsChecked;
                    e.Handled = true; // Prevent event bubbling
                };
            }

            // Status label
            var statusLabel = new TextBlock
            {
                Text = template.IsActive ? "Enabled" : "Disabled",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = template.IsActive ? 
                    new SolidColorBrush(Color.FromRgb(34, 197, 94)) : 
                    new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(statusLabel, 1);

            // Modern toggle switch
            var statusToggle = new ToggleButton
            {
                Style = (Style)FindResource("ModernToggleSwitchStyle"),
                IsChecked = template.IsActive,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = template.IsActive ? "Click to disable" : "Click to enable"
            };
            statusToggle.Click += async (s, e) => {
                await ToggleTemplateStatusAsync(template.Id);
                // Update the status label
                statusLabel.Text = statusToggle.IsChecked == true ? "Enabled" : "Disabled";
                statusLabel.Foreground = statusToggle.IsChecked == true ? 
                    new SolidColorBrush(Color.FromRgb(34, 197, 94)) : 
                    new SolidColorBrush(Color.FromRgb(107, 114, 128));
            };
            Grid.SetColumn(statusToggle, 2);

            // Hamburger menu button (replacing right-click context menu)
            var hamburgerButton = new Button
            {
                Content = "⋯", // Three dots (hamburger menu)
                Width = 32,
                Height = 32,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), // Subtle gray color
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Template actions",
                Margin = new Thickness(8, 0, 0, 0) // Add some spacing from toggle
            };
            
            // Create simple transparent button style with hover effect
            var buttonTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.Name = "ButtonBorder";
            
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            
            buttonTemplate.VisualTree = borderFactory;
            
            // Add hover triggers for subtle feedback
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(75, 85, 99)), "ButtonBorder"));
            buttonTemplate.Triggers.Add(hoverTrigger);
            hamburgerButton.Template = buttonTemplate;
            
            hamburgerButton.Click += (s, e) => ShowTemplateActionsDialog(template);
            Grid.SetColumn(hamburgerButton, 3);

            actionsGrid.Children.Add(checkBox);
            actionsGrid.Children.Add(statusLabel);
            actionsGrid.Children.Add(statusToggle);
            actionsGrid.Children.Add(hamburgerButton);

            infoPanel.Children.Add(headerGrid);
            infoPanel.Children.Add(detailsGrid);
            infoPanel.Children.Add(actionsGrid);

            Grid.SetRow(infoPanel, 1);

            grid.Children.Add(previewBorder);
            grid.Children.Add(infoPanel);


            card.Child = grid;
            return card;
        }

        private Border CreateTemplateListItem(Template template)
        {
            var item = new Border
            {
                Style = (Style)FindResource("TemplateCardStyle"),
                Padding = new Thickness(16),
                Margin = new Thickness(4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkBox = new CheckBox
            {
                IsChecked = _selectedTemplateIds.Contains(template.Id),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0),
                IsEnabled = template.IsActive // Disable checkbox for inactive templates
            };
            checkBox.Checked += (s, e) => TemplateCheckBox_Changed(template.Id, true);
            checkBox.Unchecked += (s, e) => TemplateCheckBox_Changed(template.Id, false);
            Grid.SetColumn(checkBox, 0);

            var previewImage = new Image
            {
                Source = LoadTemplateImage(template.PreviewPath),
                Width = 48,
                Height = 72,
                Stretch = Stretch.Uniform, // Fill thumbnail while maintaining aspect ratio
                Margin = new Thickness(0, 0, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand // Show hand cursor to indicate clickability
            };
            Grid.SetColumn(previewImage, 1);
            
            // Make preview image clickable to toggle selection only for active templates
            if (template.IsActive)
            {
                previewImage.MouseLeftButtonDown += (s, e) => 
                {
                    checkBox.IsChecked = !checkBox.IsChecked;
                    e.Handled = true; // Prevent event bubbling
                };
            }

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            namePanel.Children.Add(new TextBlock 
            { 
                Text = template.Name, 
                FontWeight = FontWeights.Medium, 
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)), // Keep normal color for readability
                Margin = new Thickness(0, 0, 8, 0)
            });
            
            var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            detailsPanel.Children.Add(new TextBlock 
            { 
                Text = $"Price: ${template.Price:F2}", 
                FontSize = 14, 
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Keep normal green color
                Margin = new Thickness(0, 0, 16, 0)
            });
            detailsPanel.Children.Add(new TextBlock 
            { 
                Text = $"Size: {FormatFileSize(template.FileSize)}", 
                FontSize = 14, 
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Keep normal color
                Margin = new Thickness(0, 0, 16, 0)
            });
            detailsPanel.Children.Add(new TextBlock 
            { 
                Text = $"Uploaded: {template.UploadedAt:MMM dd, yyyy}", 
                FontSize = 14, 
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });

            infoPanel.Children.Add(namePanel);
            infoPanel.Children.Add(detailsPanel);
            Grid.SetColumn(infoPanel, 2);

            var statusToggle = new ToggleButton
            {
                Style = (Style)FindResource("ModernToggleSwitchStyle"),
                IsChecked = template.IsActive,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = template.IsActive ? "Click to disable" : "Click to enable"
            };
            statusToggle.Click += async (s, e) => await ToggleTemplateStatusAsync(template.Id);
            Grid.SetColumn(statusToggle, 3);

            // Hamburger menu button for list view (replacing right-click context menu)
            var hamburgerButtonList = new Button
            {
                Content = "⋯", // Three dots (hamburger menu)
                Width = 32,
                Height = 32,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), // Subtle gray color
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Template actions",
                Margin = new Thickness(12, 0, 0, 0) // More spacing in list view
            };
            
            // Create simple transparent button style for list view with hover effect
            var buttonTemplateList = new ControlTemplate(typeof(Button));
            var borderFactoryList = new FrameworkElementFactory(typeof(Border));
            borderFactoryList.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactoryList.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            borderFactoryList.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            borderFactoryList.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactoryList.Name = "ButtonBorderList";
            
            var contentFactoryList = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactoryList.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactoryList.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactoryList.AppendChild(contentFactoryList);
            
            buttonTemplateList.VisualTree = borderFactoryList;
            
            // Add hover triggers for subtle feedback in list view
            var hoverTriggerList = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTriggerList.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(75, 85, 99)), "ButtonBorderList"));
            buttonTemplateList.Triggers.Add(hoverTriggerList);
            hamburgerButtonList.Template = buttonTemplateList;
            
            hamburgerButtonList.Click += (s, e) => ShowTemplateActionsDialog(template);
            Grid.SetColumn(hamburgerButtonList, 4);

            grid.Children.Add(checkBox);
            grid.Children.Add(previewImage);
            grid.Children.Add(infoPanel);
            grid.Children.Add(statusToggle);
            grid.Children.Add(hamburgerButtonList);


            item.Child = grid;
            return item;
        }

        private Border CreateAddNewTemplateCard()
        {
            var card = new Border
            {
                Style = (Style)FindResource("TemplateCardStyle"),
                Margin = new Thickness(8),
                MinHeight = 380,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Cursor = Cursors.Hand
            };

            card.MouseLeftButtonUp += AddNewTemplate_Click;

            var panel = new StackPanel 
            { 
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(224, 231, 255)),
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16),
                Child = new TextBlock
                {
                    Text = "➕",
                    FontSize = 24,
                    Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var titleText = new TextBlock
            {
                Text = "Add New Template",
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var descText = new TextBlock
            {
                Text = "Upload a new template design",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            panel.Children.Add(iconBorder);
            panel.Children.Add(titleText);
            panel.Children.Add(descText);

            card.Child = panel;
            return card;
        }

        private void TemplateCheckBox_Changed(int templateId, bool isChecked)
        {
            if (isChecked)
            {
                _selectedTemplateIds.Add(templateId);
            }
            else
            {
                _selectedTemplateIds.Remove(templateId);
            }

            UpdateSelectAllState();
            UpdateBulkActionsVisibility();
        }

        private void UpdateBulkActionsVisibility()
        {
            BulkActionsPanel.Visibility = _selectedTemplateIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            if (_selectedTemplateIds.Count > 0)
            {
                SelectionCountText.Text = $"({_selectedTemplateIds.Count} selected)";
                BulkActionCount.Text = $"({_selectedTemplateIds.Count} item{(_selectedTemplateIds.Count == 1 ? "" : "s")} selected)";
            }
        }

        private void UpdateSelectAllState()
        {
            var pageTemplates = GetCurrentPageTemplates();
            var selectedOnPage = pageTemplates.Count(t => _selectedTemplateIds.Contains(t.Id));
            
            if (selectedOnPage == 0)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else if (selectedOnPage == pageTemplates.Count)
            {
                SelectAllCheckBox.IsChecked = true;
            }
            else
            {
                SelectAllCheckBox.IsChecked = null; // Indeterminate state
            }
        }

        private void UpdateTemplateCount()
        {
            var seasonIndicator = _showAllSeasons ? " (All Seasons)" : "";
            TemplateCountText.Text = $"Upload, organize, and configure templates ({_filteredTemplates.Count} templates{seasonIndicator})";
        }

        #endregion

        #region Pagination

        private void CalculatePagination()
        {
            _totalPages = (int)Math.Ceiling((double)_filteredTemplates.Count / _templatesPerPage);
            if (_totalPages == 0) _totalPages = 1;
        }

        private void UpdatePagination()
        {
            var startIndex = (_currentPage - 1) * _templatesPerPage + 1;
            var endIndex = Math.Min(_currentPage * _templatesPerPage, _filteredTemplates.Count);
            
            PageInfoText.Text = _filteredTemplates.Count > 0 
                ? $"Showing {startIndex} to {endIndex} of {_filteredTemplates.Count} templates"
                : "No templates found";

            // Update navigation button states
            FirstPageButton.IsEnabled = _currentPage > 1;
            PrevPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < _totalPages;
            LastPageButton.IsEnabled = _currentPage < _totalPages;



            // Update page number buttons with modern styling
            PageNumbersPanel.Children.Clear();
            
            // Show up to 5 page numbers centered around current page
            var startPage = Math.Max(1, _currentPage - 2);
            var endPage = Math.Min(_totalPages, _currentPage + 2);
            
            // Add ellipsis and first page if needed
            if (startPage > 1)
            {
                var firstPageButton = CreateModernPageButton(1, false);
                PageNumbersPanel.Children.Add(firstPageButton);
                
                if (startPage > 2)
                {
                    var ellipsis = new TextBlock
                    {
                        Text = "...",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 8, 0)
                    };
                    PageNumbersPanel.Children.Add(ellipsis);
                }
            }
            
            // Add page number buttons
            for (int i = startPage; i <= endPage; i++)
            {
                var pageButton = CreateModernPageButton(i, i == _currentPage);
                PageNumbersPanel.Children.Add(pageButton);
            }
            
            // Add ellipsis and last page if needed
            if (endPage < _totalPages)
            {
                if (endPage < _totalPages - 1)
                {
                    var ellipsis = new TextBlock
                    {
                        Text = "...",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 8, 0)
                    };
                    PageNumbersPanel.Children.Add(ellipsis);
                }
                
                var lastPageButton = CreateModernPageButton(_totalPages, false);
                PageNumbersPanel.Children.Add(lastPageButton);
            }
        }

        private Border CreateModernPageButton(int pageNumber, bool isCurrentPage)
        {
            var button = new Button
            {
                Content = pageNumber.ToString(),
                Width = 36,
                Height = 36,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Cursor = Cursors.Hand,
                ToolTip = $"Go to page {pageNumber}",
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent
            };

            var border = new Border
            {
                Width = 36,
                Height = 36,
                Margin = new Thickness(2, 0, 2, 0),
                CornerRadius = new CornerRadius(8),
                Child = button
            };

            if (isCurrentPage)
            {
                // Current page styling - blue background
                border.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                border.BorderThickness = new Thickness(1);
                button.Foreground = Brushes.White;
            }
            else
            {
                // Regular page styling - white background with hover effects
                border.Background = Brushes.White;
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                border.BorderThickness = new Thickness(1);
                button.Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81));
                
                // Add hover effects
                button.MouseEnter += (s, e) => {
                    border.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                };
                button.MouseLeave += (s, e) => {
                    border.Background = Brushes.White;
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                };
            }

            var pageNum = pageNumber; // Capture for closure
            button.Click += (s, e) => {
                _currentPage = pageNum;
                RefreshTemplateDisplay();
                UpdatePagination();
            };

            return border;
        }

        private List<Template> GetCurrentPageTemplates()
        {
            var startIndex = (_currentPage - 1) * _templatesPerPage;
            return _filteredTemplates.Skip(startIndex).Take(_templatesPerPage).ToList();
        }

        #endregion

        #region Template Actions Dialog

        /// <summary>
        /// Show template actions dialog (replaces right-click context menu for touch-friendly interface)
        /// </summary>
        private void ShowTemplateActionsDialog(Template template)
        {
            try
            {
                // Create and show a clean, professional dialog
                var dialog = new Window
                {
                    Title = "Template Actions",
                    Width = 400,
                    Height = 480,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent
                };

                var mainBorder = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(16),
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(8)
                };

                mainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 8,
                    BlurRadius = 30,
                    Opacity = 0.15
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

                // Header with close button
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    CornerRadius = new CornerRadius(16, 16, 0, 0),
                    Padding = new Thickness(24, 20, 24, 20)
                };

                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Title section
                var titlePanel = new StackPanel();
                titlePanel.Children.Add(new TextBlock
                {
                    Text = "Template Actions",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39))
                });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = template.Name,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    Margin = new Thickness(0, 2, 0, 0)
                });

                Grid.SetColumn(titlePanel, 0);

                // Close button
                var closeButton = new Button
                {
                    Content = "✕",
                    Width = 32,
                    Height = 32,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                closeButton.Click += (s, e) => dialog.Close();
                Grid.SetColumn(closeButton, 1);

                headerGrid.Children.Add(titlePanel);
                headerGrid.Children.Add(closeButton);
                headerBorder.Child = headerGrid;
                Grid.SetRow(headerBorder, 0);

                // Content area
                var contentPanel = new StackPanel
                {
                    Margin = new Thickness(24, 20, 24, 20)
                };

                Grid.SetRow(contentPanel, 1);

                // Action buttons with clean, professional styling
                var editButton = CreateActionButton("Edit Template", "Modify template properties and settings");
                editButton.Click += async (s, e) => {
                    dialog.Close();
                    await EditTemplate_Click(template);
                };

                var duplicateButton = CreateActionButton("Duplicate Template", "Create a copy of this template");
                duplicateButton.Click += async (s, e) => {
                    dialog.Close();
                    await DuplicateTemplate_Click(template);
                };

                var renameButton = CreateActionButton("Rename Template", "Change the template name");
                renameButton.Click += async (s, e) => {
                    dialog.Close();
                    await RenameTemplate_Click(template);
                };

                var toggleButton = CreateActionButton(
                    template.IsActive ? "Disable Template" : "Enable Template",
                    template.IsActive ? "Hide this template from users" : "Make this template available to users"
                );
                toggleButton.Click += async (s, e) => {
                    dialog.Close();
                    await ToggleTemplateStatusAsync(template.Id);
                };

                var deleteButton = CreateActionButton("Delete Template", "Permanently remove this template");
                // Add subtle red accent for delete button
                deleteButton.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                deleteButton.Click += async (s, e) => {
                    dialog.Close();
                    await DeleteTemplate_Click(template);
                };

                contentPanel.Children.Add(editButton);
                contentPanel.Children.Add(duplicateButton);
                contentPanel.Children.Add(renameButton);
                contentPanel.Children.Add(toggleButton);
                contentPanel.Children.Add(deleteButton);

                mainGrid.Children.Add(headerBorder);
                mainGrid.Children.Add(contentPanel);
                mainBorder.Child = mainGrid;
                dialog.Content = mainBorder;

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error showing template actions dialog", ex);
                MessageBox.Show($"Error showing template actions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Create a clean, professional action button for the template actions dialog
        /// </summary>
        private Button CreateActionButton(string title, string description)
        {
            var button = new Button
            {
                Height = 60,
                Margin = new Thickness(0, 0, 0, 8),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(20, 12, 20, 12)
            };

            var textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            textPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39))
            });

            textPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            button.Content = textPanel;

            // Create clean button template with subtle hover effects
            var buttonTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.Name = "ButtonBorder";

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            contentFactory.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
            borderFactory.AppendChild(contentFactory);

            buttonTemplate.VisualTree = borderFactory;

            // Add subtle hover effect
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(249, 250, 251)), "ButtonBorder"));
            hoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(209, 213, 219)), "ButtonBorder"));
            buttonTemplate.Triggers.Add(hoverTrigger);

            // Add subtle press effect
            var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(243, 244, 246)), "ButtonBorder"));
            buttonTemplate.Triggers.Add(pressTrigger);

            button.Template = buttonTemplate;
            return button;
        }

        #endregion

        #region Helper Methods

        private BitmapImage LoadTemplateImage(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.DecodePixelWidth = 300; // Increased for better quality in cards
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // Force reload from disk
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Template image not found: {filePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading template image '{filePath}': {ex.Message}");
            }

            // Return placeholder image
            return CreatePlaceholderImage();
        }

        private BitmapImage CreatePlaceholderImage()
        {
            try
            {
                // Create a simple gray placeholder image programmatically
                var width = 200;
                var height = 300;
                var dpi = 96d;
                
                var pixelFormat = PixelFormats.Bgr24;
                var stride = (width * pixelFormat.BitsPerPixel + 7) / 8;
                var pixels = new byte[stride * height];
                
                // Fill with light gray color
                for (int i = 0; i < pixels.Length; i += 3)
                {
                    pixels[i] = 220;     // Blue
                    pixels[i + 1] = 220; // Green
                    pixels[i + 2] = 220; // Red
                }
                
                var bitmap = BitmapSource.Create(width, height, dpi, dpi, pixelFormat, null, pixels, stride);
                
                // Convert to BitmapImage
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                    stream.Position = 0;
                    
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }
                
                return bitmapImage;
            }
            catch
            {
                // If all else fails, create a minimal empty image
                var fallback = new BitmapImage();
                fallback.BeginInit();
                fallback.UriSource = new Uri("data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==");
                fallback.EndInit();
                return fallback;
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Public method to manually trigger template loading for testing
        /// </summary>
        public async Task ManualLoadTemplatesAsync()
        {
            LoggingService.Application.Information("Manual load templates triggered");
            
            // Use optimized loading for faster tab switching
            await LoadTemplatesOptimizedAsync();
        }

        /// <summary>
        /// Load template categories from database for filter dropdown
        /// </summary>
        private async Task LoadTemplateCategoriesAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Loading template categories from database...");
                
                // Use different database methods based on seasonal filter setting
                var result = _showAllSeasons 
                    ? await _databaseService.GetAllTemplateCategoriesAsync() // Show all categories regardless of season
                    : await _databaseService.GetTemplateCategoriesAsync(); // Only show currently active/in-season categories
                
                if (result.Success && result.Data != null)
                {
                    // Clear existing items for both dropdowns
                    CategoryFilterComboBox.Items.Clear();
                    BulkCategoryComboBox.ItemsSource = null; // Clear ItemsSource instead of Items when using data binding
                    
                    // Add "All" option to filter dropdown only
                    CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All" });
                    
                    // Load categories into both dropdowns - only filter by IsActive
                    var activeCategories = result.Data.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToList();
                    
                    // Add categories to filter dropdown
                    foreach (var category in activeCategories)
                    {
                        CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = category.Name });
                    }
                    
                    // Add categories to bulk assign dropdown
                    BulkCategoryComboBox.ItemsSource = activeCategories;
                    BulkCategoryComboBox.DisplayMemberPath = "Name";
                    BulkCategoryComboBox.SelectedValuePath = "Id";
                    
                    var totalCount = result.Data.Count();
                    System.Diagnostics.Debug.WriteLine($"Loaded {activeCategories.Count} active template categories (total: {totalCount}) - Show All Seasons: {_showAllSeasons}");
                    System.Diagnostics.Debug.WriteLine($"BulkCategoryComboBox populated with {BulkCategoryComboBox.Items.Count} items");
                    
                    // Log the category names for debugging
                    foreach (var category in activeCategories)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - Category: {category.Name} (ID: {category.Id})");
                    }

                    // Initialize template type filter (set default selection)
                    if (TemplateTypeFilterComboBox.SelectedIndex == -1 && TemplateTypeFilterComboBox.Items.Count > 0)
                    {
                        TemplateTypeFilterComboBox.SelectedIndex = 0; // Select "All Types"
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load template categories: {result.ErrorMessage}");
                    // Fallback to basic categories if database load fails
                    LoadFallbackCategories();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading template categories: {ex.Message}");
                // Fallback to basic categories if error occurs
                LoadFallbackCategories();
            }
        }

        /// <summary>
        /// Load fallback categories if database loading fails
        /// </summary>
        private void LoadFallbackCategories()
        {
            CategoryFilterComboBox.Items.Clear();
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All" });
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "Classic" });
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "Fun" });
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "Holiday" });
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "Seasonal" });
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "Premium" });
            
            // Also populate bulk category dropdown with fallback categories
            var fallbackCategories = new List<TemplateCategory>
            {
                new TemplateCategory { Id = 1, Name = "Classic", IsActive = true, SortOrder = 1 },
                new TemplateCategory { Id = 2, Name = "Fun", IsActive = true, SortOrder = 2 },
                new TemplateCategory { Id = 3, Name = "Holiday", IsActive = true, SortOrder = 3 },
                new TemplateCategory { Id = 4, Name = "Seasonal", IsActive = true, SortOrder = 4 },
                new TemplateCategory { Id = 5, Name = "Premium", IsActive = true, SortOrder = 5 }
            };
            
            BulkCategoryComboBox.ItemsSource = fallbackCategories;
            BulkCategoryComboBox.DisplayMemberPath = "Name";
            BulkCategoryComboBox.SelectedValuePath = "Id";
        }

        /// <summary>
        /// Force refresh of a specific template's display (used after image updates)
        /// </summary>
        public async Task RefreshTemplateDisplayAsync(int templateId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Refreshing display for template ID: {templateId}");
                
                // Since we added cache-busting to LoadTemplateImage, 
                // we just need to refresh the display to reload images with new timestamps
                RefreshTemplateDisplay();
                
                // Optionally, we could do a full reload to ensure database changes are reflected
                // await LoadTemplatesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing template display: {ex.Message}");
                // Fallback to full refresh
                await LoadTemplatesAsync();
            }
        }

        /// <summary>
        /// Update the system date preview in the system date button
        /// </summary>
        private async Task UpdateSystemDatePreviewAsync()
        {
            try
            {
                var statusResult = await _databaseService.GetSystemDateStatusAsync();
                if (statusResult.Success && statusResult.Data != null)
                {
                    var status = statusResult.Data;
                    var activeSeasons = status.ActiveSeasonsCount > 0 ? $" • {status.ActiveSeasonsCount} active season{(status.ActiveSeasonsCount == 1 ? "" : "s")}" : "";
                    SystemDatePreview.Text = $"{DateTime.Now:MMM dd, yyyy}{activeSeasons}";
                }
                else
                {
                    SystemDatePreview.Text = DateTime.Now.ToString("MMM dd, yyyy");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating system date preview: {ex.Message}");
                SystemDatePreview.Text = DateTime.Now.ToString("MMM dd, yyyy");
            }
        }

        #endregion

        #region Category Management Modal

        private void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show the category management modal (in-window)
                // Create and show the modal
                var modal = new Photobooth.Controls.CategoryManagementModal();
                
                // Subscribe to the categories changed event
                modal.CategoriesChangedEvent += async (sender, categoriesChanged) =>
                {
                    if (categoriesChanged)
                    {
                        await LoadTemplateCategoriesAsync();
                        await LoadTemplatesAsync(); // Refresh templates to reflect category changes
                    }
                };
                
                ModalService.Instance.ShowModal(modal);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error opening category management modal", ex);
                MessageBox.Show("An error occurred. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EditTemplate_Click(Template template)
        {
            try
            {
                var parentWindow = Window.GetWindow(this);
                var notificationService = NotificationService.Instance;

                // Create refresh callback that will update the UI when preview images change
                Action<int> refreshCallback = (templateId) =>
                {
                    Dispatcher.BeginInvoke(async () =>
                    {
                        await RefreshTemplateDisplayAsync(templateId);
                    });
                };

                var updatedTemplate = await TemplateEditorDialog.ShowEditorAsync(template, _databaseService, notificationService, parentWindow, refreshCallback);
                
                if (updatedTemplate != null)
                {
                    // Refresh templates to show changes
                    await LoadTemplatesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening template editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteTemplate_Click(Template template)
        {
            try
            {
                var parentWindow = Window.GetWindow(this);
                var result = ConfirmationDialog.ShowDeleteConfirmation(template.Name, "template", parentWindow);
                
                if (result)
                {
                    LoadingPanel.Visibility = Visibility.Visible;
                    
                    // Use TemplateManager for complete deletion (database + file system)
                    var deleteResult = await _templateManager.DeleteTemplateCompletelyAsync(template.Id);
                    
                    if (deleteResult.IsValid)
                    {
                        // Remove from UI
                        await LoadTemplatesAsync();
                        
                        // Show success notification toast
                        NotificationService.Instance.ShowSuccess("Template Deleted", $"'{template.Name}' has been deleted successfully.");
                        
                        // Show warning if there was a file system issue but database deletion succeeded
                        if (deleteResult.Warnings.Count > 0)
                        {
                            NotificationService.Instance.ShowWarning("Partial Deletion", string.Join(", ", deleteResult.Warnings));
                        }
                    }
                    else
                    {
                        var errorMessage = deleteResult.Errors.Count > 0 ? string.Join(", ", deleteResult.Errors) : "Unknown error";
                        MessageBox.Show($"Failed to delete template: {errorMessage}", "Error", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DuplicateTemplate_Click(Template template)
        {
            try
            {
                var parentWindow = Window.GetWindow(this);
                
                // Show input dialog to get new template name
                var newName = InputDialog.ShowInputDialog(
                    "Duplicate Template", 
                    $"Enter a name for the copy of '{template.Name}':",
                    $"{template.Name} - Copy",
                    parentWindow);
                
                if (string.IsNullOrEmpty(newName))
                {
                    return; // User cancelled
                }

                LoadingPanel.Visibility = Visibility.Visible;
                
                // Use TemplateManager to duplicate the template
                var duplicateResult = await _templateManager.DuplicateTemplateAsync(template.Id, newName);
                
                if (duplicateResult.Success && duplicateResult.Data != null)
                {
                    // Refresh templates to show the new copy
                    await LoadTemplatesAsync();
                    
                    // Show success notification toast
                    NotificationService.Instance.ShowSuccess("Template Duplicated", 
                        $"'{template.Name}' has been duplicated as '{duplicateResult.Data.Name}'.");
                }
                else
                {
                    MessageBox.Show($"Failed to duplicate template: {duplicateResult.ErrorMessage}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error duplicating template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RenameTemplate_Click(Template template)
        {
            try
            {
                var parentWindow = Window.GetWindow(this);
                
                // Show input dialog to get new template name
                var newName = InputDialog.ShowInputDialog(
                    "Rename Template", 
                    $"Enter a new name for '{template.Name}':",
                    template.Name,
                    parentWindow);
                
                if (string.IsNullOrEmpty(newName))
                {
                    return; // User cancelled
                }

                if (newName == template.Name)
                {
                    // No change needed
                    return;
                }

                LoadingPanel.Visibility = Visibility.Visible;
                
                // Use TemplateManager to rename the template completely (database + folder)
                var renameResult = await _templateManager.RenameTemplateCompletelyAsync(template.Id, newName);
                
                if (renameResult.Success && renameResult.Data != null)
                {
                    // Refresh templates to show the updated name
                    await LoadTemplatesAsync();
                    
                    // Show success notification toast
                    NotificationService.Instance.ShowSuccess("Template Renamed", 
                        $"Template has been renamed to '{renameResult.Data.Name}' successfully.");
                }
                else
                {
                    MessageBox.Show($"Failed to rename template: {renameResult.ErrorMessage}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Update the active filters display to show which filters are currently applied
        /// </summary>
        private void UpdateActiveFiltersDisplay()
        {
            var activeFilters = new List<string>();
            
            // Check for category filter
            if (_selectedCategory != "All Categories" && _selectedCategory != "All")
            {
                activeFilters.Add($"Category: {_selectedCategory}");
            }
            
            // Check for seasonal filter
            if (_showAllSeasons)
            {
                activeFilters.Add("All Seasons");
            }
            
            // Check for non-default sort
            if (_sortBy != "database")
            {
                var sortDisplay = _sortBy switch
                {
                    "name" => _sortOrder == "asc" ? "Name A-Z" : "Name Z-A",
                    "date" => _sortOrder == "desc" ? "Newest First" : "Oldest First",
                    _ => "Custom Sort"
                };
                activeFilters.Add($"Sort: {sortDisplay}");
            }
            
            // Update filter tags panel
            FilterTagsPanel.Children.Clear();
            foreach (var filter in activeFilters)
            {
                var filterTag = CreateFilterTag(filter);
                FilterTagsPanel.Children.Add(filterTag);
            }
            
            // Update visibility of active filters section
            ActiveFiltersPanel.Visibility = activeFilters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // Update filter badge
            if (activeFilters.Count > 0)
            {
                ActiveFiltersBadge.Visibility = Visibility.Visible;
                ActiveFiltersText.Text = $"{activeFilters.Count} filter{(activeFilters.Count == 1 ? "" : "s")} active";
            }
            else
            {
                ActiveFiltersBadge.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Create a visual filter tag for the active filters display
        /// </summary>
        private Border CreateFilterTag(string filterText)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)), // Blue background
                BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253)), // Blue border
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = filterText,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175)), // Blue text
                    FontWeight = FontWeights.Medium
                }
            };
        }

        #endregion

        #region Removed Modal Methods
        
        // The following methods have been removed because they reference modal controls that no longer exist:
        // - ModalAddTemplateCategory_Click
        // - RefreshModalAndRestoreScroll
        // - CreateLoadingContent, CreateAddButtonContent, CreateUpdateButtonContent
        // - ModalCategoryDescription_TextChanged, UpdateModalCharacterCount
        // - LoadModalCategoriesAsync
        // - ClearCategoryForm, SetAddMode, SetEditMode
        // - ModalCancelEdit_Click
        // - CreateModalCategoryCard
        // - EditCategory_Click, ToggleCategory_Click, DeleteCategory_Click
        //
        // All category management functionality has been moved to CategoryManagementDialog.xaml/.cs
        
        // Method removed - using CategoryManagementDialog instead

        // All remaining modal methods removed - using CategoryManagementDialog instead
        
        #endregion
    }
}
