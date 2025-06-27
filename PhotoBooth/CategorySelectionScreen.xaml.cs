using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Category selection screen - first step in the new two-step template selection flow
    /// Shows template categories as large, touch-friendly cards
    /// </summary>
    public partial class CategorySelectionScreen : UserControl, IDisposable
    {
        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private List<TemplateCategory> _availableCategories;
        private ProductInfo? _currentProduct;
        private bool _disposed = false;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when the back button is clicked
        /// </summary>
        public event EventHandler? BackButtonClicked;

        /// <summary>
        /// Event fired when a category is selected
        /// </summary>
        public event EventHandler<CategorySelectedEventArgs>? CategorySelected;

        #endregion

        #region Constructor

        public CategorySelectionScreen()
        {
            _databaseService = new DatabaseService();
            _availableCategories = new List<TemplateCategory>();
            
            InitializeComponent();
            
            // Load categories when the control is loaded
            Loaded += async (s, e) => await LoadCategoriesAsync();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the product type for this category selection
        /// </summary>
        public void SetProductType(ProductInfo product)
        {
            _currentProduct = product;
            
                         // Update the subtitle based on product type
             if (ProductSubtitle != null)
             {
                 switch (product.Type?.ToLowerInvariant())
                 {
                     case "strips":
                     case "photostrips":
                         ProductSubtitle.Text = "Select a category for your photo strip";
                         break;
                     case "4x6":
                     case "photo4x6":
                         ProductSubtitle.Text = "Select a category for your 4x6 photo";
                         break;
                     case "phone":
                     case "smartphoneprint":
                         ProductSubtitle.Text = "Select a category for your phone photo";
                         break;
                     default:
                         ProductSubtitle.Text = "Select a category for your photos";
                         break;
                 }
             }
            
            // Reload categories for the specific product type
            _ = Task.Run(async () => await LoadCategoriesAsync());
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Load available categories from the database
        /// </summary>
        private async Task LoadCategoriesAsync()
        {
            try
            {
                if (LoadingPanel != null)
                    LoadingPanel.Visibility = Visibility.Visible;
                
                if (EmptyStatePanel != null)
                    EmptyStatePanel.Visibility = Visibility.Collapsed;

                // Get all active template categories
                var result = await _databaseService.GetTemplateCategoriesAsync();
                
                if (result.Success && result.Data != null)
                {
                    _availableCategories = result.Data
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.SortOrder)
                        .ThenBy(c => c.Name)
                        .ToList();

                    // Filter categories that have templates for the current product type
                    if (_currentProduct != null)
                    {
                        _availableCategories = await FilterCategoriesWithTemplatesAsync(_availableCategories);
                    }

                    await DisplayCategoriesAsync();
                }
                else
                {
                    _availableCategories.Clear();
                    ShowEmptyState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
                _availableCategories.Clear();
                ShowEmptyState();
            }
            finally
            {
                if (LoadingPanel != null)
                    LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Filter categories to only show those that have templates for the current product type
        /// </summary>
        private async Task<List<TemplateCategory>> FilterCategoriesWithTemplatesAsync(List<TemplateCategory> categories)
        {
            var filteredCategories = new List<TemplateCategory>();

            foreach (var category in categories)
            {
                // Check if this category has any templates for the current product type
                var templatesResult = await _databaseService.GetTemplatesByCategoryAsync(category.Id);
                
                if (templatesResult.Success && templatesResult.Data != null)
                {
                    // Check if any templates match the current product type
                    var hasMatchingTemplates = templatesResult.Data.Any(template => 
                        DoesTemplateMatchProduct(template, _currentProduct));

                    if (hasMatchingTemplates)
                    {
                        filteredCategories.Add(category);
                    }
                }
            }

            return filteredCategories;
        }

        /// <summary>
        /// Check if a template matches the current product type
        /// </summary>
        private bool DoesTemplateMatchProduct(Template template, ProductInfo? product)
        {
            if (product == null || template.Layout == null) return true;

            var productType = product.Type?.ToLowerInvariant();
            var templateCategory = template.Layout.ProductCategory?.Name?.ToLowerInvariant();

            return productType switch
            {
                "strips" or "photostrips" => templateCategory == "strips" || templateCategory == "photo strips",
                "4x6" or "photo4x6" => templateCategory == "4x6" || templateCategory == "photos",
                "phone" or "smartphoneprint" => templateCategory == "4x6" || templateCategory == "photos", // Phone prints use 4x6 templates
                _ => true
            };
        }

        /// <summary>
        /// Display the categories as cards with template preview covers
        /// </summary>
        private async Task DisplayCategoriesAsync()
        {
            if (CategoriesGrid == null) return;

            await Dispatcher.InvokeAsync(() =>
            {
                CategoriesGrid.Children.Clear();

                if (_availableCategories.Count == 0)
                {
                    ShowEmptyState();
                    return;
                }

                foreach (var category in _availableCategories)
                {
                    var categoryCard = CreateCategoryCard(category);
                    CategoriesGrid.Children.Add(categoryCard);
                }

                // Adjust grid columns based on number of categories
                AdjustGridColumns();
            });
        }

        /// <summary>
        /// Create a category card UI element with template preview as cover
        /// </summary>
        private Button CreateCategoryCard(TemplateCategory category)
        {
            var card = new Button
            {
                Style = (Style)FindResource("CategoryCardStyle"),
                Width = 280,
                Height = 200,
                Tag = category
            };

            // Create card content
            var cardContent = new Grid();

            // Load first template preview as background
            _ = LoadCategoryPreviewImageAsync(cardContent, category);

            // Semi-transparent overlay for text readability
            var overlay = new Border
            {
                Background = new SolidColorBrush(Colors.Black) { Opacity = 0.4 },
                CornerRadius = new CornerRadius(16)
            };
            cardContent.Children.Add(overlay);

            // Free/Premium badge - Top Right
            var badgeContainer = new Border
            {
                Background = new SolidColorBrush(GetBadgeColor(category)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12)
            };

            var badgeText = new TextBlock
            {
                Text = GetCategoryBadgeText(category),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            badgeContainer.Child = badgeText;
            cardContent.Children.Add(badgeContainer);

            // Category name - Bottom Center
            var nameContainer = new Border
            {
                Background = new SolidColorBrush(Colors.Black) { Opacity = 0.8 },
                CornerRadius = new CornerRadius(0, 0, 16, 16),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(16, 12, 16, 12)
            };

            var nameText = new TextBlock
            {
                Text = category.Name,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            nameContainer.Child = nameText;
            cardContent.Children.Add(nameContainer);

            card.Content = cardContent;

            // Add click handler
            card.Click += (s, e) => OnCategorySelected(category);

            return card;
        }

        /// <summary>
        /// Load the first template preview image as the category cover
        /// </summary>
        private async Task LoadCategoryPreviewImageAsync(Grid cardContent, TemplateCategory category)
        {
            try
            {
                // Get templates for this category
                var templatesResult = await _databaseService.GetTemplatesByCategoryAsync(category.Id);
                
                if (templatesResult.Success && templatesResult.Data != null && templatesResult.Data.Any())
                {
                    // Filter templates that match the current product
                    var matchingTemplates = templatesResult.Data
                        .Where(t => DoesTemplateMatchProduct(t, _currentProduct))
                        .ToList();

                    if (matchingTemplates.Any())
                    {
                        var firstTemplate = matchingTemplates.First();
                        
                        await Dispatcher.InvokeAsync(() =>
                        {
                            // Create image background
                            var previewImage = new Image
                            {
                                Stretch = Stretch.UniformToFill,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            };

                            // Create border with rounded corners for clipping
                            var imageBorder = new Border
                            {
                                CornerRadius = new CornerRadius(16),
                                ClipToBounds = true
                            };
                            imageBorder.Child = previewImage;

                            // Load image
                            if (!string.IsNullOrEmpty(firstTemplate.PreviewPath) && 
                                System.IO.File.Exists(firstTemplate.PreviewPath))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(firstTemplate.PreviewPath, UriKind.Absolute);
                                bitmap.DecodePixelWidth = 280; // Optimize for card size
                                bitmap.EndInit();
                                previewImage.Source = bitmap;
                            }
                            else
                            {
                                // Fallback to gradient background
                                imageBorder.Background = GetCategoryBackgroundBrush(category.Name);
                                imageBorder.Child = null;
                            }

                            // Insert at the beginning (behind overlay and text)
                            cardContent.Children.Insert(0, imageBorder);
                        });
                    }
                    else
                    {
                        // No matching templates, use gradient background
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var fallbackBorder = new Border
                            {
                                Background = GetCategoryBackgroundBrush(category.Name),
                                CornerRadius = new CornerRadius(16)
                            };
                            cardContent.Children.Insert(0, fallbackBorder);
                        });
                    }
                }
                else
                {
                    // No templates found, use gradient background
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var fallbackBorder = new Border
                        {
                            Background = GetCategoryBackgroundBrush(category.Name),
                            CornerRadius = new CornerRadius(16)
                        };
                        cardContent.Children.Insert(0, fallbackBorder);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading category preview for {category.Name}: {ex.Message}");
                
                // Fallback to gradient on error
                await Dispatcher.InvokeAsync(() =>
                {
                    var fallbackBorder = new Border
                    {
                        Background = GetCategoryBackgroundBrush(category.Name),
                        CornerRadius = new CornerRadius(16)
                    };
                    cardContent.Children.Insert(0, fallbackBorder);
                });
            }
        }

        /// <summary>
        /// Get badge text for category (Free or Premium)
        /// </summary>
        private string GetCategoryBadgeText(TemplateCategory category)
        {
            // For now, mark Premium categories based on name, but this could be database-driven
            return category.Name.ToLowerInvariant() switch
            {
                "premium" => "PREMIUM",
                "elegant" => "PREMIUM", 
                "wedding" => "PREMIUM",
                _ => "FREE"
            };
        }

        /// <summary>
        /// Get badge color for category
        /// </summary>
        private Color GetBadgeColor(TemplateCategory category)
        {
            var badgeText = GetCategoryBadgeText(category);
            return badgeText == "PREMIUM" 
                ? Color.FromRgb(168, 85, 247) // Purple for premium
                : Color.FromRgb(34, 197, 94);  // Green for free
        }

        /// <summary>
        /// Get background brush for category based on name
        /// </summary>
        private LinearGradientBrush GetCategoryBackgroundBrush(string categoryName)
        {
            var brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);

            switch (categoryName.ToLowerInvariant())
            {
                case "classic":
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(59, 130, 246), 0)); // Blue
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(29, 78, 216), 1));
                    break;
                case "fun":
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(249, 115, 22), 0)); // Orange
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(234, 88, 12), 1));
                    break;
                case "holiday":
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(220, 38, 127), 0)); // Pink
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(190, 24, 93), 1));
                    break;
                case "seasonal":
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(34, 197, 94), 0)); // Green
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(21, 128, 61), 1));
                    break;
                case "premium":
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(168, 85, 247), 0)); // Purple
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(124, 58, 237), 1));
                    break;
                default:
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(99, 102, 241), 0)); // Indigo
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(79, 70, 229), 1));
                    break;
            }

            return brush;
        }

        /// <summary>
        /// Get icon/emoji for category
        /// </summary>
        private string GetCategoryIcon(string categoryName)
        {
            return categoryName.ToLowerInvariant() switch
            {
                "classic" => "🎭",
                "fun" => "🎉",
                "holiday" => "🎄",
                "seasonal" => "🌸",
                "premium" => "⭐",
                "wedding" => "💍",
                "birthday" => "🎂",
                "party" => "🎊",
                "elegant" => "✨",
                "retro" => "📸",
                _ => "📷"
            };
        }

        /// <summary>
        /// Adjust grid columns based on number of categories
        /// </summary>
        private void AdjustGridColumns()
        {
            if (CategoriesGrid == null) return;

            var categoryCount = _availableCategories.Count;
            
            // Adjust columns based on count for better layout with larger cards
            CategoriesGrid.Columns = categoryCount switch
            {
                <= 2 => 2,
                <= 3 => 3,
                <= 6 => 3,
                _ => 3  // Keep max at 3 for larger, more visible cards
            };
        }

        /// <summary>
        /// Show empty state when no categories are available
        /// </summary>
        private void ShowEmptyState()
        {
            if (EmptyStatePanel != null)
                EmptyStatePanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Handle category selection
        /// </summary>
        private void OnCategorySelected(TemplateCategory category)
        {
            try
            {
                LoggingService.Application.Information("Category selected for template selection",
                    ("CategoryId", category.Id),
                    ("CategoryName", category.Name),
                    ("ProductType", _currentProduct?.Type ?? "Unknown"));

                CategorySelected?.Invoke(this, new CategorySelectedEventArgs(category, _currentProduct));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error handling category selection", ex,
                    ("CategoryId", category.Id),
                    ("CategoryName", category.Name));
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle back button click
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Back button clicked from category selection");
                BackButtonClicked?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error handling back button click", ex);
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Clean up managed resources
                _availableCategories?.Clear();
                _disposed = true;
            }
        }

        #endregion
    }

    #region Event Args Classes

    /// <summary>
    /// Event arguments for category selection
    /// </summary>
    public class CategorySelectedEventArgs : EventArgs
    {
        public TemplateCategory Category { get; }
        public ProductInfo? Product { get; }

        public CategorySelectedEventArgs(TemplateCategory category, ProductInfo? product)
        {
            Category = category;
            Product = product;
        }
    }

    #endregion
} 