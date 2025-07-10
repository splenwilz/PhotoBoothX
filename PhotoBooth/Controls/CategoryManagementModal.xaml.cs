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
            
            var border = new Border
            {
                Background = category.IsActive ? 
                    System.Windows.Media.Brushes.White : 
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#F9FAFB") ?? System.Windows.Media.Brushes.WhiteSmoke), // Slightly gray background for disabled
                BorderBrush = category.IsActive ?
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#E5E7EB") ?? System.Windows.Media.Brushes.LightGray) :
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#D1D5DB") ?? System.Windows.Media.Brushes.Gray), // Darker border for disabled
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Tag = category, // Store category reference for easy access
                Opacity = category.IsActive ? 1.0 : 0.7 // Slightly transparent for disabled categories
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoPanel = new StackPanel();
            
            // Category name with seasonal indicator
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
            
            var nameBlock = new TextBlock
            {
                Text = category.Name,
                FontSize = 18,
                FontWeight = FontWeights.Medium,
                Foreground = category.IsActive ?
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#374151") ?? System.Windows.Media.Brushes.Black) :
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#9CA3AF") ?? System.Windows.Media.Brushes.Gray), // Grayed out for disabled
                VerticalAlignment = VerticalAlignment.Center
            };
            namePanel.Children.Add(nameBlock);
            
            // Status badge
            var statusBadge = new Border
            {
                Background = category.IsActive ? 
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#10B981") ?? System.Windows.Media.Brushes.Green) :
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#6B7280") ?? System.Windows.Media.Brushes.Gray),
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
            namePanel.Children.Add(statusBadge);
            namePanel.Margin = new Thickness(0, 0, 0, 8);
            
            infoPanel.Children.Add(namePanel);

            // Show description if available
            if (!string.IsNullOrEmpty(category.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = category.Description,
                    FontSize = 14,
                    Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom("#6B7280") ?? System.Windows.Media.Brushes.Gray),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                infoPanel.Children.Add(descBlock);
            }

            // Show seasonal info if it's a seasonal category
            if (category.IsSeasonalCategory && !string.IsNullOrEmpty(category.SeasonStartDate) && !string.IsNullOrEmpty(category.SeasonEndDate))
            {
                var seasonalInfo = new TextBlock
                {
                    Text = $"Season: {FormatSeasonDate(category.SeasonStartDate)} - {FormatSeasonDate(category.SeasonEndDate)}",
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#1E40AF") ?? System.Windows.Media.Brushes.DarkBlue),     // Dark blue for better contrast with light theme
                    Margin = new Thickness(0, 0, 0, 0)
                };
                infoPanel.Children.Add(seasonalInfo);
            }

            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);

            // Action buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Only show edit button for seasonal categories
            if (category.IsSeasonalCategory)
            {
                var editButton = new Button
                {
                    Content = "EDIT DATES",
                    Style = (Style)FindResource("ModernActionButtonStyle"),
                    Background = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#BFDBFE") ?? System.Windows.Media.Brushes.LightBlue), // Very light blue
                    Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#1E40AF") ?? System.Windows.Media.Brushes.DarkBlue), // Dark blue text
                    FontSize = 14,
                    Height = 44,
                    MinWidth = 120,
                    ToolTip = "Edit the date range for this seasonal category"
                };
                editButton.Click += (s, e) => EditCategoryDates(category);
                buttonPanel.Children.Add(editButton);
            }

            // Toggle Active/Inactive Button  
            var toggleButton = new Button
            {
                Content = category.IsActive ? "DISABLE" : "ENABLE",
                Style = (Style)FindResource("ModernActionButtonStyle"),
                Background = category.IsActive ? 
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#E5E7EB") ?? System.Windows.Media.Brushes.LightGray) :      // Light gray for disable
                    (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#BFDBFE") ?? System.Windows.Media.Brushes.LightBlue),     // Same light blue for enable
                Foreground = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFromString("#374151") ?? System.Windows.Media.Brushes.DarkGray), // Dark text for contrast
                FontSize = 14,
                Height = 44,
                MinWidth = 100,
                ToolTip = category.IsActive ? "Disable this category" : "Enable this category"
            };
            toggleButton.Click += (s, e) => ToggleCategory(category);
            buttonPanel.Children.Add(toggleButton);

            Grid.SetColumn(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            border.Child = grid;
            container.Children.Add(border);
            return container;
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
            // Create dropdown container with smooth animation feel
            var dropdownBorder = new Border
            {
                Background = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#F8FAFC") ?? System.Windows.Media.Brushes.AliceBlue,
                BorderBrush = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#E2E8F0") ?? System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1, 0, 1, 1), // No top border to connect with category item
                CornerRadius = new CornerRadius(0, 0, 8, 8), // Only bottom corners rounded
                Padding = new Thickness(20),
                Margin = new Thickness(0, -1, 0, 0) // Slight overlap to connect visually
            };

            var mainPanel = new StackPanel();
                
                // Compact header for dropdown
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var titleBlock = new TextBlock
                {
                    Text = $"📅 Edit {category.Name} Dates",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#374151") ?? System.Windows.Media.Brushes.Black,
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
                    Background = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#E5E7EB") ?? System.Windows.Media.Brushes.LightGray,         // Light gray to match theme
                    Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#374151") ?? System.Windows.Media.Brushes.DarkGray,         // Dark gray text
                    BorderThickness = new Thickness(0),
                    Template = CreateRoundButtonTemplate()
                };
                closeBtn.Click += (s, e) => CloseEditDropdown();
                Grid.SetColumn(closeBtn, 1);
                headerGrid.Children.Add(closeBtn);
                mainPanel.Children.Add(headerGrid);

                // Compact current dates display
                var currentInfo = new Border
                {
                    Background = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#DBEAFE") ?? System.Windows.Media.Brushes.LightBlue,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15, 8, 15, 8),
                    Margin = new Thickness(0, 0, 0, 20)
                };
                
                var currentText = new TextBlock
                {
                    Text = $"Current: {FormatSeasonDate(category.SeasonStartDate ?? "")} - {FormatSeasonDate(category.SeasonEndDate ?? "")}",
                    FontSize = 14,
                    FontWeight = FontWeights.Medium,
                    Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#1E40AF") ?? System.Windows.Media.Brushes.DarkBlue,
                    TextAlignment = TextAlignment.Center
                };
                currentInfo.Child = currentText;
                mainPanel.Children.Add(currentInfo);

                // Parse current dates
                var startParts = (category.SeasonStartDate ?? "01-01").Split('-');
                var endParts = (category.SeasonEndDate ?? "12-31").Split('-');
                
                int startMonth = int.TryParse(startParts[0], out var sm) ? sm : 1;
                int startDay = int.TryParse(startParts[1], out var sd) ? sd : 1;
                int endMonth = int.TryParse(endParts[0], out var em) ? em : 12;
                int endDay = int.TryParse(endParts[1], out var ed) ? ed : 31;

                // Create compact date controls for dropdown
                TextBlock startMonthLabel, startDayLabel, endMonthLabel, endDayLabel;
                var startDatePanel = CreateCompactDateControl("Season Start", startMonth, startDay, true, out startMonthLabel, out startDayLabel);
                var endDatePanel = CreateCompactDateControl("Season End", endMonth, endDay, false, out endMonthLabel, out endDayLabel);

                // Compact date editing grid
                var dateEditGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
                dateEditGrid.ColumnDefinitions.Add(new ColumnDefinition());
                dateEditGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
                dateEditGrid.ColumnDefinitions.Add(new ColumnDefinition());

                Grid.SetColumn(startDatePanel, 0);
                Grid.SetColumn(endDatePanel, 2);
                dateEditGrid.Children.Add(startDatePanel);
                dateEditGrid.Children.Add(endDatePanel);

                mainPanel.Children.Add(dateEditGrid);

                // Compact action buttons
                var buttonPanel = new StackPanel { 
                    Orientation = Orientation.Horizontal, 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                
                var saveButton = new Button
                {
                    Content = "💾 Save",
                    Width = 120,
                    Height = 40,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#BFDBFE") ?? System.Windows.Media.Brushes.LightBlue,        // Light blue theme
                    Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#1E40AF") ?? System.Windows.Media.Brushes.DarkBlue,        // Dark blue text
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    BorderThickness = new Thickness(0),
                    Template = CreateModernButtonTemplate()
                };
                
                var cancelButton = new Button
                {
                    Content = "❌ Cancel",
                    Width = 100,
                    Height = 40,
                    Background = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#E5E7EB") ?? System.Windows.Media.Brushes.LightGray,         // Light gray for cancel
                    Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#374151") ?? System.Windows.Media.Brushes.DarkGray,         // Dark gray text
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    BorderThickness = new Thickness(0),
                    Template = CreateModernButtonTemplate()
                };

                saveButton.Click += async (s, e) =>
        {
            try
            {
                        // Get values from stored references
                        if (startMonthLabel == null || startDayLabel == null || endMonthLabel == null || endDayLabel == null)
                {
                            NotificationService.Instance.ShowError("Save Error", "Control references are invalid. Please try again.");
                        return;
                    }

                        int startMonth = (int)startMonthLabel.Tag;
                        int startDay = (int)startDayLabel.Tag;
                        int endMonth = (int)endMonthLabel.Tag;
                        int endDay = (int)endDayLabel.Tag;
                        
                        string newStartDate = $"{startMonth:D2}-{startDay:D2}";
                        string newEndDate = $"{endMonth:D2}-{endDay:D2}";
                        
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
            catch (Exception ex)
            {
                        LoggingService.Application.Error("Error saving category dates", ex);
                        NotificationService.Instance.ShowError("Save Error", $"Error saving dates: {ex.Message}");
                    }
                };
                
                cancelButton.Click += (s, e) => CloseEditDropdown();
                
                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(cancelButton);
                mainPanel.Children.Add(buttonPanel);

                dropdownBorder.Child = mainPanel;
                return dropdownBorder;
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
                Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#4B5563") ?? System.Windows.Media.Brushes.DarkGray,
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

            var monthLeftBtn = CreateNavigationButton("←", "#BFDBFE");
            var monthDisplay = CreateDisplayBorder("#F8FAFC");
            var monthRightBtn = CreateNavigationButton("→", "#BFDBFE");

            monthLabel = new TextBlock
            {
                Text = monthNames[initialMonth - 1],
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#1E40AF") ?? System.Windows.Media.Brushes.DarkBlue,
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

            var dayLeftBtn = CreateNavigationButton("←", "#3B82F6");
            var dayDisplay = CreateDisplayBorder("#F1F5F9");
            var dayRightBtn = CreateNavigationButton("→", "#3B82F6");

            dayLabel = new TextBlock
            {
                Text = initialDay.ToString(),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString("#1E40AF") ?? System.Windows.Media.Brushes.DarkGray,
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
                Background = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString(backgroundColor) ?? System.Windows.Media.Brushes.Blue,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Template = CreateRoundButtonTemplate()
            };
        }

        private Border CreateDisplayBorder(string backgroundColor)
        {
            return new Border
            {
                Background = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString(backgroundColor) ?? System.Windows.Media.Brushes.LightBlue,
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
                
                // Basic validation for days in month
                int currentMonth = (int)monthLabel.Tag;
                var daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, currentMonth);
                if (newDay > daysInMonth) newDay = daysInMonth;
                
                dayLabel.Tag = newDay;
                dayLabel.Text = newDay.ToString();
            };

            rightBtn.Click += (s, e) =>
            {
                int currentDay = (int)dayLabel.Tag;
                int currentMonth = (int)monthLabel.Tag;
                var daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, currentMonth);
                
                int newDay = currentDay == daysInMonth ? 1 : currentDay + 1;
                
                dayLabel.Tag = newDay;
                dayLabel.Text = newDay.ToString();
            };
        }



        private ControlTemplate CreateRoundButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            
            // Create the visual tree
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(25)); // Perfect for 50px buttons
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            // Add hover effects
            var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85));
            template.Triggers.Add(trigger);
            
            var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7));
            template.Triggers.Add(pressTrigger);
            
            return template;
        }

        private ControlTemplate CreateModernButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            
            // Create the visual tree for rounded buttons
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(15));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            // Add modern hover and press effects
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.9));
            hoverTrigger.Setters.Add(new Setter(Control.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                BlurRadius = 12,
                ShadowDepth = 3,
                Opacity = 0.3
            }));
            template.Triggers.Add(hoverTrigger);
            
            var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7));
            pressTrigger.Setters.Add(new Setter(FrameworkElement.RenderTransformProperty, new ScaleTransform(0.98, 0.98)));
            template.Triggers.Add(pressTrigger);
            
            return template;
        }

        private ControlTemplate CreatePremiumButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            
            // Create premium rounded button with perfect circle design
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(35)); // Perfect circle for 70x70 button
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            // Premium animations and effects
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85));
            hoverTrigger.Setters.Add(new Setter(FrameworkElement.RenderTransformProperty, new ScaleTransform(1.05, 1.05)));
            template.Triggers.Add(hoverTrigger);
            
            var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7));
            pressTrigger.Setters.Add(new Setter(FrameworkElement.RenderTransformProperty, new ScaleTransform(0.95, 0.95)));
            template.Triggers.Add(pressTrigger);
            
            return template;
        }

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
                
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null && parentWindow != Application.Current.MainWindow)
                {
                    // This is a standalone modal dialog window - close it properly
                    LoggingService.Application.Information("Found standalone modal window, setting DialogResult to close");
                    parentWindow.DialogResult = false;
                    }
                    else
                    {
                    // This is likely shown via ModalService as an overlay - use ModalService to close
                    LoggingService.Application.Information("Modal shown via ModalService, using HideModal()");
                    ModalService.Instance.HideModal();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error closing category modal", ex);
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
                    // Ignore secondary errors
            }
        }
        }


    }
} 
