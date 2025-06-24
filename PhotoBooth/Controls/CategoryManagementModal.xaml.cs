using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth.Controls
{
    public partial class CategoryManagementModal : UserControl
    {
        private readonly IDatabaseService _databaseService;
        private int _editingCategoryId = -1;
        private bool _isEditMode = false;
        private ScrollViewer? _dialogScrollViewer;

        public bool CategoriesChanged { get; private set; } = false;

        // Event to notify when categories are changed
        public event EventHandler<bool>? CategoriesChangedEvent;

        public CategoryManagementModal()
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

        private void PopulateDayComboBoxes()
        {
            try
            {

                // Populate months
                var months = new string[]
                {
                    "January", "February", "March", "April", "May", "June",
                    "July", "August", "September", "October", "November", "December"
                };

                StartMonthComboBox.ItemsSource = months;
                EndMonthComboBox.ItemsSource = months;

                // Populate days (initial population with 31 days)

                PopulateDaysForMonth(StartDayComboBox, 31);
                PopulateDaysForMonth(EndDayComboBox, 31);


            }
            catch
            {


            }
        }

        private void PopulateDaysForMonth(ComboBox dayComboBox, int daysInMonth)
        {
            try
            {
                // Store current selection
                int currentSelection = dayComboBox.SelectedIndex;
                
                // Clear and repopulate
                dayComboBox.Items.Clear();
                for (int i = 1; i <= daysInMonth; i++)
                {
                    dayComboBox.Items.Add(i.ToString());
                }
                
                // Restore selection if valid
                if (currentSelection >= 0 && currentSelection < daysInMonth)
                {
                    dayComboBox.SelectedIndex = currentSelection;
                }
                else if (currentSelection >= daysInMonth)
                {
                    // If previous selection was beyond the new month's days, select the last valid day
                    dayComboBox.SelectedIndex = daysInMonth - 1;
                    
                    // Show validation warning
                    if (dayComboBox == StartDayComboBox || dayComboBox == EndDayComboBox)
                    {
                        NotificationService.Instance.ShowWarning("Date Adjusted", 
                            $"Day adjusted to {daysInMonth} (last day of selected month)");
                    }
                }
            }
            catch
            {

            }
        }

        private int GetDaysInMonth(int monthIndex)
        {
            // monthIndex is 0-based (0 = January, 11 = December)
            if (monthIndex >= 0 && monthIndex < 12)
            {
                // Use current year or a leap year for maximum days
                return DateTime.DaysInMonth(DateTime.Now.Year, monthIndex + 1);
            }
            
            return 31; // Default fallback
        }

        private void StartMonthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (StartMonthComboBox.SelectedIndex >= 0)
                {
                    int daysInMonth = GetDaysInMonth(StartMonthComboBox.SelectedIndex);
                    PopulateDaysForMonth(StartDayComboBox, daysInMonth);
                    UpdateSeasonPreview();
                }
            }
            catch
            {

            }
        }

        private void EndMonthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (EndMonthComboBox.SelectedIndex >= 0)
                {
                    int daysInMonth = GetDaysInMonth(EndMonthComboBox.SelectedIndex);
                    PopulateDaysForMonth(EndDayComboBox, daysInMonth);
                    UpdateSeasonPreview();
                }
            }
            catch
            {

            }
        }

        private void DateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSeasonPreview();
        }

        private void UpdateSeasonPreview()
        {
            try
            {
                if (StartMonthComboBox.SelectedIndex >= 0 && StartDayComboBox.SelectedIndex >= 0 &&
                    EndMonthComboBox.SelectedIndex >= 0 && EndDayComboBox.SelectedIndex >= 0)
                {
                    string startMonth = StartMonthComboBox.SelectedItem?.ToString() ?? "";
                    string startDay = (StartDayComboBox.SelectedIndex + 1).ToString();
                    string endMonth = EndMonthComboBox.SelectedItem?.ToString() ?? "";
                    string endDay = (EndDayComboBox.SelectedIndex + 1).ToString();
                    
                    SeasonPreviewText.Text = $"Active from {startMonth} {startDay} to {endMonth} {endDay}";
                }
                else
                {
                    SeasonPreviewText.Text = "Set dates to see preview";
                }
            }
            catch
            {

            }
        }

        private void IsSeasonalCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SeasonalSettingsPanel.Visibility = Visibility.Visible;
            UpdateSeasonPreview();
        }

        private void IsSeasonalCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SeasonalSettingsPanel.Visibility = Visibility.Collapsed;
        }

        private void CategoryDescription_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (DescriptionCharCount != null && CategoryDescriptionTextBox != null)
            {
                int charCount = CategoryDescriptionTextBox.Text?.Length ?? 0;
                DescriptionCharCount.Text = $"{charCount}/200";
                
                // Change color based on character count
                if (charCount > 180)
                {
                    DescriptionCharCount.Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#EF4444") ?? System.Windows.Media.Brushes.Red);
                }
                else if (charCount > 150)
                {
                    DescriptionCharCount.Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#F59E0B") ?? System.Windows.Media.Brushes.Orange);
                }
                else
                {
                    DescriptionCharCount.Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#9CA3AF") ?? System.Windows.Media.Brushes.Gray);
                }
            }
        }

        private void SeasonPriority_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            try
            {
                // Only allow numeric input
                if (!char.IsDigit(e.Text, 0))
                {
                    e.Handled = true;
                    return;
                }

                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    // Get what the text would be after this input
                    string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                    
                    // Remove any selected text
                    if (textBox.SelectionLength > 0)
                    {
                        newText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
                        newText = newText.Insert(textBox.SelectionStart, e.Text);
                    }
                    
                    // Check if the resulting number would be valid (1-100)
                    if (int.TryParse(newText, out int value))
                    {
                        if (value < 1 || value > 100)
                        {
                            e.Handled = true;
                        }
                    }
                    else
                    {
                        e.Handled = true;
                    }
                }
            }
            catch
            {

                e.Handled = true;
            }
        }

        private void SeasonPriority_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    // If text is empty, that's okay (will default to 100)
                    if (string.IsNullOrEmpty(textBox.Text))
                    {
                        return;
                    }

                    // Validate the current text
                    if (int.TryParse(textBox.Text, out int value))
                    {
                        if (value < 1 || value > 100)
                        {
                            // Invalid value, revert to previous valid value or default
                            textBox.Text = "100";
                            textBox.SelectionStart = textBox.Text.Length;
                        }
                    }
                    else
                    {
                        // Not a valid number, revert to default
                        textBox.Text = "100";
                        textBox.SelectionStart = textBox.Text.Length;
                    }
                }
            }
            catch
            {

            }
        }

        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(CategoryNameTextBox.Text))
                {
                    NotificationService.Instance.ShowError("Validation Error", "Category name is required.");
                    CategoryNameTextBox.Focus();
                    return;
                }

                // Prepare seasonal data
                bool isSeasonal = IsSeasonalCheckBox.IsChecked == true;
                string? seasonStartDate = null;
                string? seasonEndDate = null;
                int seasonalPriority = 100;

                if (isSeasonal)
                {
                    // Validate seasonal fields
                    if (StartMonthComboBox.SelectedIndex < 0 || StartDayComboBox.SelectedIndex < 0 ||
                        EndMonthComboBox.SelectedIndex < 0 || EndDayComboBox.SelectedIndex < 0)
                    {
                        NotificationService.Instance.ShowError("Validation Error", "Please select complete start and end dates for seasonal category.");
                        return;
                    }

                    seasonStartDate = $"{StartMonthComboBox.SelectedIndex + 1:D2}-{StartDayComboBox.SelectedIndex + 1:D2}";
                    seasonEndDate = $"{EndMonthComboBox.SelectedIndex + 1:D2}-{EndDayComboBox.SelectedIndex + 1:D2}";
                    
                    if (!int.TryParse(SeasonPriorityTextBox.Text, out seasonalPriority))
                    {
                        seasonalPriority = 100;
                    }
                    
                    // Validate priority range
                    if (seasonalPriority < 1 || seasonalPriority > 100)
                    {
                        NotificationService.Instance.ShowError("Validation Error", "Season priority must be between 1 and 100.");
                        SeasonPriorityTextBox.Focus();
                        return;
                    }
                }

                // Save category
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

        private async void LoadCategories()
        {
            try
            {


                var result = await _databaseService.GetAllTemplateCategoriesAsync();
                int dataCount = result.Data?.Count ?? 0;


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
                // Parse season dates (format: MM-DD)
                var startParts = category.SeasonStartDate.Split('-');
                var endParts = category.SeasonEndDate.Split('-');
                
                if (startParts.Length == 2 && endParts.Length == 2 &&
                    int.TryParse(startParts[0], out int startMonth) && int.TryParse(startParts[1], out int startDay) &&
                    int.TryParse(endParts[0], out int endMonth) && int.TryParse(endParts[1], out int endDay))
                {
                    SetSeasonalData(startMonth.ToString(), startDay.ToString(), endMonth.ToString(), endDay.ToString(), category.SeasonalPriority);
                }
            }
            
            // Update UI for edit mode
            CancelEditButton.Visibility = Visibility.Visible;
            ((TextBlock)((StackPanel)AddButtonContent).Children[1]).Text = "Update Category";
            ((TextBlock)((StackPanel)AddButtonContent).Children[0]).Text = "💾";
            
            // Scroll to the form
            if (_dialogScrollViewer != null)
            {
                _dialogScrollViewer.ScrollToTop();
            }
            
            // Focus on the name field for immediate editing
            CategoryNameTextBox.Focus();
            CategoryNameTextBox.SelectAll();
        }

        private void SetSeasonalData(string startMonth, string startDay, string endMonth, string endDay, int priority)
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

        private async void ToggleCategory(TemplateCategory category)
        {
            try
            {
                string action = category.IsActive ? "deactivate" : "activate";
                
                // Get template count for this category
                var templatesResult = await _databaseService.GetTemplatesByCategoryAsync(category.Id);
                int templateCount = templatesResult.Success && templatesResult.Data != null ? templatesResult.Data.Count : 0;
                
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
                    Window.GetWindow(this)
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
                bool confirmed = ConfirmationDialog.ShowDeleteConfirmation(category.Name, "category", Window.GetWindow(this));
                
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
            // Fire the event to notify about changes
            CategoriesChangedEvent?.Invoke(this, CategoriesChanged);
            
            // Close the modal using ModalService
            ModalService.Instance.HideModal();
        }

        /// <summary>
        /// Static method to show the modal
        /// </summary>
        public static void ShowModal()
        {
            try
            {

                var modal = new CategoryManagementModal();
                ModalService.Instance.ShowModal(modal);

            }
            catch
            {


                throw;
            }
        }
    }
} 
