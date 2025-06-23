using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth.Controls
{
    public partial class CategoryManagementDialog : Window
    {
        private readonly IDatabaseService _databaseService;
        private int _editingCategoryId = -1;
        private bool _isEditMode = false;
        private ScrollViewer? _dialogScrollViewer;

        public bool CategoriesChanged { get; private set; } = false;

        public CategoryManagementDialog()
        {
            try
            {
                Console.WriteLine("=== CategoryManagementDialog Constructor Start ===");
                
                Console.WriteLine("Initializing component...");
                InitializeComponent();
                Console.WriteLine("InitializeComponent completed");
                
                Console.WriteLine("Creating database service...");
                _databaseService = new DatabaseService();
                Console.WriteLine("Database service created");
                
                // Find the ScrollViewer for smooth scrolling to edit form
                Loaded += (s, e) => {
                    try
                    {
                        Console.WriteLine("Dialog Loaded event triggered");
                        _dialogScrollViewer = FindChild<ScrollViewer>(this);
                        Console.WriteLine("ScrollViewer found: " + (_dialogScrollViewer != null));
                        
                        Console.WriteLine("Populating day combo boxes...");
                        PopulateDayComboBoxes();
                        Console.WriteLine("Day combo boxes populated");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in Loaded event: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                };
                
                Console.WriteLine("Loading categories...");
                LoadCategories();
                Console.WriteLine("LoadCategories called");
                
                Console.WriteLine("=== CategoryManagementDialog Constructor Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in CategoryManagementDialog constructor: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Helper method to find child elements
        private T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        /// <summary>
        /// Populate day combo boxes with days 1-31
        /// </summary>
        private void PopulateDayComboBoxes()
        {
            try
            {
                Console.WriteLine("PopulateDayComboBoxes: Starting");
                
                Console.WriteLine("Checking if StartDayComboBox exists: " + (StartDayComboBox != null));
                Console.WriteLine("Checking if EndDayComboBox exists: " + (EndDayComboBox != null));
                
                if (StartDayComboBox == null)
                {
                    Console.WriteLine("ERROR: StartDayComboBox is null!");
                    return;
                }
                
                if (EndDayComboBox == null)
                {
                    Console.WriteLine("ERROR: EndDayComboBox is null!");
                    return;
                }
                
                Console.WriteLine("Clearing existing items...");
                StartDayComboBox.Items.Clear();
                EndDayComboBox.Items.Clear();
                Console.WriteLine("Items cleared");
                
                Console.WriteLine("Adding day items 1-31...");
                for (int day = 1; day <= 31; day++)
                {
                    var dayString = day.ToString("00");
                    
                    var startItem = new ComboBoxItem
                    {
                        Content = day.ToString(),
                        Tag = dayString
                    };
                    StartDayComboBox.Items.Add(startItem);
                    
                    var endItem = new ComboBoxItem
                    {
                        Content = day.ToString(),
                        Tag = dayString
                    };
                    EndDayComboBox.Items.Add(endItem);
                }
                
                Console.WriteLine("PopulateDayComboBoxes: Completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in PopulateDayComboBoxes: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handle seasonal checkbox toggle
        /// </summary>
        private void IsSeasonalCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = IsSeasonalCheckBox.IsChecked == true;
            SeasonalSettingsPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            
            if (isChecked)
            {
                // Set default values if none are set
                if (StartMonthComboBox.SelectedIndex == -1)
                {
                    StartMonthComboBox.SelectedIndex = 0; // January
                    StartDayComboBox.SelectedIndex = 0;   // 1st
                    EndMonthComboBox.SelectedIndex = 1;   // February 
                    EndDayComboBox.SelectedIndex = 27;    // 28th
                }
                UpdateSeasonPreview();
            }
        }

        /// <summary>
        /// Handle season date changes
        /// </summary>
        private void SeasonDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateSeasonPreview();
        }

        /// <summary>
        /// Handle season priority text changes
        /// </summary>
        private void SeasonPriority_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Validate numeric input
                if (int.TryParse(textBox.Text, out int priority))
                {
                    if (priority < 0) textBox.Text = "0";
                    if (priority > 999) textBox.Text = "999";
                }
                else if (!string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = "100"; // Default value
                }
            }
            UpdateSeasonPreview();
        }

        /// <summary>
        /// Update the season preview text
        /// </summary>
        private void UpdateSeasonPreview()
        {
            try
            {
                Console.WriteLine("UpdateSeasonPreview: Starting");
                
                // Add null checks for controls that might not be initialized yet during XAML loading
                if (StartMonthComboBox == null || StartDayComboBox == null || 
                    EndMonthComboBox == null || EndDayComboBox == null || 
                    SeasonPriorityTextBox == null || SeasonPreviewText == null)
                {
                    Console.WriteLine("UpdateSeasonPreview: Controls not yet initialized, skipping");
                    return;
                }
                
                Console.WriteLine("UpdateSeasonPreview: All controls available, proceeding");
                
                if (StartMonthComboBox.SelectedItem is ComboBoxItem startMonthItem &&
                    StartDayComboBox.SelectedItem is ComboBoxItem startDayItem &&
                    EndMonthComboBox.SelectedItem is ComboBoxItem endMonthItem &&
                    EndDayComboBox.SelectedItem is ComboBoxItem endDayItem)
                {
                    string startDate = $"{startMonthItem.Tag}-{startDayItem.Tag}";
                    string endDate = $"{endMonthItem.Tag}-{endDayItem.Tag}";
                    
                    string startMonthName = startMonthItem.Content.ToString() ?? "";
                    string endMonthName = endMonthItem.Content.ToString() ?? "";
                    
                    string priorityText = int.TryParse(SeasonPriorityTextBox.Text, out int priority) ? 
                        $" (Priority: {priority})" : "";
                    
                    // Check if it spans years
                    bool spansYears = string.Compare(startDate, endDate) > 0;
                    string spanText = spansYears ? " (spans New Year)" : "";
                    
                    SeasonPreviewText.Text = $"Active: {startMonthName} {startDayItem.Content} - {endMonthName} {endDayItem.Content}{spanText}{priorityText}";
                    Console.WriteLine($"UpdateSeasonPreview: Preview updated to '{SeasonPreviewText.Text}'");
                }
                else
                {
                    SeasonPreviewText.Text = "Set all dates to see preview";
                    Console.WriteLine("UpdateSeasonPreview: Not all dates selected, showing default message");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in UpdateSeasonPreview: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Try to set a safe fallback if SeasonPreviewText exists
                if (SeasonPreviewText != null)
                {
                    SeasonPreviewText.Text = "Set dates to see preview";
                }
            }
        }

        // Preset button handlers
        private void ValentinesPreset_Click(object sender, RoutedEventArgs e)
        {
            SetSeasonPreset("02", "01", "02", "20", 100);
        }

        private void EasterPreset_Click(object sender, RoutedEventArgs e)
        {
            SetSeasonPreset("03", "15", "04", "15", 90);
        }

        private void HalloweenPreset_Click(object sender, RoutedEventArgs e)
        {
            SetSeasonPreset("10", "15", "11", "01", 85);
        }

        private void ChristmasPreset_Click(object sender, RoutedEventArgs e)
        {
            SetSeasonPreset("12", "01", "01", "05", 95);
        }

        private void NewYearPreset_Click(object sender, RoutedEventArgs e)
        {
            SetSeasonPreset("12", "25", "01", "15", 80);
        }

        private void SummerPreset_Click(object sender, RoutedEventArgs e)
        {
            SetSeasonPreset("06", "01", "08", "31", 70);
        }

        /// <summary>
        /// Set a seasonal preset
        /// </summary>
        private void SetSeasonPreset(string startMonth, string startDay, string endMonth, string endDay, int priority)
        {
            IsSeasonalCheckBox.IsChecked = true;
            SeasonalSettingsPanel.Visibility = Visibility.Visible;
            
            // Set months (index is month number - 1)
            StartMonthComboBox.SelectedIndex = int.Parse(startMonth) - 1;
            EndMonthComboBox.SelectedIndex = int.Parse(endMonth) - 1;
            
            // Set days (index is day number - 1)
            StartDayComboBox.SelectedIndex = int.Parse(startDay) - 1;
            EndDayComboBox.SelectedIndex = int.Parse(endDay) - 1;
            
            SeasonPriorityTextBox.Text = priority.ToString();
            
            UpdateSeasonPreview();
        }

        public static bool ShowCategoryDialog(Window? owner = null)
        {
            try
            {
                Console.WriteLine("=== ShowCategoryDialog Start ===");
                
                Console.WriteLine("Creating CategoryManagementDialog instance...");
                var dialog = new CategoryManagementDialog();
                Console.WriteLine("Dialog instance created successfully");
                
                if (owner != null)
                {
                    Console.WriteLine("Setting dialog owner...");
                    dialog.Owner = owner;
                    Console.WriteLine("Dialog owner set");
                }
                else
                {
                    Console.WriteLine("Using MainWindow as owner...");
                    dialog.Owner = Application.Current.MainWindow;
                    Console.WriteLine("MainWindow owner set");
                }
                
                Console.WriteLine("Showing dialog...");
                dialog.ShowDialog();
                Console.WriteLine("Dialog closed");
                
                Console.WriteLine($"Returning CategoriesChanged: {dialog.CategoriesChanged}");
                return dialog.CategoriesChanged;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ShowCategoryDialog: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async void LoadCategories()
        {
            try
            {
                Console.WriteLine("LoadCategories: Starting");
                
                Console.WriteLine("Calling GetAllTemplateCategoriesAsync...");
                var result = await _databaseService.GetAllTemplateCategoriesAsync();
                Console.WriteLine($"Database result: Success={result.Success}, Data count={result.Data?.Count ?? 0}");
                
                Console.WriteLine("Clearing CategoriesListPanel...");
                CategoriesListPanel.Children.Clear();
                Console.WriteLine("CategoriesListPanel cleared");
                
                if (result.Success && result.Data != null)
                {
                    Console.WriteLine($"Processing {result.Data.Count} categories...");
                    foreach (var category in result.Data)
                    {
                        Console.WriteLine($"Creating list item for category: {category.Name}");
                        var categoryItem = CreateCategoryListItem(category);
                        CategoriesListPanel.Children.Add(categoryItem);
                        Console.WriteLine($"Added category item: {category.Name}");
                    }
                    Console.WriteLine("All categories processed successfully");
                }
                else
                {
                    Console.WriteLine($"Database error: {result.ErrorMessage}");
                    NotificationService.Instance.ShowError("Loading Failed", $"Failed to load categories: {result.ErrorMessage}");
                }
                
                Console.WriteLine("LoadCategories: Completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in LoadCategories: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                LoggingService.Application.Error("Error loading categories", ex);
                NotificationService.Instance.ShowError("Loading Error", "Error loading categories. Please try again.");
            }
        }

        private Border CreateCategoryListItem(TemplateCategory category)
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#E5E7EB") ?? System.Windows.Media.Brushes.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoPanel = new StackPanel();
            
            // Category name with status indicator
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var nameBlock = new TextBlock
            {
                Text = category.Name,
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#374151") ?? System.Windows.Media.Brushes.Black),
                VerticalAlignment = VerticalAlignment.Center
            };
            namePanel.Children.Add(nameBlock);
            
            var statusBadge = new Border
            {
                Background = category.IsActive ? 
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#10B981") ?? System.Windows.Media.Brushes.Green) :
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#6B7280") ?? System.Windows.Media.Brushes.Gray),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var statusText = new TextBlock
            {
                Text = category.IsActive ? "Active" : "Inactive",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = System.Windows.Media.Brushes.White
            };
            statusBadge.Child = statusText;
            namePanel.Children.Add(statusBadge);
            namePanel.Margin = new Thickness(0, 0, 0, 4);
            
            infoPanel.Children.Add(namePanel);

            if (!string.IsNullOrEmpty(category.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = category.Description,
                    FontSize = 14,
                    Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#6B7280") ?? System.Windows.Media.Brushes.Gray),
                    TextWrapping = TextWrapping.Wrap
                };
                infoPanel.Children.Add(descBlock);
            }

            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Edit Button
            var editButton = new Button
            {
                Content = "EDIT",
                Style = (Style)FindResource("ModernActionButtonStyle"),
                Background = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#10B981") ?? System.Windows.Media.Brushes.Green),
                Foreground = System.Windows.Media.Brushes.White,
                ToolTip = "Edit this category"
            };
            editButton.Click += (s, e) => EditCategory(category);
            buttonPanel.Children.Add(editButton);

            // Toggle Active/Inactive Button
            var toggleButton = new Button
            {
                Content = category.IsActive ? "DISABLE" : "ENABLE",
                Style = (Style)FindResource("ModernActionButtonStyle"),
                Background = category.IsActive ? 
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#F59E0B") ?? System.Windows.Media.Brushes.Orange) :
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#059669") ?? System.Windows.Media.Brushes.Green),
                Foreground = System.Windows.Media.Brushes.White,
                ToolTip = category.IsActive ? "Disable this category" : "Enable this category"
            };
            toggleButton.Click += (s, e) => ToggleCategory(category);
            buttonPanel.Children.Add(toggleButton);

            // Delete Button
            var deleteButton = new Button
            {
                Content = "DELETE",
                Style = (Style)FindResource("ModernActionButtonStyle"),
                Background = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#EF4444") ?? System.Windows.Media.Brushes.Red),
                Foreground = System.Windows.Media.Brushes.White,
                ToolTip = "Delete this category permanently"
            };
            deleteButton.Click += (s, e) => DeleteCategory(category);
            buttonPanel.Children.Add(deleteButton);

            Grid.SetColumn(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            border.Child = grid;
            return border;
        }

        private void CategoryDescription_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (CategoryDescriptionTextBox != null && DescriptionCharCount != null)
            {
                DescriptionCharCount.Text = $"{CategoryDescriptionTextBox.Text.Length}/200";
            }
        }

        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CategoryNameTextBox.Text))
                {
                    NotificationService.Instance.ShowWarning("Validation Error", "Please enter a category name.");
                    CategoryNameTextBox.Focus();
                    return;
                }

                // Validate seasonal data if seasonal is enabled
                bool isSeasonal = IsSeasonalCheckBox.IsChecked == true;
                string? seasonStartDate = null;
                string? seasonEndDate = null;
                int seasonalPriority = 0;

                if (isSeasonal)
                {
                    if (StartMonthComboBox.SelectedItem is ComboBoxItem startMonthItem &&
                        StartDayComboBox.SelectedItem is ComboBoxItem startDayItem &&
                        EndMonthComboBox.SelectedItem is ComboBoxItem endMonthItem &&
                        EndDayComboBox.SelectedItem is ComboBoxItem endDayItem)
                    {
                        seasonStartDate = $"{startMonthItem.Tag}-{startDayItem.Tag}";
                        seasonEndDate = $"{endMonthItem.Tag}-{endDayItem.Tag}";
                        
                        if (!int.TryParse(SeasonPriorityTextBox.Text, out seasonalPriority))
                        {
                            seasonalPriority = 100; // Default priority
                        }
                    }
                    else
                    {
                        NotificationService.Instance.ShowWarning("Validation Error", "Please set all seasonal dates.");
                        return;
                    }
                }

                if (_isEditMode)
                {
                    var result = await _databaseService.UpdateTemplateCategoryAsync(
                        _editingCategoryId, 
                        CategoryNameTextBox.Text.Trim(), 
                        CategoryDescriptionTextBox.Text?.Trim() ?? string.Empty,
                        isSeasonal,
                        seasonStartDate,
                        seasonEndDate,
                        seasonalPriority);
                        
                    if (result.Success)
                    {
                        NotificationService.Instance.ShowSuccess("Category Updated", "Category updated successfully!");
                    }
                    else
                    {
                        NotificationService.Instance.ShowError("Update Failed", $"Failed to update category: {result.ErrorMessage}");
                        return;
                    }
                }
                else
                {
                    var result = await _databaseService.CreateTemplateCategoryAsync(
                        CategoryNameTextBox.Text.Trim(), 
                        CategoryDescriptionTextBox.Text?.Trim() ?? string.Empty,
                        isSeasonal,
                        seasonStartDate,
                        seasonEndDate,
                        seasonalPriority);
                        
                    if (result.Success)
                    {
                        NotificationService.Instance.ShowSuccess("Category Created", "Category created successfully!");
                    }
                    else
                    {
                        NotificationService.Instance.ShowError("Creation Failed", $"Failed to create category: {result.ErrorMessage}");
                        return;
                    }
                }

                CategoriesChanged = true;
                ClearForm();
                LoadCategories();

                LoggingService.Application.Information(_isEditMode ? "Category updated successfully: {CategoryName}" : "Category created successfully: {CategoryName}", 
                    ("CategoryName", CategoryNameTextBox.Text.Trim()));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error saving category", ex);
                NotificationService.Instance.ShowError("Unexpected Error", "Error saving category. Please try again.");
            }
        }

        private void EditCategory(TemplateCategory category)
        {
            _isEditMode = true;
            _editingCategoryId = category.Id;
            
            // Fill the form with category data
            CategoryNameTextBox.Text = category.Name;
            CategoryDescriptionTextBox.Text = category.Description ?? string.Empty;
            
            // Fill seasonal data
            IsSeasonalCheckBox.IsChecked = category.IsSeasonalCategory;
            
            if (category.IsSeasonalCategory && !string.IsNullOrEmpty(category.SeasonStartDate) && !string.IsNullOrEmpty(category.SeasonEndDate))
            {
                try
                {
                    var startParts = category.SeasonStartDate.Split('-');
                    var endParts = category.SeasonEndDate.Split('-');
                    
                    if (startParts.Length == 2 && endParts.Length == 2)
                    {
                        // Set start date
                        StartMonthComboBox.SelectedIndex = int.Parse(startParts[0]) - 1;
                        StartDayComboBox.SelectedIndex = int.Parse(startParts[1]) - 1;
                        
                        // Set end date
                        EndMonthComboBox.SelectedIndex = int.Parse(endParts[0]) - 1;
                        EndDayComboBox.SelectedIndex = int.Parse(endParts[1]) - 1;
                        
                        // Set priority
                        SeasonPriorityTextBox.Text = category.SeasonalPriority.ToString();
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Warning("Failed to parse seasonal dates for category {CategoryName}: {Error}", 
                        ("CategoryName", category.Name), ("Error", ex.Message));
                }
                
                SeasonalSettingsPanel.Visibility = Visibility.Visible;
                UpdateSeasonPreview();
            }
            else
            {
                SeasonalSettingsPanel.Visibility = Visibility.Collapsed;
            }
            
            // Update UI for edit mode
            CancelEditButton.Visibility = Visibility.Visible;
            ((TextBlock)((StackPanel)AddButtonContent).Children[1]).Text = "Update Category";
            ((TextBlock)((StackPanel)AddButtonContent).Children[0]).Text = "✏️";
            
            // Scroll to top to show the edit form
            if (_dialogScrollViewer != null)
            {
                _dialogScrollViewer.ScrollToTop();
            }
            
            // Focus on the name field for immediate editing
            CategoryNameTextBox.Focus();
            CategoryNameTextBox.SelectAll();
        }

        private async void ToggleCategory(TemplateCategory category)
        {
            try
            {
                Console.WriteLine($"=== TOGGLE CATEGORY UI DEBUG ===");
                Console.WriteLine($"Category: {category.Name} (ID: {category.Id})");
                Console.WriteLine($"Current IsActive: {category.IsActive}");
                
                string action = category.IsActive ? "deactivate" : "activate";
                Console.WriteLine($"Action: {action}");
                
                // Get template count for this category
                Console.WriteLine("Getting templates for category...");
                var templatesResult = await _databaseService.GetTemplatesByCategoryAsync(category.Id);
                int templateCount = templatesResult.Success ? templatesResult.Data?.Count ?? 0 : 0;
                Console.WriteLine($"Template count result: Success={templatesResult.Success}, Count={templateCount}");
                
                string message = $"Are you sure you want to {action} the category '{category.Name}'?";
                if (templateCount > 0)
                {
                    message += $"\n\nThis will also {action} {templateCount} template{(templateCount == 1 ? "" : "s")} in this category.";
                }
                Console.WriteLine($"Confirmation message: {message}");
                
                bool confirmed = ConfirmationDialog.ShowConfirmation(
                    $"{char.ToUpper(action[0])}{action.Substring(1)} Category",
                    message,
                    char.ToUpper(action[0]) + action.Substring(1),
                    "Cancel",
                    this
                );
                
                Console.WriteLine($"User confirmed: {confirmed}");
                
                if (confirmed)
                {
                    Console.WriteLine($"Calling UpdateTemplateCategoryStatusAsync({category.Id}, {!category.IsActive})");
                    var result = await _databaseService.UpdateTemplateCategoryStatusAsync(category.Id, !category.IsActive);
                    Console.WriteLine($"Update result: Success={result.Success}, Error={result.ErrorMessage}");
                    
                    if (result.Success)
                    {
                        Console.WriteLine("✅ Update successful, refreshing UI");
                        CategoriesChanged = true;
                        LoadCategories();
                        LoggingService.Application.Information("Category {Action}: {CategoryName}", 
                            ("Action", action + "d"), ("CategoryName", category.Name));
                        
                        string successMessage = $"Category '{category.Name}' has been {action}d successfully!";
                        if (templateCount > 0)
                        {
                            successMessage += $"\n{templateCount} template{(templateCount == 1 ? "" : "s")} {(templateCount == 1 ? "was" : "were")} also {action}d.";
                        }
                        
                        Console.WriteLine($"Success message: {successMessage}");
                        NotificationService.Instance.ShowSuccess($"Category {char.ToUpper(action[0])}{action.Substring(1)}d", successMessage);
                    }
                    else
                    {
                        Console.WriteLine("❌ Update failed");
                        NotificationService.Instance.ShowError($"Failed to {char.ToUpper(action[0])}{action.Substring(1)} Category", 
                            $"Could not {action} category '{category.Name}': {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UI Exception: {ex.Message}");
                LoggingService.Application.Error("Error toggling category status", ex);
                NotificationService.Instance.ShowError("Unexpected Error", 
                    $"Error updating category '{category.Name}' status. Please try again.");
            }
        }

        private async void DeleteCategory(TemplateCategory category)
        {
            try
            {
                bool confirmed = ConfirmationDialog.ShowDeleteConfirmation(category.Name, "category", this);
                
                if (confirmed)
                {
                    var result = await _databaseService.DeleteTemplateCategoryAsync(category.Id);
                    if (result.Success)
                    {
                        CategoriesChanged = true;
                        LoadCategories();
                        LoggingService.Application.Information("Category deleted successfully: {CategoryName}", ("CategoryName", category.Name));
                        NotificationService.Instance.ShowSuccess("Category Deleted", 
                            $"Category '{category.Name}' has been deleted successfully!");
                    }
                    else
                    {
                        NotificationService.Instance.ShowError("Deletion Failed", 
                            $"Could not delete category '{category.Name}': {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error deleting category", ex);
                NotificationService.Instance.ShowError("Unexpected Error", 
                    $"Error deleting category '{category.Name}'. Please try again.");
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            // Exit edit mode
            _isEditMode = false;
            _editingCategoryId = -1;
            
            // Reset UI to add mode
            CancelEditButton.Visibility = Visibility.Collapsed;
            ((TextBlock)((StackPanel)AddButtonContent).Children[1]).Text = "Add Category";
            ((TextBlock)((StackPanel)AddButtonContent).Children[0]).Text = "➕";
            
            // Clear the form
            ClearForm();
        }

        private void ClearForm()
        {
            _isEditMode = false;
            _editingCategoryId = -1;
            
            CategoryNameTextBox.Text = string.Empty;
            CategoryDescriptionTextBox.Text = string.Empty;
            
            // Reset seasonal fields
            IsSeasonalCheckBox.IsChecked = false;
            SeasonalSettingsPanel.Visibility = Visibility.Collapsed;
            StartMonthComboBox.SelectedIndex = -1;
            StartDayComboBox.SelectedIndex = -1;
            EndMonthComboBox.SelectedIndex = -1;
            EndDayComboBox.SelectedIndex = -1;
            SeasonPriorityTextBox.Text = "100";
            SeasonPreviewText.Text = "Set dates to see preview";
            
            // Reset character count
            if (DescriptionCharCount != null)
            {
                DescriptionCharCount.Text = "0/200";
                DescriptionCharCount.Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#9CA3AF") ?? System.Windows.Media.Brushes.Gray);
            }
            
            CancelEditButton.Visibility = Visibility.Collapsed;
            ((TextBlock)((StackPanel)AddButtonContent).Children[1]).Text = "Add Category";
            ((TextBlock)((StackPanel)AddButtonContent).Children[0]).Text = "➕";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
 