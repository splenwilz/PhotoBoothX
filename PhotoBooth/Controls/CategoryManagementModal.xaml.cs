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
        private static class ThemeColors
        {
            public const string ActiveBackground = "#FFFFFF";
            public const string InactiveBackground = "#F9FAFB";
            public const string ActiveBorder = "#E5E7EB";
            public const string InactiveBorder = "#D1D5DB";
            public const string ActiveText = "#374151";
            public const string InactiveText = "#9CA3AF";
            public const string SuccessBadge = "#10B981";
            public const string InactiveBadge = "#6B7280";
            public const string SecondaryText = "#6B7280";
            public const string PrimaryBlue = "#1E40AF";
            public const string DateControlText = "#4B5563";
            public const string LightBlue = "#BFDBFE";
            public const string MediumBlue = "#3B82F6";
            public const string LightGray = "#F8FAFC";
            public const string SlateGray = "#F1F5F9";
            public const string DropdownBorder = "#E2E8F0";
            public const string InfoBackground = "#DBEAFE";
        }

        private static System.Windows.Media.Brush GetBrushFromColor(string colorString, System.Windows.Media.Brush fallback)
        {
            return (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString(colorString) ?? fallback);
        }

        private static int GetDaysInMonth(int month)
        {
            return DateTime.DaysInMonth(REFERENCE_YEAR, month);
        }

        private const string DEFAULT_SEASON_START = "01-01";
        private const string DEFAULT_SEASON_END = "12-31";
        private const int REFERENCE_YEAR = 2023; // Non-leap year for consistent date validation

        private readonly IDatabaseService _databaseService;
        private TemplateCategory? _currentlyEditingCategory = null;
        private Border? _currentEditDropdown = null;

        public bool CategoriesChanged { get; private set; } = false;

        // Event to notify when categories are changed
        public event EventHandler<bool>? CategoriesChangedEvent;

        public CategoryManagementModal()
        {
            try
            {
                InitializeComponent();
                _databaseService = new DatabaseService();
                LoadCategories();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error initializing CategoryManagementModal", ex);
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

        private StackPanel CreateCategoryListItem(TemplateCategory category)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            
            // Create category border with styling
            var border = CreateCategoryBorder(category);
            
            // Create main grid layout
            var grid = CreateCategoryGrid(category);
            
            border.Child = grid;
            container.Children.Add(border);
            return container;
        }

        private Border CreateCategoryBorder(TemplateCategory category)
        {
            return new Border
            {
                Background = category.IsActive ? 
                    GetBrushFromColor(ThemeColors.ActiveBackground, System.Windows.Media.Brushes.White) : 
                    GetBrushFromColor(ThemeColors.InactiveBackground, System.Windows.Media.Brushes.WhiteSmoke),
                BorderBrush = category.IsActive ?
                    GetBrushFromColor(ThemeColors.ActiveBorder, System.Windows.Media.Brushes.LightGray) :
                    GetBrushFromColor(ThemeColors.InactiveBorder, System.Windows.Media.Brushes.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Tag = category,
                Opacity = category.IsActive ? 1.0 : 0.7
            };
        }

        private Grid CreateCategoryGrid(TemplateCategory category)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Create info panel (left side)
            var infoPanel = CreateCategoryInfo(category);
            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);

            // Create button panel (right side)
            var buttonPanel = CreateCategoryButtons(category);
            Grid.SetColumn(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            return grid;
        }

        private StackPanel CreateCategoryInfo(TemplateCategory category)
        {
            var infoPanel = new StackPanel();
            
            // Create name panel with seasonal indicator and status badge
            var namePanel = CreateCategoryNamePanel(category);
            infoPanel.Children.Add(namePanel);

            // Add description if available
            if (!string.IsNullOrEmpty(category.Description))
            {
                var descBlock = CreateDescriptionBlock(category);
                infoPanel.Children.Add(descBlock);
            }

            // Add seasonal info if applicable
            if (category.IsSeasonalCategory && !string.IsNullOrEmpty(category.SeasonStartDate) && !string.IsNullOrEmpty(category.SeasonEndDate))
            {
                var seasonalInfo = CreateSeasonalInfoBlock(category);
                infoPanel.Children.Add(seasonalInfo);
            }

            return infoPanel;
        }

        private StackPanel CreateCategoryNamePanel(TemplateCategory category)
        {
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Add seasonal emoji for seasonal categories
            if (category.IsSeasonalCategory)
            {
                var seasonalIcon = new TextBlock
                {
                    Text = "📅",
                    FontSize = 18,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                namePanel.Children.Add(seasonalIcon);
            }
            
            // Category name
            var nameBlock = new TextBlock
            {
                Text = category.Name,
                FontSize = 18,
                FontWeight = FontWeights.Medium,
                Foreground = category.IsActive ?
                    GetBrushFromColor(ThemeColors.ActiveText, System.Windows.Media.Brushes.Black) :
                    GetBrushFromColor(ThemeColors.InactiveText, System.Windows.Media.Brushes.Gray),
                VerticalAlignment = VerticalAlignment.Center
            };
            namePanel.Children.Add(nameBlock);
            
            // Status badge
            var statusBadge = CreateStatusBadge(category);
            namePanel.Children.Add(statusBadge);
            
            namePanel.Margin = new Thickness(0, 0, 0, 8);
            return namePanel;
        }

        private Border CreateStatusBadge(TemplateCategory category)
        {
            var statusBadge = new Border
            {
                Background = category.IsActive ? 
                    GetBrushFromColor(ThemeColors.SuccessBadge, System.Windows.Media.Brushes.Green) :
                    GetBrushFromColor(ThemeColors.InactiveBadge, System.Windows.Media.Brushes.Gray),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var statusText = new TextBlock
            {
                Text = category.IsActive ? "Active" : "Inactive",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = System.Windows.Media.Brushes.White
            };
            statusBadge.Child = statusText;
            
            return statusBadge;
        }

        private TextBlock CreateDescriptionBlock(TemplateCategory category)
        {
            return new TextBlock
            {
                Text = category.Description,
                FontSize = 14,
                Foreground = GetBrushFromColor(ThemeColors.SecondaryText, System.Windows.Media.Brushes.Gray),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private TextBlock CreateSeasonalInfoBlock(TemplateCategory category)
        {
            return new TextBlock
            {
                Text = $"Season: {FormatSeasonDate(category.SeasonStartDate ?? "")} - {FormatSeasonDate(category.SeasonEndDate ?? "")}",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrushFromColor(ThemeColors.PrimaryBlue, System.Windows.Media.Brushes.DarkBlue),
                Margin = new Thickness(0, 0, 0, 0)
            };
        }

        private StackPanel CreateCategoryButtons(TemplateCategory category)
        {
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Only show edit button for seasonal categories
            if (category.IsSeasonalCategory)
            {
                var editButton = CreateEditDatesButton(category);
                buttonPanel.Children.Add(editButton);
            }

            // Toggle Active/Inactive Button  
            var toggleButton = CreateToggleButton(category);
            buttonPanel.Children.Add(toggleButton);

            return buttonPanel;
        }

        private Button CreateEditDatesButton(TemplateCategory category)
        {
            var editButton = new Button
            {
                Content = "EDIT DATES",
                Style = (Style)FindResource("ModernActionButtonStyle"),
                Background = GetBrushFromColor(ThemeColors.LightBlue, System.Windows.Media.Brushes.LightBlue),
                Foreground = GetBrushFromColor(ThemeColors.PrimaryBlue, System.Windows.Media.Brushes.DarkBlue),
                FontSize = 14,
                Height = 44,
                MinWidth = 120,
                ToolTip = "Edit the date range for this seasonal category"
            };
            editButton.Click += (s, e) => EditCategoryDates(category);
            return editButton;
        }

        private Button CreateToggleButton(TemplateCategory category)
        {
            var toggleButton = new Button
            {
                Content = category.IsActive ? "DISABLE" : "ENABLE",
                Style = (Style)FindResource("ModernActionButtonStyle"),
                Background = category.IsActive ? 
                    GetBrushFromColor(ThemeColors.ActiveBorder, System.Windows.Media.Brushes.LightGray) :
                    GetBrushFromColor(ThemeColors.LightBlue, System.Windows.Media.Brushes.LightBlue),
                Foreground = GetBrushFromColor(ThemeColors.ActiveText, System.Windows.Media.Brushes.DarkGray),
                FontSize = 14,
                Height = 44,
                MinWidth = 100,
                ToolTip = category.IsActive ? "Disable this category" : "Enable this category"
            };
            toggleButton.Click += (s, e) => ToggleCategory(category);
            return toggleButton;
        }

        private string FormatSeasonDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString)) return "";
            
            var parts = dateString.Split('-');
            if (parts.Length != 2) return dateString;
            
            if (int.TryParse(parts[0], out int month) && int.TryParse(parts[1], out int day))
            {
                var monthNames = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                if (month >= 1 && month <= 12)
                {
                    return $"{monthNames[month - 1]} {day}";
                }
            }
            
            return dateString;
        }

        private void EditCategoryDates(TemplateCategory category)
        {
            try
            {
                // If we're already editing this category, close the dropdown
                if (_currentlyEditingCategory?.Id == category.Id && _currentEditDropdown != null)
                {
                    CloseEditDropdown();
                    return;
                }

                // Close any existing dropdown first
                CloseEditDropdown();

                // Find the container for this category
                var container = FindCategoryContainer(category);
                if (container == null) return;

                // Create and show the dropdown
                _currentEditDropdown = CreateEditDropdown(category);
                _currentlyEditingCategory = category;
                
                container.Children.Add(_currentEditDropdown);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error showing edit dropdown", ex);
                NotificationService.Instance.ShowError("Error", "Error showing date editor. Please try again.");
            }
        }

        private StackPanel? FindCategoryContainer(TemplateCategory category)
        {
            // Find the container for this specific category
            foreach (var child in CategoriesListPanel.Children)
            {
                if (child is StackPanel container)
                {
                    // Check if this container's first child (the border) has our category
                    if (container.Children.Count > 0 && container.Children[0] is Border border)
                    {
                        if (border.Tag is TemplateCategory cat && cat.Id == category.Id)
                        {
                            return container;
                        }
                    }
                }
            }
            return null;
        }

        private void CloseEditDropdown()
        {
            if (_currentEditDropdown != null && _currentlyEditingCategory != null)
            {
                var container = FindCategoryContainer(_currentlyEditingCategory);
                if (container != null && container.Children.Contains(_currentEditDropdown))
                {
                    container.Children.Remove(_currentEditDropdown);
                }
                
                _currentEditDropdown = null;
                _currentlyEditingCategory = null;
            }
        }

        private Border CreateEditDropdown(TemplateCategory category)
        {
            // Create dropdown container
            var dropdownBorder = CreateDropdownContainer();
            
            // Create main panel with all content
            var mainPanel = CreateDropdownMainPanel(category);
            
            dropdownBorder.Child = mainPanel;
            return dropdownBorder;
        }

        private Border CreateDropdownContainer()
        {
            return new Border
            {
                Background = GetBrushFromColor(ThemeColors.LightGray, System.Windows.Media.Brushes.AliceBlue),
                BorderBrush = GetBrushFromColor(ThemeColors.DropdownBorder, System.Windows.Media.Brushes.LightGray),
                BorderThickness = new Thickness(1, 0, 1, 1),
                CornerRadius = new CornerRadius(0, 0, 8, 8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, -1, 0, 0)
            };
        }

        private StackPanel CreateDropdownMainPanel(TemplateCategory category)
        {
            var mainPanel = new StackPanel();
            
            // Create header section
            var headerGrid = CreateDropdownHeader(category);
            mainPanel.Children.Add(headerGrid);

            // Create current dates display
            var currentInfo = CreateCurrentDateDisplay(category);
            mainPanel.Children.Add(currentInfo);

            // Create date editing controls
            var dateEditGrid = CreateDateEditGrid(category);
            mainPanel.Children.Add(dateEditGrid);

            // Create action buttons
            var buttonPanel = CreateDropdownActions(category);
            mainPanel.Children.Add(buttonPanel);

            return mainPanel;
        }

        private Grid CreateDropdownHeader(TemplateCategory category)
        {
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = $"📅 Edit {category.Name} Dates",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrushFromColor(ThemeColors.ActiveText, System.Windows.Media.Brushes.Black),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            headerGrid.Children.Add(titleBlock);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = GetBrushFromColor(ThemeColors.ActiveBorder, System.Windows.Media.Brushes.LightGray),
                Foreground = GetBrushFromColor(ThemeColors.ActiveText, System.Windows.Media.Brushes.DarkGray),
                BorderThickness = new Thickness(0),
                Template = CreateRoundButtonTemplate()
            };
            closeBtn.Click += (s, e) => CloseEditDropdown();
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(closeBtn);

            return headerGrid;
        }

        private Border CreateCurrentDateDisplay(TemplateCategory category)
        {
            var currentInfo = new Border
            {
                Background = GetBrushFromColor(ThemeColors.InfoBackground, System.Windows.Media.Brushes.LightBlue),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var currentText = new TextBlock
            {
                Text = $"Current: {FormatSeasonDate(category.SeasonStartDate ?? "")} - {FormatSeasonDate(category.SeasonEndDate ?? "")}",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrushFromColor(ThemeColors.PrimaryBlue, System.Windows.Media.Brushes.DarkBlue),
                TextAlignment = TextAlignment.Center
            };
            currentInfo.Child = currentText;
            
            return currentInfo;
        }

        private Grid CreateDateEditGrid(TemplateCategory category)
        {
            // Parse current dates
            var (startMonth, startDay, endMonth, endDay) = ParseCategoryDates(category);

            // Create compact date controls for dropdown
            TextBlock startMonthLabel, startDayLabel, endMonthLabel, endDayLabel;
            var startDatePanel = CreateCompactDateControl("Season Start", startMonth, startDay, true, out startMonthLabel, out startDayLabel);
            var endDatePanel = CreateCompactDateControl("Season End", endMonth, endDay, false, out endMonthLabel, out endDayLabel);

            // Store references for save handler
            _currentStartMonthLabel = startMonthLabel;
            _currentStartDayLabel = startDayLabel;
            _currentEndMonthLabel = endMonthLabel;
            _currentEndDayLabel = endDayLabel;

            // Create date editing grid
            var dateEditGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            dateEditGrid.ColumnDefinitions.Add(new ColumnDefinition());
            dateEditGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            dateEditGrid.ColumnDefinitions.Add(new ColumnDefinition());

            Grid.SetColumn(startDatePanel, 0);
            Grid.SetColumn(endDatePanel, 2);
            dateEditGrid.Children.Add(startDatePanel);
            dateEditGrid.Children.Add(endDatePanel);

            return dateEditGrid;
        }

        private (int startMonth, int startDay, int endMonth, int endDay) ParseCategoryDates(TemplateCategory category)
        {
            var startParts = (category.SeasonStartDate ?? DEFAULT_SEASON_START).Split('-');
            var endParts = (category.SeasonEndDate ?? DEFAULT_SEASON_END).Split('-');
            
            int startMonth = int.TryParse(startParts[0], out var sm) ? sm : 1;
            int startDay = int.TryParse(startParts[1], out var sd) ? sd : 1;
            int endMonth = int.TryParse(endParts[0], out var em) ? em : 12;
            int endDay = int.TryParse(endParts[1], out var ed) ? ed : 31;

            return (startMonth, startDay, endMonth, endDay);
        }

        private StackPanel CreateDropdownActions(TemplateCategory category)
        {
            var buttonPanel = new StackPanel { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            var saveButton = CreateSaveButton(category);
            var cancelButton = CreateCancelButton();
            
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            
            return buttonPanel;
        }

        private Button CreateSaveButton(TemplateCategory category)
        {
            var saveButton = new Button
            {
                Content = "💾 Save",
                Width = 120,
                Height = 40,
                Margin = new Thickness(0, 0, 10, 0),
                Background = GetBrushFromColor(ThemeColors.LightBlue, System.Windows.Media.Brushes.LightBlue),
                Foreground = GetBrushFromColor(ThemeColors.PrimaryBlue, System.Windows.Media.Brushes.DarkBlue),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                BorderThickness = new Thickness(0),
                Template = CreateModernButtonTemplate()
            };
            
            saveButton.Click += async (s, e) => await SaveCategoryDates(category);
            return saveButton;
        }

        private Button CreateCancelButton()
        {
            var cancelButton = new Button
            {
                Content = "❌ Cancel",
                Width = 100,
                Height = 40,
                Background = GetBrushFromColor(ThemeColors.ActiveBorder, System.Windows.Media.Brushes.LightGray),
                Foreground = GetBrushFromColor(ThemeColors.ActiveText, System.Windows.Media.Brushes.DarkGray),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                BorderThickness = new Thickness(0),
                Template = CreateModernButtonTemplate()
            };
            
            cancelButton.Click += (s, e) => CloseEditDropdown();
            return cancelButton;
        }

        // Store references to current date controls for save handler
        private TextBlock? _currentStartMonthLabel;
        private TextBlock? _currentStartDayLabel;
        private TextBlock? _currentEndMonthLabel;
        private TextBlock? _currentEndDayLabel;

        private async Task SaveCategoryDates(TemplateCategory category)
        {
            try
            {
                if (!ValidateCurrentDateControls())
                {
                    return;
                }

                var (startMonth, startDay, endMonth, endDay) = ExtractDateValues();
                var (newStartDate, newEndDate) = FormatDateStrings(startMonth, startDay, endMonth, endDay);
                
                await UpdateCategoryInDatabase(category, newStartDate, newEndDate);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error saving category dates", ex);
                NotificationService.Instance.ShowError("Save Error", $"Error saving dates: {ex.Message}");
            }
        }

        private bool ValidateCurrentDateControls()
        {
            if (_currentStartMonthLabel == null || _currentStartDayLabel == null || 
                _currentEndMonthLabel == null || _currentEndDayLabel == null)
            {
                NotificationService.Instance.ShowError("Save Error", "Control references are invalid. Please try again.");
                return false;
            }
            return true;
        }

        private (int startMonth, int startDay, int endMonth, int endDay) ExtractDateValues()
        {
            int startMonth = (int)_currentStartMonthLabel!.Tag;
            int startDay = (int)_currentStartDayLabel!.Tag;
            int endMonth = (int)_currentEndMonthLabel!.Tag;
            int endDay = (int)_currentEndDayLabel!.Tag;
            
            return (startMonth, startDay, endMonth, endDay);
        }

        private (string startDate, string endDate) FormatDateStrings(int startMonth, int startDay, int endMonth, int endDay)
        {
            string newStartDate = $"{startMonth:D2}-{startDay:D2}";
            string newEndDate = $"{endMonth:D2}-{endDay:D2}";
            
            return (newStartDate, newEndDate);
        }

        private async Task UpdateCategoryInDatabase(TemplateCategory category, string newStartDate, string newEndDate)
                {
                    var result = await _databaseService.UpdateTemplateCategoryAsync(
                category.Id,
                category.Name,
                category.Description ?? "",
                category.IsSeasonalCategory,
                newStartDate,
                newEndDate,
                category.SeasonalPriority
            );
                        
                    if (result.Success)
                    {
                CategoriesChanged = true;
                LoadCategories();
                NotificationService.Instance.ShowSuccess("Dates Updated", $"Season dates for {category.Name} updated successfully!");
                CloseEditDropdown();
                    }
                    else
                    {
                NotificationService.Instance.ShowError("Update Failed", $"Failed to update dates: {result.ErrorMessage}");
            }
        }

        private StackPanel CreateCompactDateControl(string title, int initialMonth, int initialDay, bool isStart, out TextBlock monthLabel, out TextBlock dayLabel)
        {
            var mainPanel = new StackPanel();
            
            // Create title section
            var titleBlock = CreateDateControlTitle(title);
            mainPanel.Children.Add(titleBlock);

            // Create month control
            var monthControl = CreateMonthControl(initialMonth, out monthLabel);
            mainPanel.Children.Add(monthControl);

            // Create day control
            var dayControl = CreateDayControl(initialDay, monthLabel, out dayLabel);
            mainPanel.Children.Add(dayControl);

            return mainPanel;
        }

        private TextBlock CreateDateControlTitle(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = GetBrushFromColor(ThemeColors.DateControlText, System.Windows.Media.Brushes.DarkGray),
                Margin = new Thickness(0, 0, 0, 15)
            };
        }

        private Grid CreateMonthControl(int initialMonth, out TextBlock monthLabel)
        {
            var monthNames = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            
            var monthGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            monthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
            monthGrid.ColumnDefinitions.Add(new ColumnDefinition());
            monthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

            var monthLeftBtn = CreateNavigationButton("←", ThemeColors.LightBlue);
            var monthDisplay = CreateDisplayBorder(ThemeColors.LightGray);
            var monthRightBtn = CreateNavigationButton("→", ThemeColors.LightBlue);

            monthLabel = new TextBlock
            {
                Text = monthNames[initialMonth - 1],
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = GetBrushFromColor(ThemeColors.PrimaryBlue, System.Windows.Media.Brushes.DarkBlue),
                Tag = initialMonth
            };
            monthDisplay.Child = monthLabel;

            // Set up month navigation event handlers
            SetupMonthNavigation(monthLeftBtn, monthRightBtn, monthLabel, monthNames);

            Grid.SetColumn(monthLeftBtn, 0);
            Grid.SetColumn(monthDisplay, 1);
            Grid.SetColumn(monthRightBtn, 2);
            monthGrid.Children.Add(monthLeftBtn);
            monthGrid.Children.Add(monthDisplay);
            monthGrid.Children.Add(monthRightBtn);

            return monthGrid;
        }

        private Grid CreateDayControl(int initialDay, TextBlock monthLabel, out TextBlock dayLabel)
        {
            var dayGrid = new Grid();
            dayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
            dayGrid.ColumnDefinitions.Add(new ColumnDefinition());
            dayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

            var dayLeftBtn = CreateNavigationButton("←", ThemeColors.MediumBlue);
            var dayDisplay = CreateDisplayBorder(ThemeColors.SlateGray);
            var dayRightBtn = CreateNavigationButton("→", ThemeColors.MediumBlue);

            dayLabel = new TextBlock
            {
                Text = initialDay.ToString(),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = GetBrushFromColor(ThemeColors.PrimaryBlue, System.Windows.Media.Brushes.DarkGray),
                Tag = initialDay
            };
            dayDisplay.Child = dayLabel;

            // Set up day navigation event handlers
            SetupDayNavigation(dayLeftBtn, dayRightBtn, dayLabel, monthLabel);

            Grid.SetColumn(dayLeftBtn, 0);
            Grid.SetColumn(dayDisplay, 1);
            Grid.SetColumn(dayRightBtn, 2);
            dayGrid.Children.Add(dayLeftBtn);
            dayGrid.Children.Add(dayDisplay);
            dayGrid.Children.Add(dayRightBtn);

            return dayGrid;
        }

        private Button CreateNavigationButton(string content, string backgroundColor)
        {
            return new Button
            {
                Content = content,
                Width = 45,
                Height = 45,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Background = GetBrushFromColor(backgroundColor, System.Windows.Media.Brushes.Blue),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Template = CreateRoundButtonTemplate()
            };
        }

        private Border CreateDisplayBorder(string backgroundColor)
        {
            return new Border
            {
                Background = GetBrushFromColor(backgroundColor, System.Windows.Media.Brushes.LightBlue),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(5, 0, 5, 0)
            };
        }

        private void SetupMonthNavigation(Button leftBtn, Button rightBtn, TextBlock monthLabel, string[] monthNames)
        {
            leftBtn.Click += (s, e) =>
            {
                int currentMonth = (int)monthLabel.Tag;
                int newMonth = currentMonth == 1 ? 12 : currentMonth - 1;
                monthLabel.Tag = newMonth;
                monthLabel.Text = monthNames[newMonth - 1];
            };

            rightBtn.Click += (s, e) =>
            {
                int currentMonth = (int)monthLabel.Tag;
                int newMonth = currentMonth == 12 ? 1 : currentMonth + 1;
                monthLabel.Tag = newMonth;
                monthLabel.Text = monthNames[newMonth - 1];
            };
        }

        private void SetupDayNavigation(Button leftBtn, Button rightBtn, TextBlock dayLabel, TextBlock monthLabel)
        {
            leftBtn.Click += (s, e) =>
            {
                int currentDay = (int)dayLabel.Tag;
                int newDay = currentDay == 1 ? 31 : currentDay - 1;
                
                // Basic validation for days in month using reference year for consistent behavior
                int currentMonth = (int)monthLabel.Tag;
                var daysInMonth = GetDaysInMonth(currentMonth);
                if (newDay > daysInMonth) newDay = daysInMonth;
                
                dayLabel.Tag = newDay;
                dayLabel.Text = newDay.ToString();
            };

            rightBtn.Click += (s, e) =>
            {
                int currentDay = (int)dayLabel.Tag;
                int currentMonth = (int)monthLabel.Tag;
                var daysInMonth = GetDaysInMonth(currentMonth);
                
                int newDay = currentDay == daysInMonth ? 1 : currentDay + 1;
                
                dayLabel.Tag = newDay;
                dayLabel.Text = newDay.ToString();
            };
        }



        private ControlTemplate CreateButtonTemplate(double cornerRadius = 25, bool includeEffects = false, bool includePremiumEffects = false)
        {
            var template = new ControlTemplate(typeof(Button));
            
            // Create the visual tree
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            // Add hover effects
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, includeEffects ? 0.9 : 0.85));
            
            if (includeEffects)
            {
                hoverTrigger.Setters.Add(new Setter(Control.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 3,
                    Opacity = 0.3
                }));
            }
            
            if (includePremiumEffects)
            {
                hoverTrigger.Setters.Add(new Setter(FrameworkElement.RenderTransformProperty, new ScaleTransform(1.05, 1.05)));
            }
            
            template.Triggers.Add(hoverTrigger);
            
            var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7));
            
            if (includeEffects)
            {
                pressTrigger.Setters.Add(new Setter(FrameworkElement.RenderTransformProperty, new ScaleTransform(0.98, 0.98)));
            }
            
            if (includePremiumEffects)
            {
                pressTrigger.Setters.Add(new Setter(FrameworkElement.RenderTransformProperty, new ScaleTransform(0.95, 0.95)));
            }
            
            template.Triggers.Add(pressTrigger);
            
            return template;
        }

        private ControlTemplate CreateRoundButtonTemplate() => CreateButtonTemplate(25, false, false);
        private ControlTemplate CreateModernButtonTemplate() => CreateButtonTemplate(15, true, false);
        private ControlTemplate CreatePremiumButtonTemplate() => CreateButtonTemplate(35, false, true);

        private DateTime ParseSeasonDate(string seasonDate, int year)
        {
            try
            {
                var parts = seasonDate.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int month) && int.TryParse(parts[1], out int day))
                {
                    return new DateTime(year, month, day);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error parsing season date", ex);
            }
            
            return DateTime.Now;
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
                    // Update the category status (this also updates all templates in the category)
                    var result = await _databaseService.UpdateTemplateCategoryStatusAsync(category.Id, !category.IsActive);
                    
                    if (result.Success)
                    {
                        CategoriesChanged = true;
                        LoadCategories();
                        LoggingService.Application.Information("Category {Action}: {CategoryName} with {TemplateCount} templates", 
                            ("Action", action + "d"), ("CategoryName", category.Name), ("TemplateCount", templateCount));
                        
                        string successMessage = $"Category '{category.Name}' has been {action}d successfully!";
                        if (templateCount > 0)
                        {
                            successMessage += $"\n{templateCount} template{(templateCount == 1 ? "" : "s")} {(templateCount == 1 ? "was" : "were")} also {action}d.";
                        }
                        
                        NotificationService.Instance.ShowSuccess($"Category {char.ToUpper(action[0])}{action.Substring(1)}d", successMessage);
                    }
                    else
                    {
                        NotificationService.Instance.ShowError($"{char.ToUpper(action[0])}{action.Substring(1)} Failed", $"Failed to {action} category: {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error toggling category status", ex);
                NotificationService.Instance.ShowError("Toggle Error", "Error changing category status. Please try again.");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Close button clicked - starting close process");
                
                CategoriesChangedEvent?.Invoke(this, CategoriesChanged);
                
                // Close any open dropdown first
                CloseEditDropdown();
                
                if (!TryCloseModal())
                {
                    LoggingService.Application.Warning("Failed to close modal through normal methods");
                    HandleModalCloseError();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error closing category modal", ex);
                HandleModalCloseError();
            }
        }

        private bool TryCloseModal()
        {
            if (ModalService.Instance.IsModalShown)
            {
                ModalService.Instance.HideModal();
                return true;
            }
            else
            {
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null && parentWindow != Application.Current.MainWindow)
                {
                    parentWindow.DialogResult = false;
                    return true;
                }
            }
            return false;
        }

        private void HandleModalCloseError()
        {
            // Fallback: try ModalService first, then window methods
            try
            {
                if (ModalService.Instance.IsModalShown)
                {
                    ModalService.Instance.HideModal();
                }
                else
                {
                    var parentWindow = Window.GetWindow(this);
                    parentWindow?.Hide();
                }
            }
            catch
            {
                // Last resort - ignore secondary errors to prevent infinite loops
            }
        }


    }
} 
