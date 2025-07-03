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


                InitializeComponent();


                _databaseService = new DatabaseService();

                // Find the ScrollViewer for smooth scrolling to edit form
                Loaded += (s, e) => {
                    try
                    {

                        _dialogScrollViewer = FindChild<ScrollViewer>(this);


                        PopulateDayComboBoxes();

                    }
                    catch
                    {


                    }
                };

                LoadCategories();


            }
            catch
            {


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
        /// Populate day combo boxes with proper days based on selected months
        /// </summary>
        private void PopulateDayComboBoxes()
        {
            try
            {

                if (StartDayComboBox == null || EndDayComboBox == null)
                {

                    return;
                }
                
                // Initialize with 31 days, will be adjusted based on month selection
                PopulateDayComboBox(StartDayComboBox, 31);
                PopulateDayComboBox(EndDayComboBox, 31);

            }
            catch
            {


            }
        }

        /// <summary>
        /// Populate a single day combo box with specified number of days
        /// </summary>
        private void PopulateDayComboBox(ComboBox dayComboBox, int maxDays)
        {
            var selectedValue = (dayComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            dayComboBox.Items.Clear();
            
            for (int day = 1; day <= maxDays; day++)
            {
                var dayString = day.ToString("00");
                var item = new ComboBoxItem
                {
                    Content = day.ToString(),
                    Tag = dayString
                };
                dayComboBox.Items.Add(item);
                
                // Restore selection if it's still valid
                if (selectedValue == dayString)
                {
                    dayComboBox.SelectedItem = item;
                }
            }
            
            // If previous selection is now invalid, select the last available day
            if (dayComboBox.SelectedItem == null && selectedValue != null)
            {
                if (int.TryParse(selectedValue, out int previousDay) && previousDay > maxDays)
                {
                    dayComboBox.SelectedIndex = maxDays - 1; // Select last day
                }
            }
        }

        /// <summary>
        /// Get the number of days in a specific month, considering leap years
        /// </summary>
        private int GetDaysInMonth(int month, int year = 0)
        {
            // Use current year if not specified
            if (year == 0) year = DateTime.Now.Year;
            
            return month switch
            {
                2 => DateTime.IsLeapYear(year) ? 29 : 28, // February
                4 or 6 or 9 or 11 => 30, // April, June, September, November
                _ => 31 // January, March, May, July, August, October, December
            };
        }

        /// <summary>
        /// Update day combo box based on selected month
        /// </summary>
        private void UpdateDayComboBoxForMonth(ComboBox monthComboBox, ComboBox dayComboBox)
        {
            if (monthComboBox.SelectedItem is ComboBoxItem selectedMonth)
            {
                if (int.TryParse(selectedMonth.Tag?.ToString(), out int month))
                {
                    var daysInMonth = GetDaysInMonth(month);
                    PopulateDayComboBox(dayComboBox, daysInMonth);
                }
            }
        }

        /// <summary>
        /// Validate seasonal dates for leap year and month length issues
        /// </summary>
        private string ValidateSeasonalDates(ComboBoxItem startMonthItem, ComboBoxItem startDayItem, 
                                           ComboBoxItem endMonthItem, ComboBoxItem endDayItem)
        {
            var warnings = new List<string>();
            
            // Parse start date
            if (int.TryParse(startMonthItem.Tag?.ToString(), out int startMonth) &&
                int.TryParse(startDayItem.Tag?.ToString(), out int startDay))
            {
                var startDaysInMonth = GetDaysInMonth(startMonth);
                if (startDay > startDaysInMonth)
                {
                    string monthName = startMonthItem.Content?.ToString() ?? "Unknown";
                    warnings.Add($"{monthName} only has {startDaysInMonth} days, not {startDay}");
                }
                
                // Special check for February 29th in non-leap years
                if (startMonth == 2 && startDay == 29 && !DateTime.IsLeapYear(DateTime.Now.Year))
                {
                    warnings.Add($"February 29th does not exist in {DateTime.Now.Year} (not a leap year)");
                }
            }
            
            // Parse end date
            if (int.TryParse(endMonthItem.Tag?.ToString(), out int endMonth) &&
                int.TryParse(endDayItem.Tag?.ToString(), out int endDay))
            {
                var endDaysInMonth = GetDaysInMonth(endMonth);
                if (endDay > endDaysInMonth)
                {
                    string monthName = endMonthItem.Content?.ToString() ?? "Unknown";
                    warnings.Add($"{monthName} only has {endDaysInMonth} days, not {endDay}");
                }
                
                // Special check for February 29th in non-leap years
                if (endMonth == 2 && endDay == 29 && !DateTime.IsLeapYear(DateTime.Now.Year))
                {
                    warnings.Add($"February 29th does not exist in {DateTime.Now.Year} (not a leap year)");
                }
            }
            
            return warnings.Count > 0 ? string.Join(", ", warnings) : "";
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
            // Update day combo box based on selected month
            if (sender == StartMonthComboBox)
            {
                UpdateDayComboBoxForMonth(StartMonthComboBox, StartDayComboBox);
            }
            else if (sender == EndMonthComboBox)
            {
                UpdateDayComboBoxForMonth(EndMonthComboBox, EndDayComboBox);
            }
            
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

                // Add null checks for controls that might not be initialized yet during XAML loading
                if (StartMonthComboBox == null || StartDayComboBox == null || 
                    EndMonthComboBox == null || EndDayComboBox == null || 
                    SeasonPriorityTextBox == null || SeasonPreviewText == null)
                {

                    return;
                }

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
                    
                    // Validate dates
                    var dateValidation = ValidateSeasonalDates(startMonthItem, startDayItem, endMonthItem, endDayItem);
                    
                    // Check if it spans years
                    bool spansYears = string.Compare(startDate, endDate) > 0;
                    string spanText = spansYears ? " (spans New Year)" : "";
                    
                    string previewText = $"Active: {startMonthName} {startDayItem.Content} - {endMonthName} {endDayItem.Content}{spanText}{priorityText}";
                    
                    // Add validation warnings
                    if (!string.IsNullOrEmpty(dateValidation))
                    {
                        previewText += $"\n⚠️ {dateValidation}";
                    }
                    
                    SeasonPreviewText.Text = previewText;

                }
                else
                {
                    SeasonPreviewText.Text = "Set all dates to see preview";

                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Warning("Failed to update season preview: {Error}", ("Error", ex.Message));
                
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

        public static void ShowCategoryDialog(Window? owner = null, Action<bool>? onCategoriesChanged = null)
        {
            try
            {
                var dialog = new CategoryManagementDialog();

                if (owner != null)
                {
                    dialog.Owner = owner;
                }
                else
                {
                    dialog.Owner = Application.Current.MainWindow;
                }

                // Subscribe to dialog closed event to check if categories changed
                dialog.Closed += (s, e) => {
                    onCategoriesChanged?.Invoke(dialog.CategoriesChanged);
                };

                dialog.Show();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error showing category dialog", ex);
                throw;
            }
        }

        private async void LoadCategories()
        {
            try
            {


                var result = await _databaseService.GetAllTemplateCategoriesAsync();


                CategoriesListPanel.Children.Clear();

                if (result.Success && result.Data != null)
                {

                    foreach (var category in result.Data)
                    {

                        var categoryItem = CreateCategoryListItem(category);
                        CategoriesListPanel.Children.Add(categoryItem);

                    }

                }
                else
                {

                    NotificationService.Instance.ShowError("Loading Failed", $"Failed to load categories: {result.ErrorMessage}");
                }

            }
            catch (Exception ex)
            {


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


                string action = category.IsActive ? "deactivate" : "activate";

                // Get template count for this category

                var templatesResult = await _databaseService.GetTemplatesByCategoryAsync(category.Id);
                int templateCount = templatesResult.Success ? templatesResult.Data?.Count ?? 0 : 0;

                string message = $"Are you sure you want to {action} the category '{category.Name}'?";
                if (templateCount > 0)
                {
                    message += $"\n\nThis will also {action} {templateCount} template{(templateCount == 1 ? "" : "s")} in this category.";
                }

                bool confirmed = ConfirmationDialog.ShowConfirmation(
                    $"{char.ToUpper(action[0])}{action.Substring(1)} Category",
                    message,
                    char.ToUpper(action[0]) + action.Substring(1),
                    "Cancel",
                    this
                );

                if (confirmed)
                {

                    var result = await _databaseService.UpdateTemplateCategoryStatusAsync(category.Id, !category.IsActive);

                    if (result.Success)
                    {

                        CategoriesChanged = true;
                        LoadCategories();
                        LoggingService.Application.Information("Category {Action}: {CategoryName}", 
                            ("Action", action + "d"), ("CategoryName", category.Name));
                        
                        string successMessage = $"Category '{category.Name}' has been {action}d successfully!";
                        if (templateCount > 0)
                        {
                            successMessage += $"\n{templateCount} template{(templateCount == 1 ? "" : "s")} {(templateCount == 1 ? "was" : "were")} also {action}d.";
                        }

                        NotificationService.Instance.ShowSuccess($"Category {char.ToUpper(action[0])}{action.Substring(1)}d", successMessage);
                    }
                    else
                    {

                        NotificationService.Instance.ShowError($"Failed to {char.ToUpper(action[0])}{action.Substring(1)} Category", 
                            $"Could not {action} category '{category.Name}': {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {

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
 
