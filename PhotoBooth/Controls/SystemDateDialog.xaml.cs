using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth.Controls
{
    public partial class SystemDateDialog : UserControl
    {
        private readonly IDatabaseService? _databaseService;
        private Window? _parentWindow;
        private Action<bool>? _showAllSeasonsCallback;

        public SystemDateDialog()
        {
            InitializeComponent();
        }

        public SystemDateDialog(IDatabaseService databaseService, Action<bool>? showAllSeasonsCallback = null)
        {
            InitializeComponent();
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _showAllSeasonsCallback = showAllSeasonsCallback;
        }

        /// <summary>
        /// Show the system date dialog as a modal popup
        /// </summary>
        public static async Task ShowSystemDateDialogAsync(Window parentWindow, IDatabaseService databaseService, Action<bool>? showAllSeasonsCallback = null)
        {
            try
            {
                var dialog = new SystemDateDialog(databaseService, showAllSeasonsCallback)
                {
                    _parentWindow = parentWindow
                };

                // Create modal overlay
                var overlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    Child = new Border
                    {
                        Background = Brushes.Transparent,
                        Child = dialog,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(40),
                        MaxWidth = 800,
                        Width = double.NaN // Allow dialog to size itself
                    }
                };

                // Add to parent window
                if (parentWindow.Content is Panel parentPanel)
                {
                    parentPanel.Children.Add(overlay);
                    
                    // Load data
                    await dialog.LoadSystemDateDataAsync();
                    
                    // Wait for dialog to close
                    await dialog.WaitForCloseAsync();
                    
                    // Remove overlay
                    parentPanel.Children.Remove(overlay);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing system date dialog: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Load system date and seasonal data
        /// </summary>
        private async Task LoadSystemDateDataAsync()
        {
            try
            {
                if (_databaseService == null)
                {
                    ShowErrorState("Database service is not available");
                    return;
                }
                
                var statusResult = await _databaseService.GetSystemDateStatusAsync();
                if (statusResult.Success && statusResult.Data != null)
                {
                    var status = statusResult.Data;
                    
                    // Update system information
                    SystemDateText.Text = status.CurrentSystemDateString;
                    TimeZoneText.Text = status.TimeZone;
                    SeasonDateText.Text = status.CurrentDateForSeason;
                    ActiveSeasonsText.Text = $"{status.ActiveSeasonsCount} active";
                    
                    // Load seasonal categories
                    LoadSeasonalCategories(status.SeasonalCategories);
                }
                else
                {
                    ShowErrorState($"Failed to load system date status: {statusResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ShowErrorState($"Error loading system date data: {ex.Message}");
            }
        }

        /// <summary>
        /// Load and display seasonal categories
        /// </summary>
        private void LoadSeasonalCategories(System.Collections.Generic.List<SeasonStatus> seasonalCategories)
        {
            SeasonalCategoriesPanel.Children.Clear();
            
            if (seasonalCategories == null || !seasonalCategories.Any())
            {
                NoSeasonsPanel.Visibility = Visibility.Visible;
                return;
            }
            
            NoSeasonsPanel.Visibility = Visibility.Collapsed;
            
            foreach (var season in seasonalCategories)
            {
                var seasonCard = CreateSeasonCard(season);
                SeasonalCategoriesPanel.Children.Add(seasonCard);
            }
        }

        /// <summary>
        /// Create a card for displaying seasonal category information
        /// </summary>
        private Border CreateSeasonCard(SeasonStatus season)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 16, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80, GridUnitType.Pixel) });

            // Category information
            var infoPanel = new StackPanel();
            
            var nameText = new TextBlock
            {
                Text = season.CategoryName,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };
            
            var dateText = new TextBlock
            {
                Text = $"{season.SeasonStartDate} to {season.SeasonEndDate}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap
            };
            
            var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            if (season.SpansYears)
            {
                var spansYearsChip = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock
                    {
                        Text = "Spans Years",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175)),
                        FontWeight = FontWeights.Medium
                    }
                };
                detailsPanel.Children.Add(spansYearsChip);
            }
            
            var priorityText = new TextBlock
            {
                Text = $"Priority: {season.SeasonalPriority}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175))
            };
            detailsPanel.Children.Add(priorityText);
            
            infoPanel.Children.Add(nameText);
            infoPanel.Children.Add(dateText);
            infoPanel.Children.Add(detailsPanel);
            
            Grid.SetColumn(infoPanel, 0);

            // Status badge
            var statusBorder = new Border
            {
                Style = season.IsCurrentlyActive ? 
                    (Style)FindResource("ActiveSeasonStyle") : 
                    (Style)FindResource("InactiveSeasonStyle"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            
            var statusText = new TextBlock
            {
                Text = season.IsCurrentlyActive ? "ACTIVE" : "Inactive",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = season.IsCurrentlyActive ? 
                    new SolidColorBrush(Color.FromRgb(22, 163, 74)) : 
                    new SolidColorBrush(Color.FromRgb(220, 38, 38))
            };
            
            statusBorder.Child = statusText;
            Grid.SetColumn(statusBorder, 1);

            grid.Children.Add(infoPanel);
            grid.Children.Add(statusBorder);
            
            card.Child = grid;
            return card;
        }

        /// <summary>
        /// Show error state when data loading fails
        /// </summary>
        private void ShowErrorState(string errorMessage)
        {
            SystemDateText.Text = "Error loading data";
            TimeZoneText.Text = "Unknown";
            SeasonDateText.Text = "Unknown";
            ActiveSeasonsText.Text = "Unknown";
            
            SeasonalCategoriesPanel.Children.Clear();
            
            var errorPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16)
            };
            
            var errorStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            errorStack.Children.Add(new TextBlock
            {
                Text = "⚠️",
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });
            errorStack.Children.Add(new TextBlock
            {
                Text = "Error Loading Data",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            errorStack.Children.Add(new TextBlock
            {
                Text = errorMessage,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            
            errorPanel.Child = errorStack;
            SeasonalCategoriesPanel.Children.Add(errorPanel);
        }

        /// <summary>
        /// Wait for the dialog to be closed
        /// </summary>
        private async Task WaitForCloseAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            
            EventHandler? closedHandler = null;
            closedHandler = (s, e) =>
            {
                DialogClosed -= closedHandler;
                tcs.SetResult(true);
            };
            
            DialogClosed += closedHandler;
            
            await tcs.Task;
        }

        private event EventHandler? DialogClosed;

        #region Event Handlers

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        private void ShowAllSeasonsButton_Click(object sender, RoutedEventArgs e)
        {
            _showAllSeasonsCallback?.Invoke(true);
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        private async void RefreshDataButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadSystemDateDataAsync();
        }

        #endregion
    }
} 